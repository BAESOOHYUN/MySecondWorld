using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using VContainer;
using MessagePipe;
using Cleverous.VaultInventory;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;

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
    //    │  _serverAppearance = data   (SyncVar 대입)
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
        private CharacterAppearanceData _serverAppearance;
        [SyncVar] private bool _hasServerAppearance;

        // '서버가 기억하는' 파츠별 장착 칸 번호 (배열 위치 = PartType 번호, 값 = 인벤토리 칸 번호, 미장착·모름은 -1)
        // SyncVar가 아니라 서버 메모리에만 있고, 세이브에 쓰려고 들고 있는다.
        // 무기(NetworkWeaponEquip)의 _mainHandInventorySlotIndex에 해당
        private int[] _equippedPartSlots;

        // 서버가 기억하는 "한 번이라도 가져 본 아이템" dbKey 집합. 세이브에 쓰려고 서버 메모리에만 둔다
        // HashSet을 쓴 이유는 같은 아이템을 몇 번 얻어도 한 번만 기록되고, 중복 검사도 필요 없기 때문
        private readonly HashSet<int> _unlockedItemKeys = new();

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
        private LocalAppearanceCache _appearanceCache;
        private CharacterAppearanceService _appearanceService;
        private IPublisher<RequestInitialCustomizeEvent> _initialCustomizePublisher;
        private readonly List<CharacterAppearanceData> _recentSent = new();

        [Inject]
        public void Construct(
            ObservableCharacterCustomizer customizer,
            AvatarPartKeyTableSO avatarPartKeyTable,
            LocalPlayerInventoryProvider localInventoryProvider,
            LocalAppearanceCache appearanceCache,
            CharacterAppearanceService appearanceService,
            IObjectResolver resolver
            )
        {
            _customizer = customizer;
            _avatarPartKeyTable = avatarPartKeyTable;
            _localInventoryProvider = localInventoryProvider;
            _appearanceCache = appearanceCache;
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

                // 낡은 반사 무시 (버튼 연타시 버벅임 현상)
                // 서버가 마지막 값을 반사하지 않는 경우(서버 값이 이미 같을 때 등)에는
                // 기록이 비워지지 않고 남는다. (최대 8개까지만 쌓임)
                // 새 값이 올 때마다 최신 값 비교가 먼저
                _recentSent.Add(evt.Data);
                if (_recentSent.Count > 8) _recentSent.RemoveAt(0);

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
            _inventory.OnItemAdded += HandleServerItemAdded;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_inventory != null) 
                _inventory.OnItemAdded -= HandleServerItemAdded;
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
                _appearanceCache.Set(_serverAppearance);                // 서버 값을 이 클라의 기억에 넣는다
            else                                               // 최초 접속(미커스터마이징) 유저
            {
                _appearanceCache.Clear();                          // 이전 계정의 기억이 남아 있으면 지운다
                ApplyAndNotify(_appearanceCache.Get()).Forget();  // 기본 외형 (로컬에만 표시, 서버엔 저장 안 함)
                _initialCustomizePublisher.Publish(new RequestInitialCustomizeEvent());
            }
        }

        #endregion

        #region 서버: 저장 · 복원 통로 (ServerSessionService가 부른다)

        // 세이브용 — 서버가 가진 외형을 꺼낸다. 외형이 없는 계정이면 false
        [Server]
        public bool ServerTryGetAppearance(out CharacterAppearanceData data)
        {
            data = _serverAppearance;
            return _hasServerAppearance;
        }

        // 복원용 — 세이브에 있던 외형을 서버에 되살린다
        [Server]
        public void ServerRestoreAppearance(CharacterAppearanceData data)
        {
            _serverAppearance = data;
            _hasServerAppearance = true;
        }

        // 세이브용 — 지금 서버가 기억하는 장착 칸 번호. 받은 적이 없으면 null
        // 무기(NetworkWeaponEquip)의 ServerGetMainHandInventorySlotIndex에 해당
        [Server]
        public int[] ServerGetEquippedPartSlots() => _equippedPartSlots;

        // 복원용 — 저장돼 있던 장착 칸 번호를 서버 기억으로 되살린다
        [Server]
        public void ServerRestoreEquippedPartSlots(int[] slots) => _equippedPartSlots = ValidateEquippedPartSlots(slots);


        // (도감) 획득 기록. -1(빈 칸)은 무시
        [Server]
        public void ServerMarkUnlocked(int dbKey)
        {
            if (dbKey != -1) _unlockedItemKeys.Add(dbKey);
        }

        // (도감) 세이브용: 집합을 배열로 꺼냄
        [Server]
        public int[] ServerGetUnlockedKeys()
        {
            var keys = new int[_unlockedItemKeys.Count];
            _unlockedItemKeys.CopyTo(keys);
            return keys;
        }

        // (도감) 복원용 : 세이브의 목록을 집합에 합침. 덮어쓰지 않고 합쳐서, 이미 쌓인 기록이 날아가지 않게 함
        [Server]
        public void ServerRestoreUnlockedKeys(int[] keys)
        {
            if (keys == null) return; // 옛 세이브
            foreach (var key in keys) ServerMarkUnlocked(key);
        }

        // 입은 파츠의 칸이 비었으면(버리기·판매) 서버가 그 파츠를 벗긴다. (무기의 ServerValidateMainHand에 해당)
        // 반드시 인벤토리를 정리하기 "전에" 부른다. 정리하면 다음 복사본이 그 칸으로 당겨져 와 빈 칸이 가려지기 때문이다.
        [Server]
        public void ServerReleaseEmptiedParts()
        {
            if (_equippedPartSlots == null) return;        // 서버가 기억한 입은 칸이 없으면 검사할 것도 없다

            var next = _serverAppearance;                        // 외형의 복사본. [SyncVar]는 통째로 대입해야 변경이 전파된다
            bool changed = false;                          // 하나라도 벗겼는가

            for (int i = 0; i < _equippedPartSlots.Length; i++) // 파츠 25개를 하나씩 본다 (i = PartType 번호)
            {
                int slot = _equippedPartSlots[i];          // 이 파츠가 입고 있는 인벤토리 칸 번호
                if (slot < 0) continue;                    // -1이면 안 입은 파츠라 건너뜀
                if (slot < _inventory.MaxSlots && _inventory.Get(slot).Source != null)
                    continue;                              // 그 칸에 아직 옷이 있으면 정상이니 건너뜀

                next[(PartType)i] = 0;                     // 칸이 비었다 → 외형을 맨몸(0)으로
                _equippedPartSlots[i] = -1;                // 입은 칸 기록도 "없음"으로
                changed = true;
            }

            if (!changed) return;                          // 벗긴 게 없으면 알릴 것도 없다
            _serverAppearance = next;                            // [SyncVar] 훅 → 모든 클라가 모델을 다시 입힌다
            ServerSendEquippedPartSlots();                 // 바뀐 칸 기록을 이 캐릭터의 주인(본인) 클라에게만 보낸다
        }

        // 입은 칸 번호를 새 번호로 고친다
        // 인벤토리 아이템이 앞으로 당겨졌을 때, 파츠별 입은 칸 번호도 같이 옮기고 클라에 알린다.
        // oldToNew = 서버(CompactInventory)가 만든 번역표. oldToNew[옛 칸] = 새 칸
        [Server]
        public void ServerRemapEquippedPartSlots(int[] oldToNew)
        {
            if (_equippedPartSlots == null) return;        // 기억한 값이 없으면 옮길 것도 없다

            for (int i = 0; i < _equippedPartSlots.Length; i++) // 파츠 25개를 하나씩 본다
            {
                int slot = _equippedPartSlots[i];          // 이 파츠가 입은 옛 칸 번호
                if (slot >= 0 && slot < oldToNew.Length)   // 입은 파츠이고, 번역표 범위 안일 때만
                    _equippedPartSlots[i] = oldToNew[slot];// 번역표에서 새 번호를 읽어 덮어쓴다
            }
            ServerSendEquippedPartSlots();                 // 고친 번호를 클라에게 보낸다
        }

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
            _appearanceService.ApplyServerEquippedPartSlots(slots, _inventory);
        }

        #endregion

        #region 서버: 클라 요청 처리 · 검증

        // 클라 → 서버
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
                    data.Gender = _serverAppearance.Gender;

                var t = _avatarPartKeyTable;

                // [핵심 방어] _avatarPartKeyTable이 유효할 때만 인덱스 범위 및 아이템 소유 검증 수행
                if (t != null)
                {
                    // 1) 범위 검사만 서버가 직접 한다 (번호가 후보 개수를 넘지 않게)
                    foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                        data[part] = Clamp(data[part], t.GetCount(part, data.Gender));

                    // 2) 소유 검증 + 투구 장식 규칙은 클라와 같은 함수로.
                    //    미소유 파츠는 직전 서버값으로, 성별이 바뀌었으면 맨몸으로 되돌린다.
                    //    item => ... 한 줄이 서버 쪽 "갖고 있나?"이다.
                    //    클라는 Provider.IsOwned를 넘기고, 서버는 _inventory.Contains를 넘긴다.
                    data = PartOwnership.ValidateOwnership(
                        t, data, item => _inventory?.Contains(item, 1) ?? false, _serverAppearance);
                }
                else
                {
                    Debug.LogWarning("[NetAppearance] [Server] _avatarPartKeyTable이 null이므로 파츠 유효성 검증을 건너뜁니다.");
                }

                // 2. 어떤 예외가 있더라도 서버의 SyncVar에 확실하게 대입!
                _serverAppearance = data;
                Debug.Log($"[NetAppearance] [Server] _serverAppearance 동기화 성공! Gender={data.Gender}, Head={data.Head}, Torso={data.Torso}");

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
                _serverAppearance = data;
            }
        }

        // 클라가 장착 칸 번호 배열을 서버로 보낸다. 서버가 검증기로 거른 뒤 '맞는 것만 기억'한다.
        // 외형을 확정하는 CmdSetAppearance가 먼저 처리되어야 검증이 새 외형 기준으로 돌아간다.
        [Command]
        public void CmdSetEquippedPartSlots(int[] clientSlots)
        {
            _equippedPartSlots = ValidateEquippedPartSlots(clientSlots);
        }

        // 묻는 것 : 클라가 말한 칸 번호가 맞나(그 칸에 그 아이템이 있나)
        // 하는 일 : 맞는 칸 번호만 기억, 틀린 건 -1
        // 입력    : 파츠별 칸 번호 배열
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
                int index = _serverAppearance[part];
                if (index <= 0) continue;

                // 서버는 "맞는 것만 남기는" 검증
                // 이 칸에 든 아이템이, 이 파츠 또는 이 파츠의 짝이 요구하는 아이템과 같은가?
                // "짝 파츠의 요구 아이템"도 칸의 아이템으로 인정
                var slotItem = _inventory.Get(clientSlot)?.Source as EquipmentItem;
                if (PartOwnership.IsItemForPart(_avatarPartKeyTable, part, _serverAppearance.Gender, index, slotItem))
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
            if (isLocalPlayer && IsStaleEcho(next)) return; // 낡은 반사는 캐시에도 화면에도 반영하지 않는다
            if (isLocalPlayer) _appearanceCache.Set(next); // 서버가 확정한 값으로 이 클라의 기억을 맞춘다
            ApplyAndNotify(next).Forget();
#endif
        }

        #endregion

        #region 헬퍼 메서드
        // 서버 반사(next)가 "내가 이미 지나온 옛날 상태의 반사"인지 판정한다. stale = 한물간, 낡은
        //   true  = 낡은 반사 → 무시
        //   false = 적용
        // _recentSent: 내가 서버로 보낸 값의 장부. 0번이 가장 오래된 것, 맨 뒤가 가장 최근 것.
        // next: 지금 막 도착한 서버 반사
        // 서버는 보낸 순서대로 돌려주므로 앞쪽부터 짝을 찾는다.
        private bool IsStaleEcho(CharacterAppearanceData next)
        {
            for (int i = 0; i < _recentSent.Count; i++)
            {
                if (!_recentSent[i].Equals(next)) continue;  // 이 칸은 짝이 아니니 다음 칸을 본다

                // 짝을 찾았다. 그 칸이 맨 뒤면 "내가 마지막으로 보낸 값"의 반사다.
                bool isLatest = (i == _recentSent.Count - 1);

                _recentSent.RemoveRange(0, i + 1);           // 짝 칸과 그보다 오래된 칸은 답이 왔으니 장부에서 뺀다
                return !isLatest;                            // 마지막이면 false(적용), 중간이면 true(무시)
            }
            return false;  // 어느 칸과도 안 맞음 = 서버가 값을 고침(거절 등) → 적용
        }

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

        // (도감) 획득 순간 기록 핸들러
        private void HandleServerItemAdded(RootItemStack stack)
        {
            if (stack?.Source == null) return;
            ServerMarkUnlocked(stack.Source.GetDbKey());
        }

        #endregion
    }
}
