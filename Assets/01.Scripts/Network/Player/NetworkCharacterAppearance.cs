using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using VContainer;
using MessagePipe;
using Cleverous.VaultInventory;
using MySecondWorld.Customization;

namespace MySecondWorld.Network
{
    [RequireComponent(typeof(Cleverous.VaultInventory.Inventory))]
    public class NetworkCharacterAppearance : NetworkBehaviour, IUseInventory
    {
        private const string PrefKeyCustomized = "CharacterCustomized";

        [SyncVar(hook = nameof(OnAppearanceChanged))]
        private CharacterAppearanceData _appearance;

        private ObservableCharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _avatarPartKeyTable;
        private LocalPlayerInventoryProvider _localInventoryProvider;
        private Cleverous.VaultInventory.Inventory _inventory;

        // IUseInventory 구현 — Inventory 컴포넌트가 "누가 이 인벤토리의 주인인지" 알아야 해서 필요
        Cleverous.VaultInventory.Inventory IUseInventory.Inventory
        {
            get => _inventory;
            set => _inventory = value;
        }

        // (아이템 드롭, 상호작용 거리 체크)에 쓰이는 값
        public Transform MyTransform => transform;

        
        private IPublisher<RequestOpenCustomizeUIEvent> _openUIPublisher;

        [Inject]
        public void Construct(
            ObservableCharacterCustomizer customizer,
            AvatarPartKeyTableSO avatarPartKeyTable,
            LocalPlayerInventoryProvider localInventoryProvider,
            IObjectResolver resolver)
        {
            _customizer = customizer;
            _avatarPartKeyTable = avatarPartKeyTable;
            _localInventoryProvider = localInventoryProvider;

            // Root.Resolve를 쓰는 이유 :
            // PlayerScope의 MessagePipe 로컬 등록이 이 이벤트 타입을 가로채서,
            // 평범하게[Inject] 받으면 아무도 발행 안 하는 텅 빈 가짜 구독 대상을 받게 돼서, 이벤트가 와도 영원히 안 들린다.
            // 진짜 ProjectLifetimeScope가 소유한 진짜 공유 브로커를 받으려면 Root에서 직접 resolve해야 한다.
            var scoped = (IScopedObjectResolver)resolver;
            var saveRequestSub = scoped.Root.Resolve<ISubscriber<CharacterAppearanceSyncRequestedEvent>>();
            var openUISub = scoped.Root.Resolve<IPublisher<RequestOpenCustomizeUIEvent>>();
            _openUIPublisher = openUISub;

            // isLocalPlayer로 필터링하는 이유:
            // 이 broker는 화면에 보이는 모든 캐릭터가 같이 구독한다.
            // isLocalPlayer 체크 없이 두면 다른 플레이어 캐릭터도 반응해서 권한 없는 CmdSetAppearance 호출 경고가 매번 찍힌다.
            // 그걸 막기 위해 로컬 플레이어 본인 객체일 때만 호출한다.
            saveRequestSub.Subscribe(evt =>
            {
                if (!isLocalPlayer) return; // "그거 내 캐릭터일 때만 반응해"
                CmdSetAppearance(evt.Data);
            }).AddTo(destroyCancellationToken);
        }

        private void Awake()
        {
            _inventory = GetComponent<Cleverous.VaultInventory.Inventory>();
        }

        public override void OnStartServer()
        {
            // ault의 Inventory 컴포넌트는 Configuration만 Inspector에 연결해준다고 자동으로 동작하는 게 아니라,
            // 코드에서 Initialize(owner, resetContent: true)를 명시적으로 한 번 호출해줘야
            // IsInitialized = true가 되고 슬롯(Content)이 실제로 만들어진다.
            base.OnStartServer();
            _inventory.Initialize(this, resetContent: true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            Debug.Log($"[Inv] OnStartClient isServer={isServer} " +
                      $"IsInitialized={_inventory.IsInitialized}");

            // 호스트는 OnStartServer에서 이미 초기화됨.
            // 여기서 또 부르면 resetContent:true가 서버가 채워둔 내용을 지운다.
            if (isServer) return;

            _inventory.Initialize(this, resetContent: true);
        }

        // 서버가 클라이언트가 보낸 외형 데이터를 받았을 때, 각 파츠 인덱스마다 "이거 살 자격 있어?"를 재검증.
        // 없으면 그 필드만 이전 값으로 되돌림. 이게 진짜 권위 있는 검증(클라 UI는 속여도 여긴 못 속임).
        [Command]
        public void CmdSetAppearance(CharacterAppearanceData data)
        {
            var t = _avatarPartKeyTable;

            // IsDefined 이 값이 enum에 실제로 정의된 값인지 검증 (해킹 등으로 이상한 값 들어올 가능성 방어)
            if (!System.Enum.IsDefined(typeof(Gender), data.Gender))
                data.Gender = _appearance.Gender;

            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                int count = t.GetCount(part, data.Gender);
                data[part] = Clamp(data[part], count);

                // 그룹 A/B/C를 구분하지 않고 그냥 "맨몸(0)이 아닌 인덱스를 요청했나"
                if (data[part] != 0)
                {
                    // 그룹 A 파츠는 RequiredItems가 비어있어 requiredItem이 항상 null → 아래 체크가 자동으로 스킵된다.
                    var requiredItem = t.GetRequiredItem(part, data.Gender, data[part]);
                    if (requiredItem != null && 
                        !(_inventory?.Contains(requiredItem, 1) ?? false))
                        data[part] = _appearance[part]; // 미소유 → 이전 값으로 되돌림
                }
            }
            _appearance = data; // 여기서 대입하며 [SyncVar] 훅 발동 → OnAppearanceChanged 실행
        }

        private void OnAppearanceChanged(CharacterAppearanceData old, CharacterAppearanceData next)
        {
#if !UNITY_SERVER
            _customizer.ApplyAppearance(next).Forget(); // 파츠 프리팹 Instantiate — 렌더링용
#endif 
        }

        private static int Clamp(int value, int count)
        {
            if (value == -1) return -1; // "없음"은 유효한 상태 — 그대로 유지
            return value >= 0 && value < count ? value : (count > 0 ? 0 : -1);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            _localInventoryProvider.SetLocal(_inventory);
            if (!PlayerPrefs.HasKey(PrefKeyCustomized))
                _openUIPublisher.Publish(new RequestOpenCustomizeUIEvent());
        }
    }
}

//[Client] CharacterCustomizeUI
//   │  CmdSetAppearance(data)
//   ▼  [Command] RPC
//[Server] NetworkCharacterAppearance.CmdSetAppearance(data)
//   │  Gender 검증 +25개 필드 클램프
//   │  _appearance = data   (SyncVar 대입)
//   ▼
//[Mirror] SyncVar 변경 감지 → 모든 클라이언트에 자동 브로드캐스트
//   ▼
//[All Clients] NetworkCharacterAppearance.OnAppearanceChanged(old, next)
//   │  (SyncVar hook)
//   ▼
//   _customizer.ApplyAppearance(next)  → 시각 반영

