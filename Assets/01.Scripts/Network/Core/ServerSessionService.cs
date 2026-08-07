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
        public Dictionary<int, PlayerSession> Sessions { get; } = new();

        public ServerSessionService(
            GameNetworkManager manager,
            DungeonRoomRegistry roomRegistry,
            SaveManager saveManager,
            RootItem startingGoldItem,
            int startingGoldAmount)
        {
            _manager = manager;
            _roomRegistry = roomRegistry;
            _saveManager = saveManager;
            _startingGoldItem = startingGoldItem;
            _startingGoldAmount = startingGoldAmount;
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
            Vector3 spawnPosition = _manager.GetStartPosition()?.position ?? Vector3.zero;

            if (!string.IsNullOrEmpty(session.playerId) && hasSpawnData)
            {
                // 세이브 데이터가 있으면 → 저장된 씬 + 위치 사용
                spawnScene = saveData.Value.lastSceneNames[0];
                spawnPosition = saveData.Value.lastPosition;
                session.recruitedCompanionIds = saveData.Value.recruitedCompanionIds ?? Array.Empty<string>();
            }

            session.pendingSpawnPosition = spawnPosition;
            session.pendingSpawnScene = spawnScene;

            // spawnScene에 해당하는 번들 메시지도 전송
            var zone = _manager.zones.FirstOrDefault(z => z.sceneName == spawnScene);
            bool isLocalHost = NetworkServer.localConnection != null
                            && conn == NetworkServer.localConnection;

            if (!isLocalHost 
                && zone != null 
                && !string.IsNullOrEmpty(zone.ZoneAssetLabel?.labelString))
            {
                var label = zone.ZoneAssetLabel.labelString;
                session.sentBundleLabels.Add(label);
                conn.Send(new BundleRequiredMessage { zoneLabel = label });
                // 번들 완료까지 대기
                await UniTask.WaitUntil(() =>
                    _manager.IsBundleReady(conn.connectionId) || !Sessions.ContainsKey(conn.connectionId),
                    cancellationToken: _manager.Cts.Token);
                // 세션이 없으면 return (연결 끊김)
                if (!Sessions.TryGetValue(conn.connectionId, out session))
                    return;
            }
            // 번들 완료 확인 후 씬 로드 지시
            conn.Send(new AdditiveSceneMessage { sceneName = spawnScene, load = true });
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

            // Instantiate 시점부터 올바른 위치에 생성해야 Spawn 메시지에 정확한 위치가 포함됨
            Vector3 spawnPos = (session.pendingSpawnPosition != Vector3.zero)
                ? session.pendingSpawnPosition
                : (_manager.GetStartPosition()?.position ?? Vector3.zero);

            // base.OnServerAddPlayer(conn) 대신 직접 인스턴스화 (위치 보정 위해)
            GameObject player = MySecondWorld.Network.Player.PlayerFactory.Create(_manager.playerPrefab, spawnPos, Quaternion.identity);
            if (player == null)
                return;
            NetworkServer.AddPlayerForConnection(conn, player);

            // 신규 플레이어에게만 시작 골드 지급 (재접속 시 중복 지급 방지)
            if (session.isNewPlayer && _startingGoldItem != null)
            {
                var inventory = player.GetComponent<Cleverous.VaultInventory.Inventory>();
                inventory?.DoAdd(new Cleverous.VaultInventory.RootItemStack(_startingGoldItem, _startingGoldAmount));
            }

            session.currentScene = session.pendingSpawnScene;
            SceneManager.MoveGameObjectToScene(conn.identity.gameObject, _manager.GetServerScene(session.currentScene));

            // 스폰 직후 lastSafePosition 초기화:
            // zone bounds 체크 실패해도 즉시 텔레포트되지 않도록 안전 위치를 스폰 위치로 세팅
            session.lastSafePosition = session.pendingSpawnPosition;
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

        #endregion
    }
}