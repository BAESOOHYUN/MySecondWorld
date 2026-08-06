using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using MySecondWorld.Combat.Companion; // ICompanionServerState

namespace MySecondWorld.Network
{
    // 서버 측 Zone 관리. 존 체크 루프 + 씬 동기화 담당.
    public class ServerZoneService
    {
        private readonly GameNetworkManager _manager;
        private readonly ServerSessionService _sessionService;
        private readonly DungeonRoomRegistry _roomRegistry;

        public ServerZoneService(GameNetworkManager manager, ServerSessionService sessionService, DungeonRoomRegistry roomRegistry)
        {
            _manager = manager;
            _sessionService = sessionService;
            _roomRegistry = roomRegistry;
        }


        // [S-Loop] zoneCheckInterval마다 위치 체크 (S5에서 isSpawned=true 된 플레이어만)
        // spawn 이후 Zone 이동 시 추가 번들을 처리하는 역할
        public async UniTaskVoid CheckZones(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // ToList()로 스냅샷: 순회 도중 접속/퇴장으로 _sessions가 변경되면
                    // InvalidOperationException이 발생해 코루틴이 죽고 씬 동기화가 멈춘다.
                    foreach (var session in _sessionService.Sessions.ToList())
                    {
                        if (!_sessionService.Sessions.ContainsKey(session.Key)) continue; // 순회 중 제거된 경우
                        if (!session.Value.isSpawned) continue;
                        if (!NetworkServer.connections.ContainsKey(session.Key))
                        {
                            Debug.LogWarning($"[S-Loop] connections에 conn={session.Key} 없음!");
                            continue;
                        }

                        var conn = NetworkServer.connections[session.Key];
                        var playerPos = conn.identity.transform.position;
                        var needScenes = new HashSet<string>(); // 이번 틱에 필요한 씬 목록

                        ResolveCurrentZone(session, conn, playerPos);
                        RecoverFromFall(session, conn);
                        SyncAdditiveScenes(session, conn, needScenes, playerPos);
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(_manager.zoneCheckInterval), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 서버 중지 시 정상 종료
            }
        }

        // [S-Loop-1] 소속 씬 판정 (정확한 bounds) + 낙하 타이머 가동
        public void ResolveCurrentZone(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, Vector3 playerPos)
        {
            bool inAnyZone = false;

            foreach (var zone in _manager.zones)
            {
                if (zone.bounds.Contains(playerPos))
                {
                    inAnyZone = true;
                    session.Value.fallTimer = 0;
                    session.Value.lastSafePosition = playerPos;
                    PlayerChangedScene(conn, zone.sceneName);
                    break;  // 한 플레이어는 하나의 존에만 소속
                }
            }

            if (!inAnyZone)
                session.Value.fallTimer += _manager.zoneCheckInterval;
        }

        // [S-Loop-1-a] 소속 씬 변경 — MoveGameObjectToScene으로 플레이어 가시성 갱신
        public void PlayerChangedScene(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessionService.Sessions[conn.connectionId];
            if (session.currentScene == sceneName) return;

            // LeaveRoom에서 이전 씬이 필요하므로 MoveGameObjectToScene 전에 저장
            string prevScene = session.currentScene;

            SceneManager.MoveGameObjectToScene(
                conn.identity.gameObject,
                SceneManager.GetSceneByPath(sceneName));
            session.currentScene = sceneName;

            // 플레이어 씬 이동 직후에 companion도 같은 씬으로 이동
            if (session.companionIdentity != null)
            {
                SceneManager.MoveGameObjectToScene(
                    session.companionIdentity.gameObject, SceneManager.GetSceneByPath(sceneName));
            }

            // 던전 씬 진입/퇴장 분기 (matchId 갱신)
            UpdateDungeonMatch(conn, session, prevScene, sceneName);
        }

        // [S-Loop-1-b] 던전 진입/퇴장 시 matchId 갱신 (MatchInterestManagement가 matchId로 가시성 판단)
        private void UpdateDungeonMatch(
            NetworkConnectionToClient conn, PlayerSession session, string prevScene, string sceneName)
        {
            var networkMatch = conn.identity.GetComponent<NetworkMatch>();
            if (networkMatch == null) return;

            if (_roomRegistry.IsDungeonScene(sceneName))
            {
                // 던전 진입 — 이전 던전 방이 있었다면 먼저 퇴장
                // Guid.Empty = "방 없음" (필드 상태)
                if (session.roomId != Guid.Empty)
                    _roomRegistry.LeaveRoom(prevScene, conn.connectionId);

                var roomId = _roomRegistry.AssignRoom(sceneName, conn.connectionId);
                networkMatch.matchId = roomId; // MatchInterestManagement가 이 값으로 가시성 판단
                session.roomId = roomId;       // 서버 내부 추적용
            }
            else
            {
                // 필드 복귀 — 이전에 던전에 있었다면 방 퇴장
                if (session.roomId != Guid.Empty)
                {
                    _roomRegistry.LeaveRoom(prevScene, conn.connectionId);
                    session.roomId = Guid.Empty;
                }
                // 필드→필드, 던전→필드 모두 항상 갱신
                networkMatch.matchId = SceneMatchId.GetMatchIdFromScene(sceneName);

                // 필드 복귀 시 죽어있던 companion 부활
                if (session.companionIdentity != null
                    && session.companionIdentity.TryGetComponent(out ICompanionServerState companionState)
                    && !companionState.IsAlive)
                {
                    companionState.Revive(conn.identity.transform.position);
                }
            }

            // companion matchId를 플레이어와 동일하게 맞춤
            // DungeonRoomRegistry는 안 거침(정원 계산에서 제외, "방 인원 수(4)"에는 안 잡힘)
            if (session.companionIdentity != null 
                && session.companionIdentity.TryGetComponent(out NetworkMatch companionMatch))
                companionMatch.matchId = networkMatch.matchId;
        }

        // [S-Loop-2] 낙하 복귀 텔레포트 (fallTimer >= 3초)
        public void RecoverFromFall(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn)
        {
            if (session.Value.fallTimer >= 3f)
            {
                // 안전 위치 결정
                Vector3 safePos;
                string safeScene;

                if (session.Value.lastSafePosition != Vector3.zero)
                {
                    safePos = session.Value.lastSafePosition;
                    safeScene = session.Value.currentScene;
                }
                else // 한 번도 zone 안에 없었음 → 초기 스폰 위치로
                {
                    safePos = _manager.GetStartPosition()?.position ?? Vector3.zero;
                    safeScene = _manager.defaultSubScene;
                }

                // 텔레포트
                conn.identity.transform.position = safePos;

                // 씬이 다르면 이동
                if (session.Value.currentScene != safeScene)
                {
                    SceneManager.MoveGameObjectToScene(
                        conn.identity.gameObject,
                        SceneManager.GetSceneByPath(safeScene));
                    session.Value.currentScene = safeScene;
                }
                session.Value.fallTimer = 0;
            }
        }

        // [S-Loop-3] 씬 로드/언로드 판정 (bounds + preloadMargin 확장 영역)
        public void SyncAdditiveScenes(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, HashSet<string> needScenes, Vector3 playerPos)
        {
            // preloadMargin만큼 확장된 bounds 안이면 needScenes에 추가
            foreach (var zone in _manager.zones)
            {
                var expandedBounds = new Bounds(
                    zone.bounds.center,
                    zone.bounds.size + Vector3.one * zone.preloadMargin * 2);

                if (expandedBounds.Contains(playerPos))
                    needScenes.Add(zone.sceneName);
            }

            // 새로 필요해진 씬 로드
            foreach (var scene in needScenes)
            {
                if (!session.Value.loadedScenes.Contains(scene))
                    PlayerEnteredZone(conn, scene);
            }

            // 더 이상 필요 없는 씬 언로드
            // 순회 중 loadedScenes를 수정하면 에러가 나므로 제거 대상을 먼저 수집
            var scenesToUnload = new List<string>();
            foreach (var scene in session.Value.loadedScenes)
            {
                if (!needScenes.Contains(scene))
                    scenesToUnload.Add(scene);
            }
            foreach (var scene in scenesToUnload)
                PlayerLeftZone(conn, scene);
        }

        // [S-Loop-3-a] 씬 로드 지시 — 클라이언트가 지형을 볼 수 있게 함
        public void PlayerEnteredZone(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessionService.Sessions[conn.connectionId];
            if (session.loadedScenes.Contains(sceneName)) return;

            // AdditiveSceneMessage 보내기 전에 BundleRequiredMessage 먼저 전송해야 번들 다운로드가 씬 로드보다 먼저 시작됨
            var zone = _manager.zones.FirstOrDefault(z => z.sceneName == sceneName);
            var label = zone?.ZoneAssetLabel?.labelString;
            // label이 있을 때만 BundleRequiredMessage 전송
            if (!string.IsNullOrEmpty(label) && !session.sentBundleLabels.Contains(label)) // 중복 체크
            {
                session.sentBundleLabels.Add(label);
                conn.Send(new BundleRequiredMessage { zoneLabel = label }); // > GameNetworkManager.OnStartClient
            }
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = true });
            session.loadedScenes.Add(sceneName);
        }


        // [S-Loop-3-b] 씬 언로드 지시 — 메모리 절약
        public void PlayerLeftZone(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessionService.Sessions[conn.connectionId];
            if (!session.loadedScenes.Contains(sceneName)) 
                return;
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = false }); // > GameNetworkManager.OnStartClient
            session.loadedScenes.Remove(sceneName);
        }
    }
}