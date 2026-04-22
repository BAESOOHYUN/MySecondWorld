using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using MySecondWorld.Save;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 총괄 지휘관.서버+클라이언트 양쪽 로직
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        [Scene] public string defaultSubScene;           // FieldScene (마을) — 기본 스폰 존
        public ZoneDefinition[] zones;                   // 모든 존 정의 (SO 배열). defaultSubScene도 포함.
        public float zoneCheckInterval = 0.5f;           // 존 체크 주기 (초)

        Dictionary<int, PlayerSession> _sessions = new Dictionary<int, PlayerSession>(); // 플레이어별 세션
        private DungeonRoomRegistry _roomRegistry;       // 던전 룸 관리
        private bool _serverReady;                       // 서버 씬 프리로드 완료 여부
        private int _expectedSceneLoads;                 // 클라이언트: 로드해야 할 씬 수
        private int _completedSceneLoads;                // 클라이언트: 로드 완료된 씬 수

        #region UI단
        public void ToggleHost(bool isOn)
        {
            if (isOn) StartHost();
            else StopHost();
        }

        public void ToggleClient(bool isOn)
        {
            if (isOn) StartClient();
            else StopClient();
        }
        #endregion

        #region 서버 측

        // [S1] 서버 시작: 핸들러 등록 + 코루틴 시작
        public override void OnStartServer()
        {
            base.OnStartServer(); // Mirror가 onlineScene(MainScene)을 로드. 완료 후 → OnServerSceneChanged
            _roomRegistry = new DungeonRoomRegistry();
            // 클라이언트가 보내는 메시지 → NetworkServer.RegisterHandler (서버가 받으니까)
            NetworkServer.RegisterHandler<SceneLoadedAckMessage>(OnSceneLoadedAck);
            StartCoroutine(CheckZones());
        }

        // [S2] 모든 존 씬을 서버에만 additive로 프리로드한다.
        // MoveGameObjectToScene(캐릭터 스폰)의 대상 씬이 서버에 로드되어 있어야 하기 때문
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            StartCoroutine(LoadAllZoneScenes());
        }

        private IEnumerator LoadAllZoneScenes()
        {
            foreach (var zone in zones)
            {
                if (!SceneManager.GetSceneByPath(zone.sceneName).isLoaded)
                    yield return SceneManager.LoadSceneAsync(zone.sceneName, LoadSceneMode.Additive);

                // ★ additive 씬의 NetworkIdentity를 수동 Spawn
                Scene loadedScene = SceneManager.GetSceneByPath(zone.sceneName);
                foreach (GameObject rootObj in loadedScene.GetRootGameObjects())
                {
                    foreach (var netId in rootObj.GetComponentsInChildren<NetworkIdentity>())
                    {
                        // netId == 0 → 아직 Spawn 안 된 오브젝트 → Spawn 해줘야 함
                        // netId != 0 → 이미 Spawn 된 오브젝트 → 건드릴 필요 없음
                        if (netId.gameObject.activeInHierarchy && netId.netId == 0)
                            NetworkServer.Spawn(netId.gameObject);

                        // ★ 씬 오브젝트에 NetworkMatch가 있으면 해당 씬의 matchId 세팅
                        if (netId.TryGetComponent(out NetworkMatch match))
                            match.matchId = DungeonRoomRegistry.GetMatchIdFromScene(zone.sceneName);
                    }
                }
            }
            _serverReady = true;
        }

        /// <summary>
        /// [S3] 세션 생성 + PrepareAndSpawn 시작 (서버가 클라이언트에게 씬 준비하라고 시킴)
        /// Mirror 기본: base.OnServerAddPlayer(conn)에서 바로 캐릭터를 만들지만,
        /// 씬이 먼저 로드된 후에 스폰해야 하므로 base를 부르지 않는다.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 같은 playerId로 이미 세션 존재 시 → 기존 세션 정리 (재접속 처리)
            string playerId = conn.authenticationData as string;
            if (!string.IsNullOrEmpty(playerId))
            {
                var stale = _sessions.FirstOrDefault(s => s.Value.playerId == playerId);
                if (stale.Value != null)
                {
                    if (NetworkServer.connections.TryGetValue(stale.Key, out var oldConn))
                        oldConn.Disconnect();
                    _sessions.Remove(stale.Key);
                }
            }

            PlayerSession session = new PlayerSession
            {
                loadedScenes = new HashSet<string>(),
                playerId = conn.authenticationData as string
            };
            _sessions.Add(conn.connectionId, session);
            StartCoroutine(PrepareAndSpawn(conn));
        }

        // [S4] 클라이언트에게 씬 로드 지시 + 스폰 위치 저장
        public IEnumerator PrepareAndSpawn(NetworkConnectionToClient conn)
        {
            yield return new WaitUntil(() => _serverReady);

            var session = _sessions[conn.connectionId];
            var saveData = SaveManager.Instance?.Load(session.playerId);
            var hasSpawnData = saveData.HasValue;

            string spawnScene = defaultSubScene;
            Vector3 spawnPosition = GetStartPosition()?.position ?? Vector3.zero;

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

        /// <summary>
        /// [S5] ACK 수신 → 캐릭터 생성 + 씬 이동 (S1에서 등록한 핸들러)
        /// 클라이언트가 씬 로드 완료 ACK를 보내면 서버가 캐릭터를 스폰한다.
        /// </summary>
        public void OnSceneLoadedAck(NetworkConnectionToClient conn, SceneLoadedAckMessage msg)
        {
            var session = _sessions[conn.connectionId];

            // Instantiate 시점부터 올바른 위치에 생성해야 Spawn 메시지에 정확한 위치가 포함됨
            Vector3 spawnPos = (session.pendingSpawnPosition != Vector3.zero)
                ? session.pendingSpawnPosition
                : (GetStartPosition()?.position ?? Vector3.zero);

            // base.OnServerAddPlayer(conn) 대신 직접 인스턴스화 (위치 보정 위해)
            GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
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
                conn.identity.GetComponent<NetworkMatch>().matchId = DungeonRoomRegistry.GetMatchIdFromScene(session.currentScene);
            }
        }


        // [S-End] 세이브 + 세션 정리
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (_sessions.TryGetValue(conn.connectionId, out var session))
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
                _sessions.Remove(conn.connectionId);
            }
            base.OnServerDisconnect(conn);
        }
        #endregion


        #region 서버 측  Zone 관리

        // [S-Loop] zoneCheckInterval마다 위치 체크 (S5에서 isSpawned=true 된 플레이어만)
        public IEnumerator CheckZones()
        {
            while (true)
            {
                // ToList()로 스냅샷: 순회 도중 접속/퇴장으로 _sessions가 변경되면
                // InvalidOperationException이 발생해 코루틴이 죽고 씬 동기화가 멈춘다.
                foreach (var session in _sessions.ToList())
                {
                    if (!_sessions.ContainsKey(session.Key)) continue; // 순회 중 제거된 경우
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
                yield return new WaitForSeconds(zoneCheckInterval);
            }
        }

        /// <summary>
        /// [S-Loop-1] 소속 씬 판정 (정확한 bounds) + 낙하 타이머 가동
        /// </summary>
        public void ResolveCurrentZone(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, Vector3 playerPos)
        {
            bool inAnyZone = false;

            foreach (var zone in zones)
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
                session.Value.fallTimer += zoneCheckInterval;
        }

        /// <summary>
        /// [S-Loop-1-a] 소속 씬 변경 — MoveGameObjectToScene으로 플레이어 가시성 갱신
        /// </summary>
        public void PlayerChangedScene(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessions[conn.connectionId];
            if (session.currentScene == sceneName) return;

            // LeaveRoom에서 이전 씬이 필요하므로 MoveGameObjectToScene 전에 저장
            string prevScene = session.currentScene;

            SceneManager.MoveGameObjectToScene(
                conn.identity.gameObject,
                SceneManager.GetSceneByPath(sceneName));
            session.currentScene = sceneName;

            // 던전 씬 진입/퇴장 분기 (matchId 갱신)
            UpdateDungeonMatch(conn, session, prevScene, sceneName);
        }

        /// <summary>
        /// [S-Loop-1-b] 던전 진입/퇴장 시 matchId 갱신 (MatchInterestManagement가 matchId로 가시성 판단)
        /// </summary>
        private void UpdateDungeonMatch(NetworkConnectionToClient conn, PlayerSession session, string prevScene, string sceneName)
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
                Debug.Log($"[S-Loop-b] 던전 룸 배정: conn={conn.connectionId} roomId={roomId} scene={sceneName}");
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
                networkMatch.matchId = DungeonRoomRegistry.GetMatchIdFromScene(sceneName);
            }
        }


        /// <summary>
        /// [S-Loop-2] 낙하 복귀 텔레포트 (fallTimer >= 3초)
        /// </summary>
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
                    safePos = GetStartPosition()?.position ?? Vector3.zero;
                    safeScene = defaultSubScene;
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

        /// <summary>
        /// [S-Loop-3] 씬 로드/언로드 판정 (bounds + preloadMargin 확장 영역)
        /// </summary>
        public void SyncAdditiveScenes(KeyValuePair<int, PlayerSession> session, NetworkConnectionToClient conn, HashSet<string> needScenes, Vector3 playerPos)
        {
            // preloadMargin만큼 확장된 bounds 안이면 needScenes에 추가
            foreach (var zone in zones)
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

        /// <summary>
        /// [S-Loop-3-a] 씬 로드 지시 — 클라이언트가 지형을 볼 수 있게 함
        /// </summary>
        public void PlayerEnteredZone(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessions[conn.connectionId];
            if (session.loadedScenes.Contains(sceneName)) return;
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = true });
            session.loadedScenes.Add(sceneName);
        }


        /// <summary>
        /// [S-Loop-3-b] 씬 언로드 지시 — 메모리 절약
        /// </summary>
        public void PlayerLeftZone(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessions[conn.connectionId];
            if (!session.loadedScenes.Contains(sceneName)) return;
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = false });
            session.loadedScenes.Remove(sceneName);
        }
        #endregion


        #region 클라이언트 측

        // [C1] 클라이언트 시작: 핸들러 등록
        public override void OnStartClient()
        {
            base.OnStartClient();
            // 서버가 보내는 메시지 → NetworkClient.RegisterHandler (클라이언트가 받으니까)
            NetworkClient.RegisterHandler<AdditiveSceneMessage>(OnAdditiveSceneMessage);
            NetworkClient.RegisterHandler<SpawnReadyMessage>(OnSpawnReadyMessage);
        }

        // [C2] 서버의 씬 로드/언로드 명령 수신 → 코루틴 분기
        public void OnAdditiveSceneMessage(AdditiveSceneMessage msg)
        {
            if (msg.load)
                StartCoroutine(LoadAdditiveScene(msg.sceneName));
            else
                StartCoroutine(UnloadAdditiveScene(msg.sceneName));
        }

        // [C2-a] 씬 로드 + 완료 카운터 증가
        // TODO: 데디케이티드 서버 전환 시 변경 필요
        private IEnumerator LoadAdditiveScene(string sceneName)
        {
            // Host 방어: Host는 서버가 이미 프리로드했으므로 isLoaded = true → 스킵
            // 일반 클라이언트: 항상 isLoaded = false → 로드 실행
            if (!SceneManager.GetSceneByPath(sceneName).isLoaded)
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            _completedSceneLoads++; // ACK 판정용: 씬이 몇 개나 로드되었나?
            CheckAndSendSpawnAck();
        }

        // [C2-b] 씬 언로드
        private IEnumerator UnloadAdditiveScene(string sceneName)
        {
            // Host 모드: 같은 프로세스라서 언로드하면 서버 씬까지 날아감 → 스킵
            if (NetworkServer.active)
                yield break;

            if (SceneManager.GetSceneByPath(sceneName).isLoaded)
                yield return SceneManager.UnloadSceneAsync(sceneName);
        }

        // [C3] 서버가 보낸 기대 씬 수 저장
        public void OnSpawnReadyMessage(SpawnReadyMessage msg)
        {
            _expectedSceneLoads = msg.sceneCount;
            CheckAndSendSpawnAck();
        }

        // [C4] 조건 충족 시 ACK 전송 → S5로 이어짐
        public void CheckAndSendSpawnAck()
        {
            if (_expectedSceneLoads > 0 && _completedSceneLoads >= _expectedSceneLoads)
            {
                NetworkClient.Send(new SceneLoadedAckMessage());
                _expectedSceneLoads = 0;
                _completedSceneLoads = 0;
            }
        }
        #endregion
    }
}


/*
서버                                    클라이언트
────                                    ──────

[Host 버튼 클릭]
StartHost()
  │
  ├─ StartServer() 내부 실행
  │    ├─ Transport 리슨 시작
  │    └─ OnStartServer()  ← 내 코드
  │         ├─ 핸들러 등록
  │         └─ CheckZones 코루틴 시작
  │
  ├─ Mirror: onlineScene(MainScene) 로드
  │    │
  │    └─ OnServerSceneChanged()  ← 내 코드
  │         ├─ zones[] 전부 서버에 프리로드
  │         └─ _serverReady = true
  │
  ├─ Mirror: SpawnObjects()
  │    └─ 씬의 NetworkIdentity 활성화
  │
  └─ FinishStartHost()
       ├─ OnStartHost()
       └─ Host의 로컬 클라이언트 자동 연결
            │
            │ (Host는 서버+클라이언트이므로
            │  아래 과정이 내부적으로 즉시 발생)
            │
            ▼
OnServerConnect(conn)                   OnClientConnect()
  │                                       ├─ Ready()
  │                                       │     │
OnServerReady(conn)  ←────────────────────┘     │
  │                                       └─ AddPlayer()
  │                                              │
OnServerAddPlayer(conn)  ←───────────────────────┘
  │
  └─ 내 코드: PrepareAndSpawn(conn) 시작
       │
       ├─ AdditiveSceneMessage 전송 ──→  씬 로드 시작...
       ├─ SpawnReadyMessage 전송    ──→  _expected = 1
       │
       │                                 씬 로드 완료
       │                                 _completed >= _expected
       │   SceneLoadedAckMessage  ←────  "다 됐어!"
       │
  OnSceneLoadedAck(conn)
       ├─ base.OnServerAddPlayer()
       │    → playerPrefab 생성
       ├─ position 설정
       ├─ MoveGameObjectToScene()
       └─ session.IsSpawned = true

===============================================
이후 다른 클라이언트가 접속하면:
===============================================

                                        [Client 버튼 클릭]
                                        StartClient()
                                          ├─ Transport.Connect()
                                          └─ OnStartClient()
                                                │
Transport에서 연결 수락                     (연결 중...)
  │                                             │
  ▼                                             ▼
OnServerConnect(conn)               ←→  OnClientConnect()
  │                                       ├─ Ready()
  │                                       │     │
OnServerReady(conn)  ←────────────────────┘     │
  │                                       └─ AddPlayer()
  │                                              │
OnServerAddPlayer(conn)  ←───────────────────────┘
  │
  └─ PrepareAndSpawn(conn) 시작
       │
       (위와 동일한 ACK 흐름)
*/