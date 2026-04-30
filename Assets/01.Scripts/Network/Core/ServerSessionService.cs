using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using MySecondWorld.Save;

namespace MySecondWorld.Network
{
    // 플레이어 세션 소유자. 접속/퇴장/스폰 흐름 담당.
    public class ServerSessionService
    {
        private readonly GameNetworkManager _manager;
        private readonly DungeonRoomRegistry _roomRegistry;
        public Dictionary<int, PlayerSession> Sessions { get; } = new();

        public ServerSessionService(GameNetworkManager manager, DungeonRoomRegistry roomRegistry)
        {
            _manager = manager;
            _roomRegistry = roomRegistry;
        }

        // [S3] 세션 생성 + PrepareAndSpawn 시작
        // Mirror 기본: base.OnServerAddPlayer(conn)에서 바로 캐릭터를 만들지만,
        // 씬이 먼저 로드된 후에 스폰해야 하므로 base를 부르지 않는다.
        public void AddPlayer(NetworkConnectionToClient conn)
        {
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
        public async UniTaskVoid PrepareAndSpawn(NetworkConnectionToClient conn)
        {
            await UniTask.WaitUntil(() => _manager.ServerReady, cancellationToken: _manager.Cts.Token);

            var session = Sessions[conn.connectionId];
            var saveData = SaveManager.Instance?.Load(session.playerId);
            var hasSpawnData = saveData.HasValue;

            string spawnScene = _manager.defaultSubScene;
            Vector3 spawnPosition = _manager.GetStartPosition()?.position ?? Vector3.zero;

            if (!string.IsNullOrEmpty(session.playerId) && hasSpawnData)
            {
                // 세이브 데이터가 있으면 → 저장된 씬 + 위치 사용
                spawnScene = saveData.Value.lastSceneNames[0];
                spawnPosition = saveData.Value.lastPosition;
            }

            session.pendingSpawnPosition = spawnPosition;
            session.pendingSpawnScene = spawnScene;
            conn.Send(new AdditiveSceneMessage { sceneName = spawnScene, load = true });
            conn.Send(new SpawnReadyMessage { sceneCount = 1 });
        }

        // [S5] ACK 수신 → 캐릭터 생성 + 씬 이동
        // 클라이언트가 씬 로드 완료 ACK를 보내면 서버가 캐릭터를 스폰한다. (S1에서 핸들러 등록)
        public void OnSceneLoadedAck(NetworkConnectionToClient conn, SceneLoadedAckMessage msg)
        {
            var session = Sessions[conn.connectionId];

            // Instantiate 시점부터 올바른 위치에 생성해야 Spawn 메시지에 정확한 위치가 포함됨
            Vector3 spawnPos = (session.pendingSpawnPosition != Vector3.zero)
                ? session.pendingSpawnPosition
                : (_manager.GetStartPosition()?.position ?? Vector3.zero);

            // base.OnServerAddPlayer(conn) 대신 직접 인스턴스화 (위치 보정 위해)
            GameObject player = UnityEngine.Object.Instantiate(_manager.playerPrefab, spawnPos, Quaternion.identity);
            NetworkServer.AddPlayerForConnection(conn, player);

            session.currentScene = session.pendingSpawnScene;
            SceneManager.MoveGameObjectToScene(conn.identity.gameObject, SceneManager.GetSceneByPath(session.currentScene));

            // 스폰 직후 lastSafePosition 초기화:
            // zone bounds 체크 실패해도 즉시 텔레포트되지 않도록 안전 위치를 스폰 위치로 세팅
            session.lastSafePosition = session.pendingSpawnPosition;
            session.fallTimer = 0f;
            session.isSpawned = true;

            // 첫 접속 시 씬이 던전인지 판별하여 roomId 배정
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
                        SaveManager.Instance?.Save(session.playerId, new SaveData
                        {
                            lastSceneNames = new string[] { session.currentScene },
                            lastPosition = posToSave
                        });
                    }
                }

                // 2. 던전 룸 퇴장 — 방 슬롯 반환해야 다른 플레이어가 입장 가능
                if (session.roomId != Guid.Empty)
                    _roomRegistry.LeaveRoom(session.currentScene, conn.connectionId);

                // 3. playerNames 정리 — 재접속 시 이름 중복 거부 방지
                if (conn.authenticationData is string username)
                    PlayerAuthenticator.playerNames.Remove(username);

                // 4. 세션 제거
                Sessions.Remove(conn.connectionId);
            }
        }
    }
}