using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

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
        private bool _serverReady;                       // 서버 씬 프리로드 완료 여부
        private int _expectedSceneLoads;                 // 클라이언트: 로드해야 할 씬 수
        private int _completedSceneLoads;                // 클라이언트: 로드 완료된 씬 수



        //─────────────────────────── 서버 측 ──────────────────────────

        // [S1] 서버 시작: 핸들러 등록 + 코루틴 시작
        public override void OnStartServer()
        {
            base.OnStartServer(); // Mirror가 onlineScene(MainScene)을 로드. 로드 완료후 → OnServerSceneChanged
            // 클라이언트가 보내는 메시지 → NetworkServer.RegisterHandler (서버가 받으니까)
            NetworkServer.RegisterHandler<SceneLoadedAckMessage>(OnSceneLoadedAck);
            StartCoroutine(CheckZones());
            Debug.Log("[S1] OnStartServer: 핸들러 등록 + CheckZones 코루틴 시작");
        }

        // [S2] 모든 존 씬을 서버에만 additive로 프리로드 한다.
        // MoveGameObjectToScene(캐릭터 스폰)의 대상 씬이 서버에 로드되어 있어야 하기 때문
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            StartCoroutine(LoadAllZoneScenes());
            Debug.Log($"[S2] OnServerSceneChanged: 코루틴 시작");
        }

        private IEnumerator LoadAllZoneScenes()
        {
            // 모든 존 씬 프리로드 (defaultSubScene 포함)
            foreach (var zone in zones)
            {
                if (!SceneManager.GetSceneByPath(zone.sceneName).isLoaded)
                    yield return SceneManager.LoadSceneAsync(zone.sceneName, LoadSceneMode.Additive);
            }
            _serverReady = true;
            Debug.Log($"[S2] OnServerSceneChanged: 모든 존 씬 프리로드 완료. _serverReady={_serverReady}");
        }
        /// <summary>
        /// ACK 대기 시작
        /// 이 메서드가 호출되는 시점
        /// 클라이언트: OnClientConnect()
        ///    └─ NetworkClient.AddPlayer()  ──→  서버: OnServerAddPlayer(conn)
        /// 기본 Mirror라면 여기서 base.OnServerAddPlayer(conn)을 호출해서 바로 캐릭터를 만들지만,
        /// 우리는 씬이 먼저 로드된 후에 스폰해야 하므로 base를 부르지 않는다.
        /// </summary>
        // [S3] 세션 생성 + PrepareAndSpawn 시작 (서버가 클라이언트에게 씬 준비하라고 시킴)
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            PlayerSession session = new PlayerSession
            {
                LoadedScenes = new HashSet<string>(),
            };
            _sessions.Add(conn.connectionId, session);
            StartCoroutine(PrepareAndSpawn(conn));
            Debug.Log($"[S3] OnServerAddPlayer: conn={conn.connectionId} 세션 생성, PrepareAndSpawn 시작");
        }

        // [S4] 클라이언트에게 씬 로드 지시 + 스폰 위치 저장
        public IEnumerator PrepareAndSpawn(NetworkConnectionToClient conn)
        {
            yield return new WaitUntil(() => _serverReady);

            conn.Send(new AdditiveSceneMessage { sceneName = defaultSubScene, load = true });
            conn.Send(new SpawnReadyMessage { sceneCount = 1 });

            Transform startPos = GetStartPosition();
            Vector3 spawnPosition = startPos != null ? startPos.position : Vector3.zero;

            var session = _sessions[conn.connectionId];
            session.PendingSpawnPosition = spawnPosition;
            session.PendingSpawnScene = defaultSubScene;
            Debug.Log($"[S4] PrepareAndSpawn: conn={conn.connectionId} 씬={defaultSubScene} 위치={spawnPosition}");
        }

        /// <summary>
        /// ACK 수신 → 스폰 + 씬 이동
        /// 클라이언트가 "(씬 준비)다 됐어!" ACK를 보내면 서버가 드디어 '캐릭터'를 만드는 메서드
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        // [S5] ACK 수신 → 캐릭터 생성 + 씬 이동 (S1에서 등록한 핸들러)
        public void OnSceneLoadedAck(NetworkConnectionToClient conn, SceneLoadedAckMessage msg)
        {
            Debug.Log($"[S5] OnSceneLoadedAck: conn={conn.connectionId} ACK 수신!");
            base.OnServerAddPlayer(conn); // 드디어 playerPrefab 생성
            var session = _sessions[conn.connectionId];
            conn.identity.transform.position = session.PendingSpawnPosition;
            session.CurrentScene = session.PendingSpawnScene;
            Debug.Log($"[S5] 스폰 위치={session.PendingSpawnPosition} 씬={session.CurrentScene}");
            SceneManager.MoveGameObjectToScene(conn.identity.gameObject, SceneManager.GetSceneByPath(session.CurrentScene));
            session.IsSpawned = true;
            Debug.Log($"[S5] MoveGameObjectToScene 완료. IsSpawned=true");
        }


        // [S-End] 세션 정리
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[S-End] OnServerDisconnect: conn={conn.connectionId}");
            _sessions.Remove(conn.connectionId);
            base.OnServerDisconnect(conn);
        }


        //─────────────────────── 서버 측  Zone 관리 ──────────────────────────

        /// <summary>
        /// 거리 기반 존 체크
        /// </summary>
        /// <returns></returns>
        // [S-Loop] 0.5초마다 위치 체크 (S5에서 IsSpawned=true 된 플레이어만)
        public IEnumerator CheckZones()
        {
            while (true)
            {
                foreach (var session in _sessions)
                {
                    // 아직 스폰 안 된 플레이어 스킵
                    if (!session.Value.IsSpawned) continue;
                    if (!NetworkServer.connections.ContainsKey(session.Key))
                    {
                        Debug.LogWarning($"[S-Loop] connections에 conn={session.Key} 없음!");
                        continue;
                    }

                    Debug.Log($"[S-Loop] 체크 중: conn={session.Key} pos={NetworkServer.connections[session.Key].identity.transform.position}");

                    var conn = NetworkServer.connections[session.Key];
                    var playerPos = conn.identity.transform.position;

                    // === 1. 소속 씬 판정 (정확한 bounds) ===
                    foreach (var zone in zones)
                    {
                        if (session.Key != 0)
                            Debug.Log($"[S-Loop] conn={session.Key} zone={zone.sceneName} contains={zone.bounds.Contains(playerPos)} pos={playerPos} center={zone.bounds.center} extents={zone.bounds.extents}");

                        if (zone.bounds.Contains(playerPos))
                        {
                            PlayerChangedScene(conn, zone.sceneName);
                            break;  // 한 플레이어는 하나의 존에만 소속
                        }
                    }

                    // === 2. 씬 로드/언로드 판정 (bounds + preloadMargin) ===
                    var needScenes = new HashSet<string>();  // 이번 프레임에 필요한 씬 목록
                    foreach (var zone in zones)
                    {
                        var expandedBounds = new Bounds(
                            zone.bounds.center,
                            zone.bounds.size + Vector3.one * zone.preloadMargin * 2);
                        if (expandedBounds.Contains(playerPos))
                            needScenes.Add(zone.sceneName);
                    }

                    // 새로 필요한 씬 로드
                    foreach (var scene in needScenes)
                    {
                        if (!session.Value.LoadedScenes.Contains(scene))
                            PlayerEnteredZone(conn, scene);
                    }

                    // 더 이상 필요 없는 씬 언로드
                    // 순회 중인 컬렉션을 수정하면 에러가 나므로, 제거 대상을 먼저 모아놓고 나중에 처리

                    // 제거 대상 모으기
                    var scenesToUnload = new List<string>();
                    foreach (var scene in session.Value.LoadedScenes)
                    {
                        if (!needScenes.Contains(scene))
                            scenesToUnload.Add(scene);
                    }
                    // 모은 후에 처리
                    foreach (var scene in scenesToUnload)
                    {
                        PlayerLeftZone(conn, scene);
                    }
                }
                yield return new WaitForSeconds(zoneCheckInterval);
            }
        }

        /// <summary>
        /// [S-Loop-a] 씬 로드 지시 (지형 보기)
        /// </summary>
        public void PlayerEnteredZone(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessions[conn.connectionId];
            if (session.LoadedScenes.Contains(sceneName)) return;
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = true });
            session.LoadedScenes.Add(sceneName);
            Debug.Log($"[S-Loop-a] PlayerEnteredZone: conn={conn.connectionId} 씬 로드 지시={sceneName}");
        }

        /// <summary>
        /// [S-Loop-b] 소속 변경 (플레이어 보기)
        /// </summary>
        public void PlayerChangedScene(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessions[conn.connectionId]; 
            
            Debug.Log($"[S-Loop-b] 진입: conn={conn.connectionId} current={session.CurrentScene} new={sceneName}");

            if (session.CurrentScene == sceneName) return;
            SceneManager.MoveGameObjectToScene(
                conn.identity.gameObject,
                SceneManager.GetSceneByPath(sceneName));
            session.CurrentScene = sceneName;
            Debug.Log($"[S-Loop-b] PlayerChangedScene: conn={conn.connectionId} → {sceneName}");
        }

        /// <summary>
        /// [S-Loop-c] 씬 언로드 지시 (메모리 절약)
        /// </summary>
        public void PlayerLeftZone(NetworkConnectionToClient conn, string sceneName)
        {
            var session = _sessions[conn.connectionId];
            if (!session.LoadedScenes.Contains(sceneName)) return;
            conn.Send(new AdditiveSceneMessage { sceneName = sceneName, load = false });
            session.LoadedScenes.Remove(sceneName);
            Debug.Log($"[S-Loop-c] PlayerLeftZone: conn={conn.connectionId} 씬 언로드 지시={sceneName}");
        }

        //──────────────────── 클라이언트 측 ────────────────────
        
        // [C1] 클라이언트 시작: 핸들러 등록
        public override void OnStartClient()
        {
            base.OnStartClient();
            // 서버가 보내는 메시지 → NetworkClient.RegisterHandler (클라이언트가 받으니까)
            NetworkClient.RegisterHandler<AdditiveSceneMessage>(OnAdditiveSceneMessage);
            NetworkClient.RegisterHandler<SpawnReadyMessage>(OnSpawnReadyMessage);
            Debug.Log("[C1] OnStartClient: 핸들러 등록 완료");
        }

        // [C2] 서버의 씬 로드/언로드 명령 수신 → 코루틴 분기
        public void OnAdditiveSceneMessage(AdditiveSceneMessage msg)
        {
            Debug.Log($"[C2] OnAdditiveSceneMessage: 씬={msg.sceneName} load={msg.load}");
            if (msg.load) // "이 씬을 로드 해라"는 명령을 받았을 때
                StartCoroutine(LoadAdditiveScene(msg.sceneName));
            else
                StartCoroutine(UnloadAdditiveScene(msg.sceneName));
        }

        // [C2-a] 실제 씬 로드 + 카운터 증가
        private IEnumerator LoadAdditiveScene(string sceneName)
        {
            // TODO : 데디케이티드 서버로 전환시 변경 필요
            // (현재) 로드 되지 않은 씬에 한해서만 수행 
            // Host모드 진입시 방어코드.
            // 일반 클라이언트: 씬이 항상 없으므로 isLoaded = false → !isLoaded가 항상 true → 항상 로드 실행
            //           Host: 서버가 이미 로드했으므로 isLoaded = true → !isLoaded가 false → 로드 스킵
            bool alreadyLoaded = SceneManager.GetSceneByPath(sceneName).isLoaded;
            Debug.Log($"[C2-a] LoadAdditiveScene: 씬={sceneName} 이미로드={alreadyLoaded}");

            if (!alreadyLoaded)
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            // 최초 접속 시 스폰 타이밍 맞추는 용
            _completedSceneLoads++; //ACK 판정용. 씬이 몇 개나 로드되었나?
            Debug.Log($"[C2-a] 씬 로드 완료: completed={_completedSceneLoads} expected={_expectedSceneLoads}");
            CheckAndSendSpawnAck();
        }

        // [C2-b] 씬 언로드
        private IEnumerator UnloadAdditiveScene(string sceneName)
        {
            // (현재) 로드 된 씬에 한해서만
            if (SceneManager.GetSceneByPath(sceneName).isLoaded)
                yield return SceneManager.UnloadSceneAsync(sceneName);
        }

        // [C3] 서버가 보낸 기대 씬 수 저장
        public void OnSpawnReadyMessage(SpawnReadyMessage msg)
        {
            _expectedSceneLoads = msg.sceneCount;
            Debug.Log($"[C3] OnSpawnReadyMessage: expected={_expectedSceneLoads}");
            CheckAndSendSpawnAck();
        }

        /// <summary>
        /// 클라이언트가 서버에 보내는 ACK. 이후 SceneLoadedAckMessage에 전송
        /// </summary>
        // [C4] 조건 충족 시 ACK 전송 → S5로 이어짐
        public void CheckAndSendSpawnAck()
        {
            Debug.Log($"[C4] CheckAndSendSpawnAck: completed={_completedSceneLoads} expected={_expectedSceneLoads}");
            if (_expectedSceneLoads > 0 && _completedSceneLoads >= _expectedSceneLoads)
            {
                Debug.Log("[C4] ★ ACK 전송! → S5로 이어짐");
                NetworkClient.Send(new SceneLoadedAckMessage());
                _expectedSceneLoads = 0;
                _completedSceneLoads = 0;
            }
        }
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