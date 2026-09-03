using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Mirror;
using MessagePipe;
using VContainer;
using Cleverous.VaultInventory;
using MySecondWorld.Save;
using MySecondWorld.Bundle;
using MySecondWorld.Network.Companion;
using MySecondWorld.Customization;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MySecondWorld.Network
{
    // Mirror 진입점. 콜백 수신 후 각 서비스에 위임한다.
    public class GameNetworkManager : NetworkManager, IGameNetworkManager
    {
        #region 인스펙터 설정
        [Header("존 / 포탈")]
        [Scene] public string defaultSubScene;           // FieldScene (마을) — 기본 스폰 존
        public ZoneDefinitionSO[] zones;                 // 모든 존 정의 (SO 배열). defaultSubScene도 포함.
        public ZonePortalSO[] portals;                   // 월드 간 전환 지점
        public float zoneCheckInterval = 0.5f;           // 존 체크 주기 (초)
        public LayerMask groundLayer;                    // 낙하 안전망 레이캐스트용 지면 레이어

        [Tooltip("이 Y 아래로 떨어지면 존 판정과 무관하게 마지막 안전 위치로 구조한다")]
        public float fallRescueY = -50f;

        [Header("포탈 전환 페이드")]
        [SerializeField] private ScreenFadeUI _screenFadeUI;
        public ScreenFadeUI ScreenFadeUI => _screenFadeUI;
        public float portalFadeDuration = 0.3f;          // FadeOut/FadeIn 각각의 지속시간
        public float portalAckTimeoutSeconds = 15f;      // 로드완료 ack 대기 타임아웃(소프트락 방지 안전장치)
        public float portalReentryCooldownSeconds = 2f;  // 도착 직후 반대쪽 트리거 무시 시간(즉시 왕복 방지)
        public float portalCheckInterval = 0.1f;         // 포탈 감지 폴링 주기(zoneCheckInterval보다 훨씬 짧게 — 빠르게 지나가도 안 놓치도록)

        [Header("신규 플레이어 시작 지급")]
        [SerializeField] private RootItem _startingGoldItem;
        [SerializeField] private int _startingGoldAmount = 100;
        [SerializeField] private RootItem _testSwordItem;
        [SerializeField] private RootItem _testRingItem;
        [SerializeField] private RootItem _testBowItem;
        [SerializeField] private RootItem _testQuiverItem;
        [SerializeField] private RootItem _testBraceletItem;
        [SerializeField] private RootItem _testNecklaceItem;
        [SerializeField] private RootItem _testAxeItem;
        [SerializeField] private RootItem _testPolearmItem;
        [SerializeField] private AvatarPartKeyTableSO _avatarPartKeyTable;

        [Header("동행 NPC")]
        [SerializeField] private CompanionAdminService.CompanionEntry[] _companionRegistry;
        #endregion

        #region 런타임 상태 및 서비스 프로퍼티
        public bool ServerReady { get; private set; }    // 서버 씬 프리로드 완료 여부
        public CancellationTokenSource Cts { get; private set; }

        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _serverSceneHandles = new();
        private readonly HashSet<int> _bundleReadyConnIds = new(); // 번들 준비 완료된 connectionId 목록
        public bool IsBundleReady(int connId) => _bundleReadyConnIds.Contains(connId);

        private DungeonRoomRegistry _roomRegistry;
        private ServerSessionService _sessionService;
        private CompanionAdminService _companionService;
        private ServerZoneService _zoneService;
        private ClientSceneSyncService _clientSyncService;
        private ClientBundleSyncService _clientBundleService;
        private AddressableLabelLoader _zoneLoader;
        private SaveManager _saveManager;
        private IUIManager _uiManager;
        private ISubscriber<AddressablesProgressEvent> _progressSub;
        private IPublisher<AddressablesProgressEvent> _progressPub;

        public CompanionAdminService CompanionService => _companionService;
        public ServerZoneService ZoneService => _zoneService;
        public ServerSessionService SessionService => _sessionService;

        public event System.Action OnClientStopped;

        public string NetworkAddress
        {
            get => networkAddress;
            set => networkAddress = value;
        }
        #endregion

        #region 의존성 주입 (VContainer)
        [Inject]
        private void Construct(
            AddressableLabelLoader zoneLoader,
            SaveManager saveManager,
            ISubscriber<AddressablesProgressEvent> progressSub,
            IPublisher<AddressablesProgressEvent> progressPub,
            AvatarPartKeyTableSO avatarPartKeyTable = null,
            IUIManager uiManager = null
            )
        {
            _zoneLoader = zoneLoader;
            _saveManager = saveManager;
            _progressSub = progressSub;
            _progressPub = progressPub;
            if (avatarPartKeyTable != null) 
                _avatarPartKeyTable = avatarPartKeyTable;
            _uiManager = uiManager;
        }
        #endregion

        #region 유니티 생명주기
        // 클라이언트만 도는 케이스에서도 ExitingPlayMode 정리가 보장되도록 Awake에서 등록.
        public override void Awake()
        {
            base.Awake();
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
#endif
        }

        public override void Start()
        {
            base.Start();
#if UNITY_SERVER && !UNITY_EDITOR
            Debug.Log("[Server] 헤드리스 모드 감지 (UNITY_SERVER) -> StartServer() 자동 실행");
            StartServer();
#endif
        }

        public override void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
#endif
            base.OnDestroy();
        }

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
            offlineScene = ""; // ← 씬 전환 자체를 막아버림

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
        #endregion

        #region 서버 측 로직
        // [S1] 서버 시작: 핸들러 등록 + 코루틴 시작
        public override void OnStartServer()
        {
            Debug.Log("[Server] OnStartServer");
            base.OnStartServer(); // Mirror가 onlineScene(MainScene)을 로드. 완료 후 → OnServerSceneChanged
            Cts = new CancellationTokenSource();
            _roomRegistry = new DungeonRoomRegistry();
            _sessionService = new ServerSessionService(
                this, _roomRegistry, _saveManager, _startingGoldItem, _startingGoldAmount,
                _testSwordItem, _testRingItem, _testBowItem,
                _testQuiverItem, _testBraceletItem, _testNecklaceItem, _testAxeItem, _testPolearmItem,
                _avatarPartKeyTable);
            _companionService = new CompanionAdminService(_sessionService, _companionRegistry);
            _zoneService = new ServerZoneService(this, _sessionService, _roomRegistry);

            // 클라이언트가 보내는 메시지 → NetworkServer.RegisterHandler (서버가 받으니까)
            NetworkServer.RegisterHandler<BundleReadyMessage>(OnBundleReadyMessage);
            NetworkServer.RegisterHandler<SceneLoadedAckMessage>(_sessionService.OnSceneLoadedAck);
            NetworkServer.RegisterHandler<PortalSceneLoadedAckMessage>(_zoneService.OnPortalSceneLoadedAck);
            _zoneService.CheckZones(Cts.Token).Forget();
            _zoneService.CheckPortals(Cts.Token).Forget();

            ValidatePortals(); // 포탈 SO 설정 실수(zoneA/zoneB 누락 등) 조기 검증
        }

        // [S1-a] 포탈/월드 설정 오타 조기 검증
        // 서버 시작 시 1회 — worldId 오타/포탈 설정 실수를 조기에 잡아낸다
        private void ValidatePortals()
        {
            foreach (var group in zones.GroupBy(z => z.WorldId))
                Debug.Log($"[Portal] World '{group.Key}' — 존 {group.Count()}개");

            foreach (var portal in portals)
            {
                if (portal.zoneA == null || portal.zoneB == null)
                {
                    Debug.LogError($"[Portal] {portal.name} — zoneA/zoneB 중 비어있는 게 있음", portal);
                    continue;
                }

                if (portal.zoneA.WorldId == portal.zoneB.WorldId)
                    Debug.LogError($"[Portal] {portal.name} — zoneA/zoneB가 같은 World({portal.zoneA.WorldId})임, 포탈 의미 없음", portal);
            }
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
            Debug.Log($"[Server] OnServerSceneChanged: {sceneName}");
            base.OnServerSceneChanged(sceneName);
            LoadAllZoneScenes().Forget();
        }

        private async UniTaskVoid LoadAllZoneScenes()
        {
            foreach (var zone in zones)
            {
                // 이미 로드된 씬은 스킵 (OnServerSceneChanged 재호출 대비)
                if (!_serverSceneHandles.ContainsKey(zone.sceneName))
                {
                    // Addressables 초기화 완료 대기 (이미 됐으면 즉시 반환)
                    await Addressables.InitializeAsync().ToUniTask();

                    var handle = Addressables.LoadSceneAsync(zone.sceneName, LoadSceneMode.Additive);
                    _serverSceneHandles[zone.sceneName] = handle;
                    await handle.ToUniTask();

                    // ★ await 이후 서버가 여전히 활성 상태인지 재확인
                    if (!NetworkServer.active) return;

                    // ★ additive 씬의 NetworkIdentity를 수동 Spawn
                    // Addressables 번들 씬은 GetSceneByPath로 못 찾을 수 있음 → handle.Result.Scene으로 직접 접근
                    Scene loadedScene = handle.Result.Scene;
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
            }

            ServerReady = true;
        }

        // [S3] → ServerSessionService.AddPlayer로 위임
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Server] OnServerAddPlayer conn={conn.connectionId}");
            _sessionService.AddPlayer(conn);
        }

        // [S-End] 세이브 + 세션 정리
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _bundleReadyConnIds.Remove(conn.connectionId);
            _sessionService?.RemovePlayer(conn);
            base.OnServerDisconnect(conn);
        }

        private void OnBundleReadyMessage(NetworkConnectionToClient conn, BundleReadyMessage msg)
        {
            _bundleReadyConnIds.Add(conn.connectionId);
        }

        // 씬 접근용 메서드
        public Scene GetServerScene(string sceneName) =>
            _serverSceneHandles.TryGetValue(sceneName, out var handle)
            ? handle.Result.Scene : SceneManager.GetSceneByPath(sceneName); //폴백

        #endregion


        #region 클라이언트 및 UI 제어

        // [C1] 클라이언트 시작: 서비스 생성 + 핸들러 등록
        // 서버가 보내는 메시지 → NetworkClient.RegisterHandler (클라이언트가 받으니까)
        // -> ClientSceneSyncService.cs 로 이어짐
        public override void OnStartClient()
        {
            Debug.Log("[Client] OnStartClient");
            base.OnStartClient();
            _clientSyncService = new ClientSceneSyncService(this, _zoneLoader);
            _clientBundleService = new ClientBundleSyncService(_zoneLoader, _progressSub, _progressPub, _uiManager);

            // 왜 바닥 드롭 아이템(FloorItem) 관련 핸들러는 없는가?
            // 바닥 드롭 아이템(FloorItem.prefab)은 NetworkFloorItem이 프리팹에 이미 붙어있어
            // Registered Spawnable Prefabs 리스트에만 넣으면 NetworkManager 기본 로직이 알아서 등록해준다 — 커스텀 스폰 핸들러 불필요.

            NetworkClient.RegisterHandler<BundleRequiredMessage>(msg =>
                            _clientBundleService.OnBundleRequired(msg, destroyCancellationToken).Forget());
            NetworkClient.RegisterHandler<AdditiveSceneMessage>(_clientSyncService.OnAdditiveSceneMessage);
            NetworkClient.RegisterHandler<SpawnReadyMessage>(_clientSyncService.OnSpawnReadyMessage);
            NetworkClient.RegisterHandler<ScreenFadeMessage>(_clientSyncService.OnScreenFadeMessage);
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

        public void SetServerAddress(string address)
        {
            if (!string.IsNullOrEmpty(address))
            {
                networkAddress = address.Trim();
                Debug.Log($"[Network] Server address updated to '{networkAddress}'");
            }
        }

        public void ToggleHost(bool isOn)
        {
#if UNITY_SERVER && !UNITY_EDITOR
            // 서버 빌드에서는 StartServer()가 자동 구동되므로 Host 토글 무시
            return;
#else
            if (isOn) StartHost();
            else StopHost();
#endif
        }

        public void ToggleClient(bool isOn)
        {
#if UNITY_SERVER && !UNITY_EDITOR
            return;
#else
            if (isOn) StartClient();
            else StopClient();
#endif
        }
        #endregion
    }
}

/*
서버                                      클라이언트
────                                      ──────

[Host/Client 버튼 클릭]
  └─ OnStartServer()
       ├─ BundleReadyMessage 핸들러 등록
       ├─ SceneLoadedAckMessage 핸들러 등록
       └─ CheckZones 루프 시작 (0.5초 간격, isSpawned=true 플레이어만)

  └─ OnStartClient()
       ├─ BundleRequiredMessage 핸들러 등록 → ClientBundleSyncService
       ├─ AdditiveSceneMessage 핸들러 등록 → ClientSceneSyncService
       └─ SpawnReadyMessage 핸들러 등록 → ClientSceneSyncService

OnServerAddPlayer(conn)
  └─ PrepareAndSpawn(conn)
       └─ BundleRequiredMessage 전송 ──→  번들 다운로드 시작 (UI 표시)
                                           └─ 완료 시 BundleReadyMessage 전송

   BundleReadyMessage     ←──────────   "번들 됐어!"

OnBundleReadyMessage(conn)
  └─ _bundleReadyConnIds에 기록
       └─ PrepareAndSpawn WaitUntil 해제

PrepareAndSpawn(cont.)
  ├─ AdditiveSceneMessage 전송  ──→  씬 로드 시작 (번들 이미 캐시됨)
  └─ SpawnReadyMessage 전송    ──→  _expectedSceneLoads = 1

                                         씬 로드 완료
   SceneLoadedAckMessage  ←──────────   "씬 됐어!"

OnSceneLoadedAck(conn)
  └─ SpawnPlayer()   ← 조건 분기 없이 항상 스폰

SpawnPlayer(conn)
  ├─ playerPrefab Instantiate
  ├─ NetworkServer.AddPlayerForConnection
  ├─ MoveGameObjectToScene
  └─ session.isSpawned = true
       └─ CheckZones 루프 대상에 포함됨
*/