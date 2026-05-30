using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Mirror;
using VContainer;
using MySecondWorld.Save;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MySecondWorld.Network
{
    // Mirror 진입점. 콜백 수신 후 각 서비스에 위임한다.
    public class GameNetworkManager : NetworkManager
    {
        [Scene] public string defaultSubScene;           // FieldScene (마을) — 기본 스폰 존
        public ZoneDefinition[] zones;                   // 모든 존 정의 (SO 배열). defaultSubScene도 포함.
        public float zoneCheckInterval = 0.5f;           // 존 체크 주기 (초)
        public bool ServerReady { get; private set; }    // 서버 씬 프리로드 완료 여부
        public CancellationTokenSource Cts { get; private set; } 

        private DungeonRoomRegistry _roomRegistry;
        private ServerSessionService _sessionService;
        private ServerZoneService _zoneService;
        private ClientSceneSyncService _clientSyncService;
        private SaveManager _saveManager;
        [Inject]
        private void Construct(SaveManager saveManager)
        {
            _saveManager = saveManager;
        }

#if UNITY_EDITOR
        // 클라이언트만 도는 케이스에서도 ExitingPlayMode 정리가 보장되도록 Awake에서 등록.
        public override void Awake()
        {
            base.Awake();
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
        }

        public override void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            base.OnDestroy();
        }
#endif

#if UNITY_EDITOR
        private void OnEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;

            // "자기 자신 안에서 자신을 제거" = one-shot listener 패턴.
            // ExitingPlayMode 때 딱 한 번만 실행하고 자동으로 구독 해제하겠다는 의도
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;

            // offlineScene이 설정된 경우 StopServer() 내부 ServerChangeScene()이 모바일 클라이언트에
            // 씬 변경 메시지를 보내고 ACK를 기다리며 hang한다. 에디터 종료 시 씬 전환은 불필요하다.
            // offlineScene을 비워두면 StopHost() 내부에서 씬 전환을 시도하지 않아서 hang이 발생하지 않는다.
            // hang(프로그램이 멈춰서 아무것도 안 하는 상태 = 먹통) 방지용
            offlineScene = ""; // // ← 씬 전환 자체를 막아버림

            if (NetworkServer.active)
            {
                StopHost();
            }
            else if (NetworkClient.isConnected || NetworkClient.active)
            {
                // 클라이언트 전용 종료 — KCP UDP 소켓 명시적 close
                StopClient();
                NetworkClient.Shutdown();
            }
        }
#endif

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
            Cts = new CancellationTokenSource();
            _roomRegistry = new DungeonRoomRegistry();
            _sessionService = new ServerSessionService(this, _roomRegistry, _saveManager);
            _zoneService = new ServerZoneService(this, _sessionService, _roomRegistry);
            
            // 클라이언트가 보내는 메시지 → NetworkServer.RegisterHandler (서버가 받으니까)
            NetworkServer.RegisterHandler<SceneLoadedAckMessage>(_sessionService.OnSceneLoadedAck);
            _zoneService.CheckZones(Cts.Token).Forget();
        }

        public override void OnStopServer()
        {
            Cts?.Cancel();
            Cts?.Dispose();
        }

        // [S2] 모든 존 씬을 서버에만 additive로 프리로드한다.
        // MoveGameObjectToScene(캐릭터 스폰)의 대상 씬이 서버에 로드되어 있어야 하기 때문
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            LoadAllZoneScenes().Forget();
        }

        private async UniTaskVoid LoadAllZoneScenes()
        {
            foreach (var zone in zones)
            {
                if (!SceneManager.GetSceneByPath(zone.sceneName).isLoaded)
                    await SceneManager.LoadSceneAsync(zone.sceneName, LoadSceneMode.Additive).ToUniTask();

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
                            match.matchId = SceneMatchId.GetMatchIdFromScene(zone.sceneName);
                    }
                }
            }
            ServerReady = true;
        }

        // [S3] → ServerSessionService.AddPlayer로 위임
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            _sessionService.AddPlayer(conn);
        }

        // [S-End] 세이브 + 세션 정리
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _sessionService?.RemovePlayer(conn);
            base.OnServerDisconnect(conn);
        }
        #endregion

        #region 클라이언트 측

        public event System.Action OnClientStopped;

        // [C1] 클라이언트 시작: 서비스 생성 + 핸들러 등록
        // 서버가 보내는 메시지 → NetworkClient.RegisterHandler (클라이언트가 받으니까)
        // -> ClientSceneSyncService.cs 로 이어짐
        public override void OnStartClient()
        {
            base.OnStartClient();
            _clientSyncService = new ClientSceneSyncService();
            NetworkClient.RegisterHandler<AdditiveSceneMessage>(_clientSyncService.OnAdditiveSceneMessage);
            NetworkClient.RegisterHandler<SpawnReadyMessage>(_clientSyncService.OnSpawnReadyMessage);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("[Client] OnClientConnect — 연결 성공, 인증 대기");
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.LogWarning("[Client] OnClientDisconnect — 연결 끊김 또는 타임아웃");
            OnClientStopped?.Invoke();
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
  │         └─ ServerReady = true
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