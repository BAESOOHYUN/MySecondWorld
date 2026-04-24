using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Mirror;

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
            _sessionService = new ServerSessionService(this, _roomRegistry);
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

        // [S3] → SessionService.AddPlayer로 위임
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            _sessionService.AddPlayer(conn);
        }

        // [S-End] 세이브 + 세션 정리
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _sessionService.RemovePlayer(conn);
            base.OnServerDisconnect(conn);
        }
        #endregion

        #region 클라이언트 측

        // [C1] 클라이언트 시작: 서비스 생성 + 핸들러 등록
        // 서버가 보내는 메시지 → NetworkClient.RegisterHandler (클라이언트가 받으니까)
        public override void OnStartClient()
        {
            base.OnStartClient();
            _clientSyncService = new ClientSceneSyncService();
            NetworkClient.RegisterHandler<AdditiveSceneMessage>(_clientSyncService.OnAdditiveSceneMessage);
            NetworkClient.RegisterHandler<SpawnReadyMessage>(_clientSyncService.OnSpawnReadyMessage);
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