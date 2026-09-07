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
                        if (session.Value.isTransitioningPortal) continue; // 포탈 전환 진행 중 — 정기 루프와 경합 방지

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

        // [S-Loop-Portal] 포탈 감지 — 물리 OnTriggerEnter 대신 서버가 bounds로 직접 폴링(짧은 주기).
        // 원격 클라 서버 사본은 CharacterController가 항상 꺼져있어 물리 트리거가 못 걸리기 때문(2026-08-26).
        // OnPortalTriggerEntered 내부 가드(isTransitioningPortal/쿨다운/월드일치)가 이미 중복호출을 걸러줘서
        // 트리거 범위 안에 있는 동안 매 틱 호출해도 안전함 — 별도 진입/이탈 추적 불필요.
        public async UniTaskVoid CheckPortals(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    foreach (var session in _sessionService.Sessions.ToList())
                    {
                        // 방어코드
                        if (!_sessionService.Sessions.ContainsKey(session.Key)) continue; // 우리 장부(_sessionService)에 해당 세션이 종료되거나 해서 없음, 무시.
                        if (!session.Value.isSpawned) continue;  // 세션이 월드에 스폰안됨, 무시
                        if (session.Value.isTransitioningPortal) continue;  // 포탈에서 이동중인 세션, 무시
                        if (!NetworkServer.connections.TryGetValue(session.Key, out var conn) || conn.identity == null) continue; //Mirror 쪽 딕셔너리에 우리 세션이 없음, 무시

                        try
                        {
                            Vector3 playerPos = conn.identity.transform.position;
                            // 던전 씬이 로드되면서 각 PortalTrigger가 스스로 켜지고 꺼질 때마다 알아서 리스트를 갱신하는 방식
                            // Active를 읽을 때는 이미 최신 상태가 보장
                            foreach (var trigger in ZonePortalTrigger.Active)
                            {
                                bool inside = trigger.Bounds.Contains(playerPos);

                                if (trigger.Portal == session.Value.lastUsedPortal)
                                {
                                    // 방금 이 포탈을 타고 온 경우 — 범위를 벗어난 적이 있어야만 재탑승 허용
                                    if (!inside)
                                        session.Value.hasLeftLastPortalBounds = true; // 도착 지점을 실제로 벗어남
                                    if (inside && !session.Value.hasLeftLastPortalBounds)
                                        continue;  // 방금 탄 그 포탈 범위 안에 계속 있음 — 재진입 무시
                                }

                                if (inside)
                                    OnPortalTriggerEntered(conn, trigger.Portal, trigger.SourceZone);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[S-Loop-Portal] conn={session.Key} 처리 중 예외 — 이 세션만 스킵: {e}");
                        }
                    }

                    //"0.1초마다 내가 직접 '지금 이 플레이어 위치가 트리거 안에 있나?' 하고 계속 물어봄" — 능동적으로 확인
                    await UniTask.Delay(TimeSpan.FromSeconds(_manager.portalCheckInterval), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
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
            ZoneDefinitionSO detectedZone = null;
            // ─────────────────────────────────────────────────────────────
            // [1단계: 최우선] 실제로 발을 딛고 서 있는 바닥 콜라이더의 씬 확인
            // ─────────────────────────────────────────────────────────────
            var controller = conn.identity.GetComponent<CharacterController>();
            Vector3 origin = conn.identity.transform.position + (controller != null ? controller.center : Vector3.up);
            float radius = controller != null ? Mathf.Max(0.2f, controller.radius - controller.skinWidth) : 0.3f;
            float distance = (controller != null ? (controller.height / 2f) : 1f) + 2.5f; // 계단 경사로 감안 넉넉한 거리
                                                                                          // 트리거를 제외한 바닥 물리 콜라이더를 SphereCast로 확실하게 검출
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Scene hitScene = hit.collider.gameObject.scene;
                if (hitScene.IsValid() && !string.IsNullOrEmpty(hitScene.name))
                {
                    // 1단계: 밟은 콜라이더의 씬과 일치하는 ZoneDefinitionSO 탐색
                    //  핵심: Addressables 씬은 scene.path가 비어있으므로 scene.name으로 비교
                    var candidateZone = _manager.zones.FirstOrDefault(z =>
                        z.sceneName == hitScene.path ||
                        System.IO.Path.GetFileNameWithoutExtension(z.sceneName) == hitScene.name ||
                        z.sceneName.EndsWith("/" + hitScene.name + ".unity"));
                    // 2단계: 현재 세션의 월드(currentWorld)와 일치하는 바닥만 인정
                    // 던전에 있을 때 겹쳐 있는 FieldScene 콜라이더를 밟아 필드로 강제 추방되는 것 차단
                    if (candidateZone != null && candidateZone.WorldId == currentWorld)
                    {
                        detectedZone = candidateZone;
                        Debug.Log($"[Zone] 바닥 감지 성공: 밟은 오브젝트='{hit.collider.name}', 씬='{hitScene.name}' ➔ Zone='{detectedZone.name}' (World: {currentWorld})");
                    }
                    else if (candidateZone != null)
                    {
                        // 다른 월드의 겹친 콜라이더(예: 던전 입구에서 FieldScene 지형)를 밟았을 때 가드가 정상 작동했음을 확인
                        Debug.Log($"[Zone] 바닥 감지됐으나 월드 불일치로 무시: 밟은 씬='{hitScene.name}'(WorldId={candidateZone.WorldId}) ≠ currentWorld='{currentWorld}'");
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────
            // [2단계: 폴백] 점프 중이거나 허공일 때는 기존 Bounds 판정 수행
            // ─────────────────────────────────────────────────────────────
            if (detectedZone == null)
            {
                // 2-A. 현재 머물고 있는 씬의 Bounds 안에 여전히 있다면 현재 씬 유지 (히스테리시스)
                var currentZone = _manager.zones.FirstOrDefault(z => z.sceneName == session.Value.currentScene);
                if (currentZone != null && currentZone.bounds.Contains(playerPos))
                {
                    detectedZone = currentZone; // 현재 머물고 있는 씬 유지
                }
                else
                {
                    // 2-B. 완전히 다른 영역으로 벗어났을 때만 가장 작은 부피의 겹친 Zone 탐색
                    ZoneDefinitionSO bestZone = null;
                    float bestVolume = float.MaxValue;
                    foreach (var zone in _manager.zones)
                    {
                        if (zone.WorldId != currentWorld) continue;
                        if (!zone.bounds.Contains(playerPos)) continue;
                        float volume = zone.bounds.size.x * zone.bounds.size.y * zone.bounds.size.z;
                        if (volume < bestVolume)
                        {
                            bestVolume = volume;
                            bestZone = zone;
                        }
                    }
                    detectedZone = bestZone;
                }
            }
            // ─────────────────────────────────────────────────────────────
            // [3단계: 씬 소속 갱신]
            // ─────────────────────────────────────────────────────────────
            if (detectedZone != null)
            {
                inAnyZone = true;
                // 판정된 씬이 현재 세션 씬과 다르면 즉시 소속 씬 전환 (MoveGameObjectToScene)
                if (session.Value.currentScene != detectedZone.sceneName)
                {
                    Debug.Log($"[Zone] 씬 전환 감지: {session.Value.currentScene} ➔ {detectedZone.sceneName} (밟고 있는 바닥 기준)");
                    PlayerChangedScene(conn, detectedZone.sceneName);
                }
            }
            // 존 이탈 타이머 갱신
            if (inAnyZone)
                session.Value.fallTimer = 0;
            else
                session.Value.fallTimer += _manager.zoneCheckInterval;
            // 안전 위치 기록
            if (grounded && inAnyZone)
            {
                session.Value.lastSafePosition = playerPos;
                session.Value.lastSafeScene = session.Value.currentScene;
            }
        }

        // [S-Loop-2-a] 소속 씬 변경 — MoveGameObjectToScene으로 플레이어 가시성 갱신
        public void PlayerChangedScene(NetworkConnectionToClient conn, string sceneName)
        {
            // session = _sessionService.Sessions[conn.connectionId]처럼 인덱서로 직접 접근하면
            // 전환 중(최대 15초) 접속이 끊긴 경우 세션이 이미 삭제된 상태라 KeyNotFoundException이 난다.
            // TryGetValue로 세션 존재 여부부터 확인 — 없으면(접속 종료) 조용히 리턴.
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;

            if (session.currentScene == sceneName) return;

            var targetScene = _manager.GetServerScene(sceneName);
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                // session.currentScene을 여기서 갱신해버리면 실제 GameObject는 예전 씬에 남는데
                // 서버 상태만 새 씬으로 넘어간 것처럼 꼬인다(matchId·바운드 판정 전부 어긋남) —
                // 그 예전 씬이 나중에 언로드될 때 클라의 "언로드 대피" 안전장치가 플레이어를
                // MainScene으로 끌고 가는 사고로 이어진다. 그러느니 이번 전환은 그냥 취소한다.
                Debug.LogError($"[Zone] '{sceneName}' 전환 실패 — GetServerScene이 로드된 씬을 못 찾음. conn={conn.connectionId}");
                return;
            }

            // LeaveRoom에서 이전 씬이 필요하므로 MoveGameObjectToScene 전에 저장
            string prevScene = session.currentScene;

            if (conn.identity != null)
            {
                var playerGo = conn.identity.gameObject;
                if (playerGo.transform.parent != null)
                    playerGo.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(playerGo, targetScene);
            }

            session.currentScene = sceneName;
            // 클라에도 소속 씬을 알린다 — 클라의 ActiveScene/캐릭터 소속을 서버 권위에 맞추는 유일한 경로.
            conn.Send(new PlayerSceneChangedMessage { sceneName = sceneName });

            // 플레이어 씬 이동 직후에 companion도 같은 씬으로 이동
            if (session.companionIdentity != null)
            {
                var compGo = session.companionIdentity.gameObject;
                if (compGo.transform.parent != null)
                    compGo.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(compGo, targetScene);

                // 이전 씬에 남겨진 몬스터(_targetM)를 계속 쫓아가려는 상태를 초기화
                // — 안 하면 이제 존재하지 않는 씬의 좌표를 향해 영원히 길찾기를 시도한다
                if (compGo.TryGetComponent(out ICompanionServerState companionState))
                    companionState.ResetCombatState();
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

                var roomId = _roomRegistry.AssignRoom(sceneName, conn.connectionId, out bool isNewRoom);
                networkMatch.matchId = roomId; // MatchInterestManagement가 이 값으로 가시성 판단
                session.roomId = roomId;       // 서버 내부 추적용

                // 시연용 오버레이에 방을 알려준다(컴포넌트가 없으면 조용히 건너뜀)
                if (conn.identity.TryGetComponent(out RoomIdDisplay roomDisplay))
                    roomDisplay.SetRoom(roomId);

                // matchId를 세팅한 '뒤에' 스폰을 알린다 — 순서가 반대면
                // 몬스터 스폰 시점에 플레이어가 아직 옛 match라 옵저버에서 빠진다.
                if (isNewRoom)
                    _roomRegistry.NotifyRoomOpened(sceneName, roomId);
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

                if (conn.identity.TryGetComponent(out RoomIdDisplay roomDisplay))
                    roomDisplay.SetRoom(Guid.Empty);

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

            TeleportToSafePosition(session, conn);

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
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
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
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = true });  // > GameNetworkManager.OnStartClient
                                                                                         // > _clientSyncService.OnAdditiveSceneMessage
                                                                                         // > LoadAdditiveScene
                                                                                         // > conn.Send(PortalSceneLoadedAckMessage)
            session.loadedScenes.Add(sceneName);
        }


        // [S-Loop-4-b] 씬 언로드 지시 — 메모리 절약
        public void PlayerLeftZone(NetworkConnectionToClient conn, string sceneName)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (!session.loadedScenes.Contains(sceneName))
                return;
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = false }); // > GameNetworkManager.OnStartClient
            session.loadedScenes.Remove(sceneName);
        }

        // [Trigger] 포탈 콜라이더 진입 — CheckZones 루프와 무관한 별도 진입점(ZonePortalTrigger.OnTriggerEnter에서 호출).
        // sourceZone은 이 트리거가 대표하는 "출발 쪽" 존 — 플레이어가 아직 그 존의 월드에 있을 때만 발동한다
        // (두 씬이 동시에 로드된 경계 순간, 반대쪽 트리거도 같이 발동하는 중복 케이스 방지).
        public void OnPortalTriggerEntered(NetworkConnectionToClient conn, ZonePortalSO portal, ZoneDefinitionSO sourceZone)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.isTransitioningPortal) return; // 이미 전환 진행 중 — 중복 트리거 무시
            if (Time.time < session.portalReentryCooldownUntil) return; // 방금 반대편에서 갓 도착 — 재진입 쿨다운 중

            string currentWorld = WorldOf(session.currentScene) ?? WorldOf(_manager.defaultSubScene);
            if (currentWorld != sourceZone.WorldId) return; // 이미 전환됨 — 반대쪽 트리거가 먼저 처리함

            var targetZone = portal.zoneA == sourceZone ? portal.zoneB : portal.zoneA;

            // 이 포탈을 지금 탄다 — 재진입 판정용 상태 기록
            session.lastUsedPortal = portal;
            session.hasLeftLastPortalBounds = false;

            BeginPortalTransitionAsync(conn, session, sourceZone, targetZone).Forget();
        }

        // [Trigger-a] 포탈 전환 시퀀스:
        // FadeOut → 씬스왑 요청 → 로드완료 ack 대기(타임아웃 가드) → 소속 전환 → 재스냅 → 이전 씬 언로드 → FadeIn.
        // (재스냅 : 씬을 옮긴 뒤 캐릭터의 실제 위치/이동 상태를 강제로 깔끔하게 다시 고정시키는 것을 말함)
        // 로드 완료는 기기/네트워크마다 편차가 커서 고정 타이머 대신 PortalSceneLoadedAckMessage로 확인한다
        private async UniTaskVoid BeginPortalTransitionAsync(
            NetworkConnectionToClient conn, PlayerSession session, ZoneDefinitionSO sourceZone, ZoneDefinitionSO targetZone)
        {
            session.isTransitioningPortal = true; // 포탈 페이드 전환 진행 중
            try
            {
                Vector3 rescuePos = conn.identity.transform.position; // 필드 / 던전 입구 좌표가 겹치도록 설계돼 있어 재스냅은 같은 좌표
                conn.Send(new ScreenFadeMessage { fadeOut = true, duration = _manager.portalFadeDuration }); // 씬 FadeOut  > GameNetworkManager.OnStartClient
                await UniTask.Delay(TimeSpan.FromSeconds(_manager.portalFadeDuration));
                session.confirmedPortalScene = null; // 이전 전환에서 남아있을 수 있는 낡은 ack 기록 리셋
                PlayerEnteredZone(conn, targetZone.sceneName); // 씬스왑 요청(AdditiveSceneMessage) — 소속(currentScene)은 아직 안 바뀜
                // 여기까지 되면 Send(PortalSceneLoadedAckMessage) 전송된 상태

                // 로드완료 ack 대기. portalAckTimeoutSeconds 초과 시 강제 진행(소프트락 방지 안전장치).
                var ackTask = UniTask.WaitUntil(() => session.confirmedPortalScene == targetZone.sceneName); // 대상 씬 로드완료 ack 대기 (소속 전환은 아직)
                var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(_manager.portalAckTimeoutSeconds));
                int winnerIndex = await UniTask.WhenAny(ackTask, timeoutTask);
                if (winnerIndex == 1) // timeoutTask가 먼저 끝남 = ack 못 받음
                    Debug.LogWarning($"[Portal] conn={conn.connectionId} — '{targetZone.sceneName}' 로드 ack 타임아웃 — 강제 진행");
                PlayerChangedScene(conn, targetZone.sceneName); // 소속 전환(MoveGameObjectToScene)

                // 1. 플레이어 재스냅
                // ServerRescueTo는 이름은 "낙하 구조"지만 실제로는 NetworkTransform 보간 버퍼 리셋 +
                // CharacterController 껐다 켜기라, 포탈 전환 직후 재스냅에도 그대로 재사용한다
                if (conn.identity.TryGetComponent(out MySecondWorld.Network.Player.NetworkPlayerController playerController))
                    playerController.ServerRescueTo(rescuePos);
                // 2. 컴패니언도 플레이어 바로 옆으로 동반 텔레포트
                if (session.companionIdentity != null && session.companionPresent)
                {
                    if (session.companionIdentity.TryGetComponent(out ICompanionServerState compState))
                    {
                        compState.Warp(rescuePos);
                        Debug.Log($"[Portal] 컴패니언 동반 텔레포트(Warp) 완료: {rescuePos}");
                    }
                }
                // 3. 낙하 구조(RecoverFromFall) 시에도 동일하게 컴패니언 동반 텔레포트 추가
                PlayerLeftZone(conn, sourceZone.sceneName); // 이전 씬 언로드

                conn.Send(new ScreenFadeMessage { fadeOut = false, duration = _manager.portalFadeDuration });  // 씬 FadeIn > GameNetworkManager.OnStartClient
                await UniTask.Delay(TimeSpan.FromSeconds(_manager.portalFadeDuration));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Portal] conn={conn.connectionId} 전환 중 예외 — {e}");
                if (NetworkServer.connections.ContainsKey(conn.connectionId)) // 접속 종료가 원인이면 여기 안 들어감
                    conn.Send(new ScreenFadeMessage { fadeOut = false, duration = _manager.portalFadeDuration }); // 검은 화면 방치 방지
            }
            finally
            {
                session.isTransitioningPortal = false; // 포탈 페이드 전환 진행 끝
                session.portalReentryCooldownUntil = Time.time + _manager.portalReentryCooldownSeconds; // 포탈 재진입 쿨다운 시작 시각 기록
            }
        }

        // [Trigger-b] 클라 씬 로드완료 ack 수신 — session.confirmedPortalScene에 기록해
        // BeginPortalTransitionAsync의 WaitUntil을 깨운다.
        public void OnPortalSceneLoadedAck(NetworkConnectionToClient conn, PortalSceneLoadedAckMessage msg)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var session)) return;
            session.confirmedPortalScene = msg.sceneName;
        }

        // 사망 리스폰 등, 낙하 타이머 게이트 없이 즉시 안전 위치로 보내야 할 때 사용
        // toTown이 true면 마을 스폰 지점으로
        public void RespawnPlayer(NetworkConnectionToClient conn, bool toTown = false)
        {
            if (!_sessionService.Sessions.TryGetValue(conn.connectionId, out var playerSession))
            {
                Debug.LogWarning($"[ServerZoneService] RespawnPlayer — conn={conn.connectionId} 세션을 찾을 수 없음");
                return;
            }
            TeleportToSafePosition(new KeyValuePair<int, PlayerSession>(conn.connectionId, playerSession), conn, toTown);
        }

        // 안전 위치 결정 + 씬 이동 + 텔레포트 + 컴패니언 동반. RecoverFromFall/RespawnPlayer 공용.
        // 목적지 씬이 클라에 없을 수 있어(던전 → 필드 탈출) 로드 완료를 기다려야 하므로 async다.
        private void TeleportToSafePosition(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, bool toTown = false)
            => TeleportToSafePositionAsync(session, conn, toTown).Forget();

        private async UniTaskVoid TeleportToSafePositionAsync(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, bool toTown = false)
        {
            // 안전 위치 결정
            Vector3 safePos;
            string safeScene;

            // toTown이 false이고 마지막 안전 위치가 있으면 제자리 부활
            if (!toTown && session.Value.lastSafePosition != Vector3.zero
                && !string.IsNullOrEmpty(session.Value.lastSafeScene))
            {
                safePos = session.Value.lastSafePosition;
                safeScene = session.Value.lastSafeScene;
            }
            else // 마을 부활(toTown=true)이거나 지면에 서 본 적이 없을 때 -> MainScene의 NetworkStartPosition 사용
            {
                safeScene = _manager.defaultSubScene;

                // MainScene에 배치된 NetworkStartPosition을 그대로 가져옴
                // GetStartPosition()은 조회처럼 보이지만 RoundRobin 인덱스를 전진시킨다(NetworkManager.cs:1123).
                // 구조는 스폰이 아니므로 신규 플레이어의 스폰 순번을 건드리면 안 된다 → 리스트를 직접 읽는다.
                // (파괴된 Transform이 남아 있을 수 있어 null이 아닌 것 중 첫 번째를 고른다)
                var startPos = NetworkManager.startPositions.FirstOrDefault(t => t != null);

                if (startPos != null)
                {
                    safePos = startPos.position;
                }
                else // 씬에 NetworkStartPosition이 하나도 없을 때의 비상용 대체
                {
                    // 여기서 원점(Vector3.zero)으로 보내면 필드 지면(Y≈15)보다 아래라
                    // 구조가 곧바로 재낙하가 된다 — 고치려던 버그를 구조 로직이 다시 만드는 셈.
                    // 필드 존 bounds의 중심은 정의상 필드 콘텐츠 안이므로 훨씬 안전한 최후 수단.
                    var fieldZone = _manager.zones.FirstOrDefault(z => z.sceneName == safeScene);
                    safePos = fieldZone != null ? fieldZone.bounds.center : Vector3.zero;
                    Debug.LogError(
                        $"[ServerZoneService] NetworkStartPosition이 없습니다 — 필드 bounds 중심({safePos})으로 대체");
                }
            }

            await MovePlayerToAsync(session, conn, safePos, safeScene);
        }

        /// <summary>
        /// 서버 권위 텔레포트의 실행부. 목적지 씬이 클라에 없으면 로드 완료를 기다린 뒤 옮긴다.
        /// 낙하 구조·사망 부활·탈출 버튼·NPC 순간이동이 전부 이 경로를 탄다 —
        /// 좌표만 던지는 지름길을 새로 만들면 "클라에 지형이 없어 허공에 떨어지는" 버그가 그대로 재현된다.
        /// </summary>
        private async UniTask MovePlayerToAsync(
            KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn,
            Vector3 targetPos, string targetScene)
        {
            // 씬 이동을 먼저 — 목적지 씬으로 옮긴 뒤에 위치를 확정해야 가시성 판정이 어긋나지 않는다
            if (session.Value.currentScene != targetScene)
            {
                // 클라에 목적지 씬이 아직 없으면(던전 → 필드 탈출 등) 로드를 먼저 시키고 완료를 기다린다.
                // 안 기다리면 서버가 소속만 바꾼 채 후속 스폰 메시지를 쏟아내고, 클라는 씬이 없어
                // "Spawn scene object not found"로 그 오브젝트들을 영구히 잃는다(Mirror는 재시도하지 않음).
                if (!session.Value.loadedScenes.Contains(targetScene))
                {
                    session.Value.isTransitioningPortal = true; // 대기 중 정기 루프가 또 구조하지 않도록 잠금
                    try
                    {
                        conn.Send(new ScreenFadeMessage { fadeOut = true, duration = _manager.portalFadeDuration });
                        session.Value.confirmedPortalScene = null;
                        PlayerEnteredZone(conn, targetScene); // 씬 로드 요청

                        var ackTask = UniTask.WaitUntil(() => session.Value.confirmedPortalScene == targetScene);
                        var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(_manager.portalAckTimeoutSeconds));
                        if (await UniTask.WhenAny(ackTask, timeoutTask) == 1)
                            Debug.LogWarning($"[Rescue] conn={conn.connectionId} — '{targetScene}' 로드 ack 타임아웃, 강제 진행");

                        conn.Send(new ScreenFadeMessage { fadeOut = false, duration = _manager.portalFadeDuration });
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Rescue] conn={conn.connectionId} 목적지 씬 로드 대기 중 예외 — {e}");
                    }
                    finally
                    {
                        session.Value.isTransitioningPortal = false;
                    }

                    // 대기 도중 접속이 끊겼을 수 있다
                    if (!NetworkServer.connections.ContainsKey(conn.connectionId) || conn.identity == null)
                        return;
                }

                // PlayerChangedScene 재사용
                // 직접 MoveGameObjectToScene만 하면 UpdateDungeonMatch(matchId 갱신)가 안 불려서
                // 포탈 ack 타임아웃 강제진행 이후 재낙하 시 matchId가 안 풀리는 문제가 있었음
                PlayerChangedScene(conn, targetScene);
            }

            // transform.position 직접 대입은 다음 클라 스냅샷에 덮어써진다 (권한이 클라에 있음).
            // 반드시 소유 클라까지 내려가는 경로로 보낼 것.
            if (conn.identity.TryGetComponent(out MySecondWorld.Network.Player.NetworkPlayerController npc))
                npc.ServerRescueTo(targetPos);

            // 컴패니언도 옆으로 동반 텔레포트
            if (session.Value.companionIdentity != null && session.Value.companionPresent)
            {
                if (session.Value.companionIdentity.TryGetComponent(out ICompanionServerState compState))
                {
                    compState.Warp(targetPos + (Vector3.right * 1.0f));
                }
            }
        }
    }
}