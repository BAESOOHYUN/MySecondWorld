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
        private const float GroundCheckDistance = 0.1f; // MovementController와 동일 기본값

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
            try                                                     // 서버 종료 시 ct가 취소되는 것 처리 — 루프 자체의 정상 종료
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

                        try                                         // 플레이어 한 명 데이터에 문제 생겨도 다른 플레이어까지 안 죽게 격리
                        {
                            var conn = NetworkServer.connections[session.Key];
                            var playerPos = conn.identity.transform.position;
                            var needScenes = new HashSet<string>(); // 이번 틱에 필요한 씬 목록

                            bool grounded = CheckGrounded(session, conn);
                            ResolveCurrentZone(session, conn, playerPos, grounded);
                            RecoverFromFall(session, conn);
                            SyncAdditiveScenes(session, conn, needScenes, playerPos);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[S-Loop] conn={session.Key} 처리 중 예외 발생 — 이 세션만 스킵: {e}");
                        }
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(_manager.zoneCheckInterval), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)                      // ← 루프 전체를 감싸는 바깥 try의 catch
            {
                // 서버 중지 시 정상 종료
            }
        }

        // 이 씬이 어느 월드에 속하는지 zones에서 해당 존을 찾아 WorldId(번들 라벨)를 읽어온다.
        // currentScene이 유일한 원본이므로 월드를 세션에 따로 저장하지 않는다(두 값이 어긋날 일 없게).
        private string WorldOf(string sceneName) =>
            _manager.zones.FirstOrDefault(z => z.sceneName == sceneName)?.WorldId;

        // [S-Loop-1] 지면 존재 여부 판정 — 존 안이어도 지형이 아직 안 떠 있으면 허공일 수 있음
        public bool CheckGrounded(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn)
        {
            var controller = conn.identity.GetComponent<CharacterController>();
            if (controller == null)
            {
                Debug.LogWarning($"[S-Loop] conn={conn.connectionId}에 CharacterController 없음 — 낙하 체크 스킵", conn.identity);
                return false;
            }

            Vector3 origin = conn.identity.transform.position + controller.center;
            float radius = controller.radius - controller.skinWidth;
            float distance = (controller.height / 2f) - radius + GroundCheckDistance;
            bool grounded = Physics.SphereCast(origin, radius, Vector3.down, out _, distance, _manager.groundLayer);

            if (grounded)
                session.Value.ungroundedTimer = 0;
            else
                session.Value.ungroundedTimer += _manager.zoneCheckInterval;

            return grounded;
        }

        // [S-Loop-2] 소속 씬 판정 (정확한 bounds) + 낙하 타이머 가동
        public void ResolveCurrentZone(
            KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, Vector3 playerPos, bool grounded)
        {
            bool inAnyZone = false;
            string currentWorld = WorldOf(session.Value.currentScene) ?? WorldOf(_manager.defaultSubScene);

            var currentZone = _manager.zones.FirstOrDefault(z => z.sceneName == session.Value.currentScene);
            if (currentZone != null && currentZone.bounds.Contains(playerPos))
            {
                inAnyZone = true;
            }
            else
            {
                foreach (var zone in _manager.zones)
                {
                    // 다른 월드의 존은 후보로 삼지 않는다 — 월드 간 이동은 포탈로만 일어난다.
                    if (zone.WorldId != currentWorld) continue;

                    if (zone.bounds.Contains(playerPos))
                    {
                        inAnyZone = true;
                        PlayerChangedScene(conn, zone.sceneName);
                        break;  // 한 플레이어는 하나의 존에만 소속
                    }
                }
            }

            if (inAnyZone)
            {
                session.Value.fallTimer = 0;
                // 안전 위치는 "존 안"이 아니라 "발밑에 지형이 있을 때"만 기록한다.
                if (grounded)
                {
                    session.Value.lastSafePosition = playerPos;
                    session.Value.lastSafeScene = session.Value.currentScene;
                }
            }
            else
            {
                session.Value.fallTimer += _manager.zoneCheckInterval;
            }
        }

        // [S-Loop-2-a] 소속 씬 변경 — MoveGameObjectToScene으로 플레이어 가시성 갱신
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

        // [S-Loop-2-b] 던전 진입/퇴장 시 matchId 갱신 (MatchInterestManagement가 matchId로 가시성 판단)
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

        // [S-Loop-3] 낙하 복귀 텔레포트(존 이탈 3초 / 발밑 지형 없음 3초 / 구조 한계선 통과)
        public void RecoverFromFall(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn)
        {
            // 구조 한계선은 존 판정을 전혀 믿지 않는 최후의 방어선
            // — 겹치는 bounds가 낙하 경로를 계속 "존 안"으로 판정해서 타이머가 안 도는 상황에서도 반드시 걸린다.
            bool belowRescueLine = conn.identity.transform.position.y < _manager.fallRescueY;

            if (session.Value.fallTimer < 3f && session.Value.ungroundedTimer < 3f && !belowRescueLine)
                return;

            // 안전 위치 결정
            Vector3 safePos;
            string safeScene;

            if (session.Value.lastSafePosition != Vector3.zero
                && !string.IsNullOrEmpty(session.Value.lastSafeScene))
            {
                // 위치와 씬은 항상 같이 기록되므로 같이 꺼내 쓴다.
                // currentScene은 낙하 중에 바뀌어 있을 수 있으므로 lastSafeScene를 사용.
                safePos = session.Value.lastSafePosition;
                safeScene = session.Value.lastSafeScene;
            }
            else // 한 번도 지면에 서 본 적이 없음 → 스폰 지점으로
            {
                safeScene = _manager.defaultSubScene;

                // GetStartPosition()은 조회처럼 보이지만 RoundRobin 인덱스를 전진시킨다(NetworkManager.cs:1123).
                // 구조는 스폰이 아니므로 신규 플레이어의 스폰 순번을 건드리면 안 된다 → 리스트를 직접 읽는다.
                // (파괴된 Transform이 남아 있을 수 있어 null이 아닌 것 중 첫 번째를 고른다)
                var startPos = NetworkManager.startPositions.FirstOrDefault(t => t != null);

                if (startPos != null)
                {
                    safePos = startPos.position;
                }
                else // 살아있는 NetworkStartPosition이 하나도 없는 비정상 상황
                {
                    // 여기서 원점(Vector3.zero)으로 보내면 필드 지면(Y≈15)보다 아래라
                    // 구조가 곧바로 재낙하가 된다 — 고치려던 버그를 구조 로직이 다시 만드는 셈.
                    // 필드 존 bounds의 중심은 정의상 필드 콘텐츠 안이므로 훨씬 안전한 최후 수단.
                    var fieldZone = _manager.zones.FirstOrDefault(z => z.sceneName == safeScene);
                    safePos = fieldZone != null ? fieldZone.bounds.center : Vector3.zero;
                    Debug.LogError(
                        $"[S-Loop] NetworkStartPosition이 하나도 없다 — 필드 bounds 중심({safePos})으로 대체 구조. 씬 배치 확인 필요.");
                }
            }

            // 씬 이동을 먼저 — 목적지 씬으로 옮긴 뒤에 위치를 확정해야 가시성 판정이 어긋나지 않는다
            if (session.Value.currentScene != safeScene)
            {
                SceneManager.MoveGameObjectToScene(
                    conn.identity.gameObject, _manager.GetServerScene(safeScene));
                session.Value.currentScene = safeScene;
            }

            // transform.position 직접 대입은 다음 클라 스냅샷에 덮어써진다 (권한이 클라에 있음).
            // 반드시 소유 클라까지 내려가는 경로로 보낼 것.
            if (conn.identity.TryGetComponent(out MySecondWorld.Network.Player.NetworkPlayerController npc))
                npc.ServerRescueTo(safePos);

            session.Value.fallTimer = 0;
            session.Value.ungroundedTimer = 0;
        }

        // [S-Loop-4] 씬 로드/언로드 판정 (bounds + preloadMargin 확장 영역)
        public void SyncAdditiveScenes(
            KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, HashSet<string> needScenes, Vector3 playerPos)
        {
            string currentWorld = WorldOf(session.Value.currentScene) ?? WorldOf(_manager.defaultSubScene);

            // preloadMargin만큼 확장된 bounds 안이면 needScenes에 추가
            foreach (var zone in _manager.zones)
            {
                if (zone.WorldId != currentWorld)
                    continue; // 다른 월드는 스트리밍하지 않는다

                var expandedBounds = new Bounds(
                    zone.bounds.center,
                    zone.bounds.size + Vector3.one * zone.preloadMargin * 2);

                if (expandedBounds.Contains(playerPos))
                    needScenes.Add(zone.sceneName);
            }

            // 지금 서 있는 씬은 어떤 판정이 나오든 절대 언로드하지 않는다.
            // 발밑 지형이 사라지는 순간 무한낙하가 시작되므로 이게 최후의 방어선.
            if (!string.IsNullOrEmpty(session.Value.currentScene))
                needScenes.Add(session.Value.currentScene);

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

        // [S-Loop-4-a] 씬 로드 지시 — 클라이언트가 지형을 볼 수 있게 함
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
                conn.Send(new BundleRequiredMessage { labels = new[] { label } }); // > GameNetworkManager.OnStartClient
            }
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = true });
            session.loadedScenes.Add(sceneName);
        }


        // [S-Loop-4-b] 씬 언로드 지시 — 메모리 절약
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