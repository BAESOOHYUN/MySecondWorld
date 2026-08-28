using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Cleverous.VaultInventory;
using MySecondWorld.Save;

namespace MySecondWorld.Network
{
    // 플레이어 세션 소유자. 접속/퇴장/스폰 흐름 담당.
    public class ServerSessionService
    {
        private readonly GameNetworkManager _manager;
        private readonly DungeonRoomRegistry _roomRegistry;
        private readonly SaveManager _saveManager;
        private readonly RootItem _startingGoldItem;
        private readonly int _startingGoldAmount;
        private readonly RootItem _testSwordItem;
        private readonly RootItem _testRingItem;
        private readonly RootItem _testBowItem;
        private readonly RootItem _testQuiverItem;
        private readonly RootItem _testBraceletItem;
        private readonly RootItem _testNecklaceItem;
        private readonly RootItem _testAxeItem;
        private readonly RootItem _testPolearmItem;

        public Dictionary<int, PlayerSession> Sessions { get; } = new();

        public ServerSessionService(
           GameNetworkManager manager,
           DungeonRoomRegistry roomRegistry,
           SaveManager saveManager,
           RootItem startingGoldItem,
           int startingGoldAmount,
           RootItem testSwordItem,
           RootItem testRingItem,
           RootItem testBowItem,
           RootItem testQuiverItem,
           RootItem testBraceletItem,
           RootItem testNecklaceItem,
           RootItem testAxeItem,
           RootItem testPolearmItem
           )
        {
            _manager = manager;
            _roomRegistry = roomRegistry;
            _saveManager = saveManager;
            _startingGoldItem = startingGoldItem;
            _startingGoldAmount = startingGoldAmount;
            _testSwordItem = testSwordItem;
            _testRingItem = testRingItem;
            _testBowItem = testBowItem;
            _testQuiverItem = testQuiverItem;
            _testBraceletItem = testBraceletItem;
            _testNecklaceItem = testNecklaceItem;
            _testAxeItem = testAxeItem;
            _testPolearmItem = testPolearmItem;
        }

        // [S3] 세션 생성 + PrepareAndSpawn 시작
        // Mirror 기본: base.OnServerAddPlayer(conn)에서 바로 캐릭터를 만들지만,
        // 씬이 먼저 로드된 후에 스폰해야 하므로 base를 부르지 않는다.
        public void AddPlayer(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Session] AddPlayer conn={conn.connectionId}");  // ← 추가
            // 같은 playerId로 이미 세션 존재 시 → 기존 세션 정리 (재접속 처리)
            string playerId = conn.authenticationData as string;
            if (!string.IsNullOrEmpty(playerId))
            {
                var stale = Sessions.FirstOrDefault(s => s.Value.playerId == playerId);
                if (stale.Value != null)
                {
                    if (NetworkServer.connections.TryGetValue(stale.Key, out var oldConn))
                        oldConn.Disconnect();
                    Sessions.Remove(stale.Key);
                }
            }

            PlayerSession session = new PlayerSession
            {
                loadedScenes = new HashSet<string>(),
                playerId = conn.authenticationData as string
            };

            Sessions.Add(conn.connectionId, session);
            PrepareAndSpawn(conn).Forget();
        }

        // [S4] 클라이언트에게 씬 로드 지시 + 스폰 위치 저장
        // 초기 접속은 번들/씬 로드는 PrepareAndSpawn이 담당
        public async UniTaskVoid PrepareAndSpawn(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Session] PrepareAndSpawn 시작 conn={conn.connectionId}");
            await UniTask.WaitUntil(() => _manager.ServerReady, cancellationToken: _manager.Cts.Token);
            Debug.Log($"[Session] ServerReady 통과");
            // 서버 준비 대기 중 이 클라이언트(conn)가 먼저 연결을 끊으면 RemovePlayer가 세션을 삭제
            // Sessions[conn.connectionId] 직접 접근 시 KeyNotFoundException 발생
            if (!Sessions.TryGetValue(conn.connectionId, out var session))
            {
                Debug.Log("[Session] 세션 없음 → return");
                return;
            }

            var saveData = _saveManager.Load(session.playerId);
            var hasSpawnData = saveData.HasValue;
            session.isNewPlayer = !hasSpawnData;

            string spawnScene = _manager.defaultSubScene;
            Vector3 spawnPosition;

            if (!string.IsNullOrEmpty(session.playerId) && hasSpawnData)
            {
                // 세이브 데이터가 있으면 → 저장된 씬 + 위치 사용
                spawnScene = saveData.Value.lastSceneNames[0];
                spawnPosition = saveData.Value.lastPosition;
                session.recruitedCompanionIds = saveData.Value.recruitedCompanionIds ?? Array.Empty<string>();
            }
            else
            {
                // 시작 지점은 신규 플레이어만 소비한다.
                // GetStartPosition()은 조회처럼 보이지만 RoundRobin 인덱스를 전진시키므로
                // (NetworkManager.cs:1123), 값을 쓰지도 않을 경로에서 부르면 스폰 순번이 헛돈다.
                var startPos = _manager.GetStartPosition();
                if (startPos == null)
                    Debug.LogError("[Session] NetworkStartPosition이 없다 — 원점에 스폰된다. 씬 배치 확인 필요.");
                spawnPosition = startPos != null ? startPos.position : Vector3.zero;
            }

            session.pendingSpawnPosition = spawnPosition;
            session.pendingSpawnScene = spawnScene;

            // spawnScene에 해당하는 번들 메시지도 전송
            var zone = _manager.zones.FirstOrDefault(z => z.sceneName == spawnScene);
            bool isLocalHost = NetworkServer.localConnection != null
                            && conn == NetworkServer.localConnection;

            if (!isLocalHost)
            {
                var labels = new List<string> { "character_parts" };

                if (zone != null && !string.IsNullOrEmpty(zone.ZoneAssetLabel?.labelString))
                    labels.Add(zone.ZoneAssetLabel.labelString);

                foreach (var label in labels)
                    session.sentBundleLabels.Add(label);

                conn.Send(new BundleRequiredMessage { labels = labels.ToArray() });

                // 번들 전부(캐릭터 파츠 + 존) 완료까지 대기 — 씬 로드/스폰은 이 아래에서만 진행됨
                await UniTask.WaitUntil(() =>
                    _manager.IsBundleReady(conn.connectionId) || !Sessions.ContainsKey(conn.connectionId),
                    cancellationToken: _manager.Cts.Token);
                // 세션이 없으면 return (연결 끊김)
                if (!Sessions.TryGetValue(conn.connectionId, out session))
                    return;
            }
            // 번들 완료 확인 후 씬 로드 지시
            conn.Send(new AdditiveSceneMessage { sceneName = spawnScene, load = true });
            // PlayerEnteredZone을 안 거치는 경로라 loadedScenes에 수동 등록해야 한다.
            // 빠뜨리면 첫 CheckZones 틱이 스폰 씬까지 "신규"로 판정해 중복 로드 지시를 보낸다.
            session.loadedScenes.Add(spawnScene);
            conn.Send(new SpawnReadyMessage { sceneCount = 1 });
        }

        // [S5] ACK 수신 → 캐릭터 생성 + 씬 이동
        // 클라이언트가 씬 로드 완료 ACK를 보내면 서버가 캐릭터를 스폰한다. (S1에서 핸들러 등록)
        public void OnSceneLoadedAck(NetworkConnectionToClient conn, SceneLoadedAckMessage msg)
        {
            Debug.Log($"[Session] OnSceneLoadedAck conn={conn.connectionId}");
            SpawnPlayer(conn);  // 씬 ACK 오면 항상 스폰 (번들은 이미 완료됨)
        }

        public void SpawnPlayer(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Session] SpawnPlayer 호출 conn={conn.connectionId}");
            var session = Sessions[conn.connectionId];

            // Instantiate 시점부터 올바른 위치에 생성해야 Spawn 메시지에 정확한 위치가 포함됨.
            // 위치는 PrepareAndSpawn에서 이미 확정됐으므로 여기서 다시 뽑지 않는다 —
            // GetStartPosition()을 한 번 더 부르면 RoundRobin 순번이 접속 1회당 두 칸씩 밀린다.
            Vector3 spawnPos = session.pendingSpawnPosition;

            // base.OnServerAddPlayer(conn) 대신 직접 인스턴스화 (위치 보정 위해)
            GameObject player = MySecondWorld.Network.Player.PlayerFactory.Create(_manager.playerPrefab, spawnPos, Quaternion.identity);
            if (player == null)
                return;
            NetworkServer.AddPlayerForConnection(conn, player);

            // After
            // 신규 플레이어에게만 시작 골드 지급 (재접속 시 중복 지급 방지)
            if (session.isNewPlayer)
                GrantStartingGold(conn);

            session.currentScene = session.pendingSpawnScene;
            SceneManager.MoveGameObjectToScene(conn.identity.gameObject, _manager.GetServerScene(session.currentScene));

            // 스폰 직후 lastSafePosition 초기화:
            // zone bounds 체크 실패해도 즉시 텔레포트되지 않도록 안전 위치를 스폰 위치로 세팅
            session.lastSafePosition = session.pendingSpawnPosition;
            session.lastSafeScene = session.currentScene;
            session.fallTimer = 0f;
            session.isSpawned = true;

            // 첫 접속 시 씬이 던전인지 판별하여 matchId ← roomId 배정
            if (_roomRegistry.IsDungeonScene(session.currentScene))
            {
                var roomId = _roomRegistry.AssignRoom(session.currentScene, conn.connectionId);
                conn.identity.GetComponent<NetworkMatch>().matchId = roomId;
                session.roomId = roomId;
            }
            else
            {
                // session.roomId는 Guid.Empty 그대로 — 던전 방이 아니니까
                conn.identity.GetComponent<NetworkMatch>().matchId = SceneMatchId.GetMatchIdFromScene(session.currentScene);
            }

            // 이미 영입된 companion 자동 재소환 (MVP("Minimum Viable Product"(최소 기능 제품)): 1마리만)
            if (session.recruitedCompanionIds != null && session.recruitedCompanionIds.Length > 0)
            {
                var companionId = session.recruitedCompanionIds[0];
                var prefab = _manager.GetCompanionPrefab(companionId);
                if (prefab != null)
                    SpawnCompanionFor(session, conn, prefab);
                else
                    Debug.LogWarning($"[Session] companionId '{companionId}'에 매칭되는 프리팹이 레지스트리에 없음");
            }
        }

        // [S-End] 세이브 + 세션 정리 (OnServerDisconnect에서 호출)
        public void RemovePlayer(NetworkConnectionToClient conn)
        {
            if (Sessions.TryGetValue(conn.connectionId, out var session))
            {
                // 1. 세이브 (스폰된 적 있고 playerId가 있을 때만)
                if (session.isSpawned && !string.IsNullOrEmpty(session.playerId))
                {
                    // lastSafePosition 우선, 없으면 현재 위치 사용
                    var posToSave = session.lastSafePosition != Vector3.zero
                        ? session.lastSafePosition
                        : conn.identity?.transform.position ?? Vector3.zero;

                    if (posToSave != Vector3.zero)
                    {
                        _saveManager.Save(session.playerId, new SaveData
                        {
                            lastSceneNames = new string[] { session.currentScene },
                            lastPosition = posToSave,
                            recruitedCompanionIds = session.recruitedCompanionIds
                        });
                    }
                }

                if (session.companionIdentity != null)
                    NetworkServer.Destroy(session.companionIdentity.gameObject);

                // 2. 던전 룸 퇴장 — 방 슬롯 반환해야 다른 플레이어가 입장 가능
                if (session.roomId != Guid.Empty)
                    _roomRegistry.LeaveRoom(session.currentScene, conn.connectionId);

                // 3. playerNames 정리 — 재접속 시 이름 중복 거부 방지
                if (conn.authenticationData is string username)
                    PlayerAuthenticator.playerNames.Remove(username);

                // 4. 세션 제거
                session.sentBundleLabels.Clear();
                Sessions.Remove(conn.connectionId);
            }
        }


        #region 헬퍼

        private void SpawnCompanionFor(PlayerSession session, NetworkConnectionToClient conn, GameObject prefab)
        {
            var companion = MySecondWorld.Network.Companion.CompanionFactory.Create(
                prefab, conn.identity.transform.position, Quaternion.identity, conn.identity.transform);
            if (companion == null) return;

            // 최초 스폰 시 씬 위치 동일하게 맞춤
            SceneManager.MoveGameObjectToScene(companion, conn.identity.gameObject.scene);

            session.companionIdentity = companion.GetComponent<NetworkIdentity>();
            if (companion.TryGetComponent(out NetworkMatch companionMatch))
                companionMatch.matchId = conn.identity.GetComponent<NetworkMatch>().matchId;
        }

        // 퀘스트/이벤트 시스템이 나중에 호출할 진입점 — "누가 언제 부르는지"는 이 메서드 책임 밖
        public void RecruitCompanion(NetworkConnectionToClient conn, GameObject speciesPrefab, string companionId)
        {
            if (!Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity != null) return; // MVP: 이미 있으면 무시
            
            SpawnCompanionFor(session, conn, speciesPrefab);
            if (session.companionIdentity == null) return; // 스폰 실패

            // recruitedCompanionIds는 "이 플레이어가 지금까지 영입한 companion들의 ID 목록"
            var ids = session.recruitedCompanionIds ?? Array.Empty<string>();
            if (System.Array.IndexOf(ids, companionId) < 0) // < 0은 "못 찾았다 = 아직 이 companionId가 목록에 없다"는 뜻
            {
                // (없을 때만) 배열을 하나 더 큰 걸로 새로 만듦
                var newIds = new string[ids.Length + 1];
                // 옛날 배열(ids)에 있던 값들을 새 배열(newIds)의 0번 자리부터 그대로 복사
                // 예를 들어 ids가 ["Mowe"]였으면, newIds는 지금 ["Mowe", null](맨 뒤 한 칸은 아직 빈 자리) 상태
                ids.CopyTo(newIds, 0);
                // 새 값을 맨 끝자리에 넣기
                newIds[ids.Length] = companionId;
                //  세션에 반영
                session.recruitedCompanionIds = newIds;
            }
        }

        // 시작 골드 지급. 스폰 경로와 디버그 버튼이 같은 코드를 쓰도록 분리.
        // "신규 플레이어인가" 판단은 호출부 책임 — 디버그 경로는 그 조건을 일부러 건너뛴다.
        public void GrantStartingGold(NetworkConnectionToClient conn)
        {
            if (_startingGoldItem == null)
            {
                Debug.LogWarning("[Session] GameNetworkManager의 Starting Gold Item이 비어 있어 지급을 건너뜀");
                return;
            }
            if (conn.identity == null) return;

            var inventory = conn.identity.GetComponent<Cleverous.VaultInventory.Inventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[Session] conn={conn.connectionId}의 플레이어에 Inventory가 없음");
                return;
            }

            inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(_startingGoldItem, _startingGoldAmount));
            Debug.Log($"[Session] conn={conn.connectionId}에게 골드 {_startingGoldAmount} 지급");
        }

        // 무기 애니메이션 전환 테스트용 — Sword/Ring/Bow 1개씩 지급. 실제 인게임 획득 경로
        // (상점/드랍)가 생기면 이 메서드는 삭제한다.
        public void GrantWeaponTestKit(NetworkConnectionToClient conn)
        {
            if (conn.identity == null) return;

            var inventory = conn.identity.GetComponent<Cleverous.VaultInventory.Inventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[Session] conn={conn.connectionId}의 플레이어에 Inventory가 없음");
                return;
            }

            foreach (var item in new[] {  _testSwordItem, _testRingItem, _testBowItem,
                _testQuiverItem, _testBraceletItem, _testNecklaceItem, _testAxeItem, _testPolearmItem})
            {
                if (item == null) continue;
                inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(item, 1));
            }
            Debug.Log($"[Session] conn={conn.connectionId}에게 무기 테스트 킷 지급");
        }

        #endregion
    }
}