using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using VContainer;
using MessagePipe;
using Cleverous.VaultInventory;
using MySecondWorld.Customization;

namespace MySecondWorld.Network
{
    using Inventory = Cleverous.VaultInventory.Inventory;

    // 캐릭터 외형(파츠 모양 번호 + 색)을 서버가 쥐고 모든 클라이언트에 동기화한다.
    // 파츠별로 "인벤토리 몇 번 칸 옷을 입었나"(장착 칸 번호)도 서버가 들고 있다가 세이브에 쓴다.
    //
    // 외형이 바뀌는 길:
    // [Client] CharacterCustomizeUI / CharacterAppearanceService
    //    │  CmdSetAppearance(data)
    //    ▼  [Command]
    // [Server] CmdSetAppearance: 성별 검증 + 25개 파츠 클램프 + 미보유 파츠 롤백
    //    │  _appearance = data   (SyncVar 대입)
    //    ▼
    // [Mirror] SyncVar 변경 감지 → 모든 클라이언트에 자동 전송
    //    ▼
    // [All Clients] OnAppearanceChanged 훅 → _customizer.ApplyAppearance(next) → 모델에 파츠를 입힌다
    [RequireComponent(typeof(Inventory))]
    public class NetworkCharacterAppearance : NetworkBehaviour, IUseInventory
    {
        #region 상태

        // 선언 순서를 바꾸지 말 것 — Mirror가 SyncVar를 선언 순서로 직렬화한다.
        [SyncVar(hook = nameof(OnAppearanceChanged))]
        private CharacterAppearanceData _appearance;
        [SyncVar] private bool _hasServerAppearance;

        // '서버가 기억하는' 파츠별 장착 칸 번호 (배열 위치 = PartType 번호, 값 = 인벤토리 칸 번호, 미장착·모름은 -1)
        // SyncVar가 아니라 서버 메모리에만 있고, 세이브에 쓰려고 들고 있는다.
        // 무기(NetworkWeaponEquip)의 _mainHandInventorySlotIndex에 해당
        private int[] _equippedPartSlots;

        private Inventory _inventory;

        #endregion

        #region 공개 프로퍼티 · 이벤트

        // IUseInventory 구현 — Inventory 컴포넌트가 "누가 이 인벤토리의 주인인지" 알아야 해서 필요
        Inventory IUseInventory.Inventory
        {
            get => _inventory;
            set => _inventory = value;
        }

        // 아이템 드롭, 상호작용 거리 체크에 쓰이는 값
        public Transform MyTransform => transform;

        // "외형 적용 완료" 신호. portrait 캡쳐에 필요
        public static event System.Action LocalAppearanceReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LocalAppearanceReady = null; // Domain Reload OFF 대응

        #endregion

        #region DI

        private ObservableCharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _avatarPartKeyTable;
        private LocalPlayerInventoryProvider _localInventoryProvider;
        private CharacterSaveService _saveService;
        private CharacterAppearanceService _appearanceService;
        private IPublisher<RequestInitialCustomizeEvent> _initialCustomizePublisher;

        [Inject]
        public void Construct(
            ObservableCharacterCustomizer customizer,
            AvatarPartKeyTableSO avatarPartKeyTable,
            LocalPlayerInventoryProvider localInventoryProvider,
            CharacterSaveService saveService,
            CharacterAppearanceService appearanceService,
            IObjectResolver resolver
            )
        {
            _customizer = customizer;
            _avatarPartKeyTable = avatarPartKeyTable;
            _localInventoryProvider = localInventoryProvider;
            _saveService = saveService;
            _appearanceService = appearanceService;

            // Root.Resolve를 쓰는 이유 :
            // PlayerScope의 MessagePipe 로컬 등록이 이 이벤트 타입을 가로채서,
            // 평범하게 [Inject] 받으면 아무도 발행 안 하는 텅 빈 가짜 구독 대상을 받게 되어 이벤트가 영원히 안 들린다.
            // 진짜 ProjectLifetimeScope가 소유한 공유 브로커를 받으려면 Root에서 직접 resolve해야 한다.
            var scoped = (IScopedObjectResolver)resolver;
            var saveRequestSub = scoped.Root.Resolve<ISubscriber<CharacterAppearanceSyncRequestedEvent>>();
            var initialCustomizePub = scoped.Root.Resolve<IPublisher<RequestInitialCustomizeEvent>>();
            _initialCustomizePublisher = initialCustomizePub;

            // isLocalPlayer로 필터링하는 이유:
            // 이 broker는 화면에 보이는 모든 캐릭터가 같이 구독한다.
            // isLocalPlayer 체크 없이 두면 다른 플레이어 캐릭터도 반응해서 권한 없는 CmdSetAppearance 호출 경고가 매번 찍힌다.
            // 그걸 막기 위해 로컬 플레이어 본인 객체일 때만 호출한다.
            saveRequestSub.Subscribe(evt =>
            {
                if (!isLocalPlayer) return; // 내 캐릭터일 때만 반응

                // 외형 조립 후 HUD 얼굴 캡처 신호 발송
                ApplyAndNotify(evt.Data).Forget();

                // 보내는 순서가 중요! 외형 -> 칸 순서

                // 외형을 서버에 보낸다. 서버가 검증해서 확정하면 SyncVar로 다른 유저들에게도 퍼진다.
                CmdSetAppearance(evt.Data);

                // 칸 번호는 외형 다음에 보낸다. 서버 검증이 방금 확정된 외형을 쓰기 때문이다.
                // 칸 번호 없이 오는 이벤트(커스터마이징 창 저장 등)는 null이라 보내지 않는다.
                if (evt.EquippedPartSlots != null)
                    CmdSetEquippedPartSlots(evt.EquippedPartSlots);
            }).AddTo(destroyCancellationToken);
        }

        #endregion

        #region 생명주기

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
        }

        public override void OnStartServer()
        {
            // Vault의 Inventory 컴포넌트는 Configuration만 Inspector에 연결해 준다고 자동으로 동작하는 게 아니라,
            // 코드에서 Initialize(owner, resetContent: true)를 명시적으로 한 번 호출해야
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

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            _localInventoryProvider.SetLocal(_inventory);
            if (NetworkManager.singleton is GameNetworkManager gnm)
                gnm.RevealScreen();

            if (_hasServerAppearance)
                _saveService.Save(_appearance);                // 서버 값을 이 클라의 기억에 넣는다
            else                                               // 최초 접속(미커스터마이징) 유저
            {
                _saveService.Clear();                          // 이전 계정의 기억이 남아 있으면 지운다
                ApplyAndNotify(_saveService.Load()).Forget();  // 기본 외형 (로컬에만 표시, 서버엔 저장 안 함)
                _initialCustomizePublisher.Publish(new RequestInitialCustomizeEvent());
            }
        }

        #endregion

        #region 서버: 저장 · 복원 통로 (ServerSessionService가 부른다)

        // 세이브용 — 서버가 가진 외형을 꺼낸다. 외형이 없는 계정이면 false
        [Server]
        public bool ServerTryGetAppearance(out CharacterAppearanceData data)
        {
            data = _appearance;
            return _hasServerAppearance;
        }

        // 복원용 — 세이브에 있던 외형을 서버에 되살린다
        [Server]
        public void ServerRestoreAppearance(CharacterAppearanceData data)
        {
            _appearance = data;
            _hasServerAppearance = true;
        }

        // 세이브용 — 지금 서버가 기억하는 장착 칸 번호. 받은 적이 없으면 null
        // 무기(NetworkWeaponEquip)의 ServerGetMainHandInventorySlotIndex에 해당
        [Server]
        public int[] ServerGetEquippedPartSlots() => _equippedPartSlots;

        // 복원용 — 저장돼 있던 장착 칸 번호를 서버 기억으로 되살린다
        [Server]
        public void ServerRestoreEquippedPartSlots(int[] slots) => _equippedPartSlots = ValidateEquippedPartSlots(slots);

        // 서버 → 이 캐릭터의 클라이언트: 서버가 기억하는 장착 칸 번호를 알려 준다. (접속 때 5단계가 부른다)
        // 기억한 값이 없으면 보낼 것이 없다.
        [Server]
        public void ServerSendEquippedPartSlots()
        {
            if (_equippedPartSlots == null) return;
            TargetSetEquippedPartSlots(connectionToClient, _equippedPartSlots);
        }

        // 클라이언트에서 실행된다. 받은 칸 번호를 서비스에 후보로 넘긴다.
        // 서비스는 인벤토리를 열 때 그 칸에 정말 그 옷이 있는지 확인한 뒤 쓴다.
        [TargetRpc] //서버가 특정 한 명의 클라이언트에게만 보내는 호출
        private void TargetSetEquippedPartSlots(NetworkConnectionToClient target, int[] slots)
        {
            _appearanceService.SetServerEquippedPartSlots(slots);
        }

        #endregion

        #region 서버: 클라 요청 처리 · 검증

        // 클라가 보낸 외형을 받았을 때, 각 파츠 인덱스마다 "이거 살 자격 있어?"를 서버가 재검증한다.
        // 자격이 없으면 그 필드만 이전 값으로 되돌린다. 이게 진짜 권위 있는 검증이다 (클라 UI는 속여도 여기는 못 속인다).
        [Command]
        public void CmdSetAppearance(CharacterAppearanceData data)
        {
            _hasServerAppearance = true;
            try
            {
                // 1. 성별 enum 유효성 검증
                // IsDefined = 이 값이 enum에 실제로 정의된 값인지 검증 (해킹 등으로 이상한 값이 들어올 가능성 방어)
                if (!System.Enum.IsDefined(typeof(Gender), data.Gender))
                    data.Gender = _appearance.Gender;

                var t = _avatarPartKeyTable;

                // [핵심 방어] _avatarPartKeyTable이 유효할 때만 인덱스 범위 및 아이템 소유 검증 수행
                if (t != null)
                {
                    foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                    {
                        int count = t.GetCount(part, data.Gender);
                        data[part] = Clamp(data[part], count);

                        // 기본 파츠(0)나 미착용(-1)이 아닌 경우 인벤토리 소유권 검증
                        // 그룹 A/B/C를 구분하지 않고 그냥 "맨몸(0)이 아닌 인덱스를 요청했나"
                        if (data[part] != 0)
                        {
                            // 그룹 A 파츠는 RequiredItems가 비어 있어 requiredItem이 항상 null → 아래 체크가 자동으로 스킵된다.
                            var requiredItem = t.GetRequiredItem(part, data.Gender, data[part]);
                            if (requiredItem != null &&
                                 !(_inventory?.Contains(requiredItem, 1) ?? false))
                            {
                                data[part] = _appearance[part]; // 미소유 → 이전 값으로 롤백
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[NetAppearance] [Server] _avatarPartKeyTable이 null이므로 파츠 유효성 검증을 건너뜁니다.");
                }
                // 2. 어떤 예외가 있더라도 서버의 SyncVar에 확실하게 대입!
                _appearance = data;
                Debug.Log($"[NetAppearance] [Server] _appearance 동기화 성공! Gender={data.Gender}, Head={data.Head}, Torso={data.Torso}");

                // 외형이 바뀌었으니, 이미 기억하던 칸 번호가 아직 맞는지 다시 확인한다.
                // (칸 번호 없이 외형만 오는 경우에, 바뀐 옷을 가리키는 낡은 칸 번호가 남지 않게 하려는 것)
                if (_equippedPartSlots != null)
                    _equippedPartSlots = ValidateEquippedPartSlots(_equippedPartSlots);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetAppearance] [Server] CmdSetAppearance 처리 중 예외 발생: {ex}");
                // 예외가 발생하더라도 최소한 데이터는 대입하여 SyncVar 전송 보장
                // 여기서 대입하며 [SyncVar] 훅 발동 → OnAppearanceChanged 실행
                _appearance = data;
            }
        }

        // 클라가 장착 칸 번호 배열을 서버로 보낸다. 서버가 검증기로 거른 뒤 '맞는 것만 기억'한다.
        // 외형을 확정하는 CmdSetAppearance가 먼저 처리되어야 검증이 새 외형 기준으로 돌아간다.
        [Command]
        public void CmdSetEquippedPartSlots(int[] clientSlots)
        {
            _equippedPartSlots = ValidateEquippedPartSlots(clientSlots);
        }

        // 클라가 보낸 장착 칸 번호 배열(clientSlots)을 서버가 검사한다. 클라의 말을 믿지 않는다.
        // clientSlots의 위치 = 파츠 종류(PartType 번호), 값 = 클라가 주장한 인벤토리 칸 번호 (안 입은 파츠는 -1)
        // 파츠마다 "그 칸에 그 옷이 정말 있나"를 확인해서, 맞는 칸만 남기고 틀린 칸은 -1로 바꿔 돌려준다.
        [Server]
        private int[] ValidateEquippedPartSlots(int[] clientSlots)
        {
            // 결과 배열: 파츠 수만큼 만들고 전부 -1(없음)로 시작한다. 맞는 칸만 아래에서 채운다.
            int partCount = System.Enum.GetValues(typeof(PartType)).Length;
            var result = new int[partCount];
            for (int i = 0; i < partCount; i++) result[i] = -1;

            // 배열이 없거나 길이가 파츠 수와 다르면 이상한 요청이니 전부 -1로 돌려준다
            if (clientSlots == null || clientSlots.Length != partCount || _avatarPartKeyTable == null)
                return result;

            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                // clientSlot = 클라가 이 파츠에 대해 말한 칸 번호. (예: 왼쪽 어깨 → clientSlots[17] → 7)
                // 번호를 꺼낸 것뿐이고, 아직 인벤토리는 열지 않았다.
                int clientSlot = clientSlots[(int)part];
                // -1(안 입음)이거나 인벤토리 칸 개수를 넘는 번호는 건너뛴다
                if (clientSlot < 0 || clientSlot >= _inventory.MaxSlots) continue;

                // index = 서버가 확정한 이 파츠의 모양 번호. (clientSlot과는 다른 값)
                // 0 이하면 맨몸이거나 안 입은 것이라 가방 칸이 없으니 건너뛴다
                int index = _appearance[part];
                if (index <= 0) continue;

                // requiredItem = 그 모양을 입으려면 가방에 있어야 하는 아이템. 서버가 자기 외형으로 계산한 정답이다.
                var requiredItem = _avatarPartKeyTable.GetRequiredItem(part, _appearance.Gender, index);
                // 클라가 말한 clientSlot 칸 하나만 열어서(Get), 그 안의 아이템이 정답과 같으면 인정한다.
                // 같은 아이템이 여러 칸에 있어도, 클라가 말한 칸이 맞으면 그 칸 번호가 그대로 남는다.
                if (requiredItem != null && _inventory.Get(clientSlot)?.Source == requiredItem)
                    result[(int)part] = clientSlot;
            }
            return result;
        }

        #endregion

        #region SyncVar 훅

        // _appearance가 바뀌면 모든 클라이언트에서 불린다 → 모델에 파츠를 다시 입힌다 (서버 빌드는 건너뜀)
        private void OnAppearanceChanged(CharacterAppearanceData old, CharacterAppearanceData next)
        {
#if !UNITY_SERVER
            ApplyAndNotify(next).Forget();
#endif
        }

        #endregion

        #region 헬퍼 메서드

        // 외형을 모델에 입히고, 다 입혀지면 얼굴 사진(HUD 썸네일) 촬영 신호를 보낸다
        private async Cysharp.Threading.Tasks.UniTaskVoid ApplyAndNotify(CharacterAppearanceData next)
        {
            await _customizer.ApplyAppearance(next); // 1.머리, 눈, 투구 등 모든 파츠가 다 로딩 될 때까지 기다림
            if (isLocalPlayer) LocalAppearanceReady?.Invoke(); // 2. 완벽하게 다 입혀진 순간 사진(썸네일) 촬영
        }

        // 외부에서 얼굴 캡처 신호를 안전하게 쏠 수 있는 정적 함수
        public static void TriggerLocalAppearanceReady()
        {
            LocalAppearanceReady?.Invoke();
        }

        // 파츠 번호가 후보 개수 안에 들어오게 맞춘다. -1(없음)은 그대로 둔다.
        private static int Clamp(int value, int count)
        {
            if (value == -1) return -1; // "없음"은 유효한 상태 — 그대로 유지
            return value >= 0 && value < count ? value : (count > 0 ? 0 : -1);
        }

        #endregion
    }
}
