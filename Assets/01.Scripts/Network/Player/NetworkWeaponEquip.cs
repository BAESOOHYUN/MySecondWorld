using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Mirror;
using VContainer;
using Cleverous.VaultSystem;
using MySecondWorld.Inventory;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Customization;

namespace MySecondWorld.Network
{
    // 메인핸드 무기 장착. 서버가 "무슨 무기를 인벤토리 몇 번 칸에서 장착했는지"를 권한으로 가지고,
    // 클라이언트는 로컬 예측으로 먼저 보여 준 뒤 SyncVar 동기화로 맞춘다.
    [RequireComponent(typeof(Cleverous.VaultInventory.Inventory))]
    public class NetworkWeaponEquip : NetworkBehaviour, IWeaponEquipActionsProvider
    {
        #region 상수 · 정적 데이터

        // 미장착 표시. 장착한 무기의 dbKey와 인벤토리 칸 번호 양쪽에 쓴다.
        // 0은 실제 dbKey일 수도, 0번 칸일 수도 있어서 쓰지 않는다 (Vault도 빈 슬롯을 -1로 쓴다).
        private const int Unequipped = -1;

        // 이 Category 이외에는 MainHand 장착 자체를 거부한다(2026-07-20 스코프 조정 —
        // 둔기류/너클/Staff/Sceptre는 맞는 애니메이션이 없어 이번 Phase 제외).
        // TODO: 해당 카테고리의 애니메이션이 생기면 허용
        private static readonly HashSet<WeaponCategory> AllowedMainHandCategories = new()
        {
            WeaponCategory.Sword, WeaponCategory.Dagger, WeaponCategory.Axe, WeaponCategory.Polearm,
            WeaponCategory.Bow,
            WeaponCategory.Ring, WeaponCategory.Bracelet, WeaponCategory.Necklace,
        };

        #endregion

        #region 인스펙터 설정

        [Header("장착 소켓")]
        [SerializeField] private Transform _rightHandSocket;// 오른손 — Sword/Dagger/Axe/Polearm 장착 + Bow 장착 시 화살(ArrowToShoot) 부착
        [SerializeField] private Transform _leftHandSocket; // 왼손 — Bow 전용(활은 왼손으로 쥐고 오른손으로 시위를 당기는 그립)
        [SerializeField] private Transform _fingerSocket;   // 손가락 — 반지(캐스터)
        [SerializeField] private Transform _wristSocket;    // 손목 — 팔찌(캐스터)
        [SerializeField] private Transform _neckSocket;     // 목 — 목걸이(캐스터)
        [SerializeField] private Transform _backSocket;     // 화살통 — 등/어깨 본

        [Header("궁수 전용")]
        [SerializeField] private GameObject _quiverPrefab; // 화살통 — 특정 소유 아이템과 무관한 고정 시각 프롭(화살과 동일 패턴)

        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private WeaponAnimationMapSO _animationMap;

        #endregion

        #region 상태

        // ── 서버가 권한을 가지는 장착 정보 (모든 클라이언트에 동기화) ──
        // 선언 순서를 바꾸지 말 것 — Mirror가 SyncVar를 선언 순서로 직렬화한다.
        [SyncVar(hook = nameof(OnMainHandChanged))]
        private int _mainHandDbKey = Unequipped;                    // 장착한 무기의 종류(dbKey)

        [SyncVar(hook = nameof(OnMainHandInventorySlotChanged))]
        private int _mainHandInventorySlotIndex = Unequipped;       // 그 무기가 들어 있는 인벤토리 칸 번호

        // ── 로컬 예측값 — SyncVar 왕복을 기다리지 않고 UI가 즉시 정답을 보게 ──
        private int _predictedMainHandDbKey = Unequipped;
        private int _predictedMainHandInventorySlot = Unequipped;

        // ── 손에 든 모델 ──
        private int _equipToken; // 빠른 연속 교체 시, 먼저 시작된 느린 로드가 나중에 끝나 최신 장착을 덮어쓰는 걸 막기 위한 토큰
        private bool _currentVisualIsAddressable;
        private GameObject _currentVisual;
        private GameObject _currentQuiverVisual;

        // ── 참조 ──
        private Cleverous.VaultInventory.Inventory _inventory;
        private RuntimeAnimatorController _defaultController;      // 맨손일 때의 컨트롤러
        private LocalPlayerInventoryProvider _localInventoryProvider;

        #endregion

        #region 공개 프로퍼티 · 이벤트

        public int CurrentMainHandDbKey => _predictedMainHandDbKey;
        public int CurrentMainHandInventorySlotIndex => _predictedMainHandInventorySlot;

        // 메인핸드 시각 오브젝트가 적용된 뒤 발행. 인자는 방금 장착된 카테고리(해제 시 None).
        // Combat 어셈블리의 타입을 쓰지 않는 게 중요하다 — 여기서 ArrowNockPoint를 직접 참조하면
        // Network → Combat 참조가 생겨 기존 Combat → Network 참조와 순환이 된다.
        // 이벤트로 뒤집으면 Combat 쪽이 일방적으로 구독하므로 방향이 하나로 유지된다.
        public event System.Action<WeaponCategory> OnMainHandVisualApplied;

        // InventoryUI가 목록을 다시 그리는 신호로 구독함. 장착 정보(무기 종류·인벤토리 칸 번호)가 바뀌었을 때 발행.
        public event System.Action OnMainHandEquipChanged;

        #endregion

        #region 생명주기

        [Inject]
        public void Construct(LocalPlayerInventoryProvider localInventoryProvider)
        {
            _localInventoryProvider = localInventoryProvider;
        }

        private void Awake()
        {
            _inventory = GetComponent<Cleverous.VaultInventory.Inventory>();
            if (_animator != null)
                _defaultController = _animator.runtimeAnimatorController;
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            _localInventoryProvider.SetWeaponActions(this);
            Debug.Log("[Equip] OnStartLocalPlayer — WeaponActions 등록 완료");
        }

        #endregion

        #region 클라이언트 요청 (IWeaponEquipActionsProvider)

        // InventoryUI는 이 메서드만 알면 되고, Command는 몰라도 된다
        public void EquipMainHand(int weaponDbKey, int inventorySlotIndex)
        {
            Debug.Log($"[Equip] EquipMainHand(client) 호출 dbKey={weaponDbKey} 칸={inventorySlotIndex}");

            // [핵심] 서버 응답 기다리지 않고 내 손에 0.01초 즉시 무기 부착
            if (isLocalPlayer)
            {
                // 같은 무기가 여러 칸에 있을 때 어느 칸을 장착했는지 먼저 기억해서 UI가 바로 그 칸만 "해제"로 보여 주게 함
                _predictedMainHandInventorySlot = weaponDbKey == Unequipped ? Unequipped : inventorySlotIndex;

                if (weaponDbKey == _predictedMainHandDbKey)
                    OnMainHandEquipChanged?.Invoke(); // 같은 무기의 다른 칸으로 바꾼 것 — 모델은 그대로, UI만 갱신
                else
                {
                    _equipToken++;
                    ApplyMainHandVisual(weaponDbKey, _equipToken).Forget();
                }
            }

            CmdEquipMainHand(weaponDbKey, inventorySlotIndex);
        }

        public void UnequipMainHand() => EquipMainHand(Unequipped, Unequipped);

        #endregion

        #region 서버: 장착 처리 · 검증

        // 장착 방어는 3겹이다.
        // 1) 서버 검증: ServerTryEquipMainHand(요청 시), ServerValidateMainHand(인벤토리 변경 후). 진짜 상태는 항상 서버 기준.
        // 2) 클라 사전 차단: InventoryUI의 isEquippable. 이상한 요청이 애초에 안 나가게 한다.
        // 3) 거부 후 클라 보정: 아직 없음(TODO B7 후속 5). 서버가 거부하면 요청한 클라의 예측 화면이 그대로 남는다.

        // 거부(false)돼도 요청한 클라를 보정하지 않는다 — TODO B7 후속 5
        [Command]
        public void CmdEquipMainHand(int weaponDbKey, int inventorySlotIndex) => ServerTryEquipMainHand(weaponDbKey, inventorySlotIndex);

        // 검증 후 장착. 성공 true / 거부 false. (클라 요청과 세이브 복원이 같이 사용)
        [Server]
        public bool ServerTryEquipMainHand(int weaponDbKey, int inventorySlotIndex)
        {
            Debug.Log($"[Equip][Server] ServerTryEquipMainHand 호출 dbKey={weaponDbKey}");
            if (weaponDbKey == Unequipped)
            {
                _mainHandInventorySlotIndex = Unequipped;
                _mainHandDbKey = Unequipped;  // 해제 요청은 무조건 허용
                Debug.Log("[Equip][Server] 해제 요청 처리");
                return true;
            }

            if (Vault.Get(weaponDbKey) is not EquipmentItem item)// 장비 아님
            {
                Debug.LogWarning($"[Equip][Server] 거부 — dbKey={weaponDbKey}가 EquipmentItem이 아님 (Vault.Get 결과: {Vault.Get(weaponDbKey)})");
                return false;
            }

            if (!AllowedMainHandCategories.Contains(item.Category))
            {
                Debug.LogWarning($"[Equip][Server] 거부 — Category={item.Category}가 허용 목록에 없음");
                return false;
            }

            // 칸 번호를 모르면(-1: 로그인 직후 서버가 세이브로 장착을 복구할 때)
            // 인벤토리를 0번부터 훑어 같은 아이템이 든 첫 칸을 쓴다.
            if (inventorySlotIndex < 0)
                inventorySlotIndex = FindInventorySlotOf(item);

            if (_inventory == null
                || inventorySlotIndex < 0
                || inventorySlotIndex >= _inventory.MaxSlots
                || _inventory.Get(inventorySlotIndex)?.Source != item) // 그 칸이 비어 있거나, 장착하려는 아이템이 아닌 다른 아이템이 들어 있음
            {
                Debug.LogWarning($"[Equip][Server] 거부 — {inventorySlotIndex}번 칸에 {item.Title}이 없음");
                return false;
            }

            _mainHandInventorySlotIndex = inventorySlotIndex;   // [SyncVar] 훅 발동 → OnMainHandInventorySlotChanged 실행
            _mainHandDbKey = weaponDbKey;                       // [SyncVar] 훅 발동 → OnMainHandChanged 실행
            return true;
        }

        // 인벤토리가 바뀐 뒤 호출. 장착한 칸이 비거나 다른 아이템으로 바뀌었으면 해제한다.
        // 같은 무기가 다른 칸에 있어도 대신 장착하지 않는다.
        // 예) 2번 칸 도끼 장착 중 → 2번을 버리면 해제, 5번을 버리면 모션 유지
        [Server]
        public void ServerValidateMainHand()
        {
            if (_mainHandDbKey == Unequipped) return;

            // 장착한 칸의 내용물 (칸 번호가 범위 밖이거나 칸이 비었으면 null)
            var stack = _inventory != null
                        && _mainHandInventorySlotIndex >= 0 && _mainHandInventorySlotIndex < _inventory.MaxSlots
                        ? _inventory.Get(_mainHandInventorySlotIndex) : null;

            // 서버가 기억하는 장착 무기(_mainHandDbKey)와, 장착한 칸에 지금 들어 있는 아이템의 종류가 같은가
            bool stillThere = stack?.Source != null && stack.Source.GetDbKey() == _mainHandDbKey;
            if (stillThere) return; // 칸에 아직 그 무기가 있음 → 그대로 둔다

            // 칸이 비었거나 다른 아이템 → 해제
            _mainHandInventorySlotIndex = Unequipped;
            _mainHandDbKey = Unequipped;
        }

        // 세이브용 — 장착 중인 무기 dbKey. 미장착은 -1
        [Server]
        public int ServerGetMainHandDbKey() => _mainHandDbKey;

        // 인벤토리를 0번 칸부터 훑어 item이 든 첫 칸의 번호를 돌려준다. 없으면 -1.
        private int FindInventorySlotOf(EquipmentItem item)
        {
            for (int i = 0; i < _inventory.MaxSlots; i++)
            {
                if (_inventory.Get(i)?.Source == item)
                    return i;
            }
            return -1; // 가방 어디에도 그 아이템이 없을 때
        }

        #endregion

        #region 장착 정보 조회 (전투가 읽음)

        // 장착된 메인핸드 무기의 Attack(Flat) 스탯 합. 미장착이거나 무기가 아니면 0.
        // Percent 모디파이어는 베이스 공격력 집계기가 없는 이번 슬라이스에서는 제외한다.
        public float GetMainHandAttack()
        {
            if (_mainHandDbKey == Unequipped) return 0f;
            if (Vault.Get(_mainHandDbKey) is not EquipmentItem item) return 0f;

            float attack = 0f;
            foreach (var stat in item.Stats)
            {
                if (stat.Type == StatType.Attack && stat.ModifierType == ModifierType.Flat)
                    attack += stat.Value;
            }
            return attack;
        }

        // 장착된 메인핸드 무기의 카테고리. 미장착이거나 무기가 아니면 WeaponCategory.None.
        // PlayerAttackController가 근접/원거리 분기 판단에 사용한다.
        public WeaponCategory GetMainHandCategory()
        {
            if (_mainHandDbKey == Unequipped) return WeaponCategory.None;
            if (Vault.Get(_mainHandDbKey) is not EquipmentItem item) return WeaponCategory.None;
            return item.Category;
        }

        #endregion

        #region SyncVar 훅

        // 서버에서 장착한 무기가 바뀌어 내려왔을 때 호출 (로컬 플레이어는 이미 예측해서 적용했으면 건너뜀)
        private void OnMainHandChanged(int oldKey, int newKey)
        {
            if (newKey == _predictedMainHandDbKey) return;   // 로컬 예측해서 이미 적용한 값이면 건너뛰기.
            Debug.Log($"[Equip] OnMainHandChanged(SyncVar hook) {oldKey} → {newKey}");
            _equipToken++;
#if !UNITY_SERVER
            ApplyMainHandVisual(newKey, _equipToken).Forget(); // 무기 모델 Instantiate — 렌더링용 (서버 빌드는 모델을 만들지 않는다)
#endif
        }

        // 서버에서 장착한 칸 번호가 바뀌어 내려왔을 때 호출
        private void OnMainHandInventorySlotChanged(int oldSlot, int newSlot)
        {
            if (newSlot == _predictedMainHandInventorySlot) return; // 이미 로컬 예측으로 반영한 값
            _predictedMainHandInventorySlot = newSlot;
            OnMainHandEquipChanged?.Invoke(); // UI 새로고침
        }

        #endregion

        #region 무기 시각 적용 (private)

        private async UniTask ApplyMainHandVisual(int dbKey, int generation)
        {
            _predictedMainHandDbKey = dbKey; // 로컬 예측 반영 — SyncVar 왕복 기다리지 않고 UI가 즉시 정답을 보게
            ReleaseCurrentVisual();

            if (dbKey == Unequipped)
            {
                Debug.Log("[Equip] ApplyMainHandVisual — 해제 처리");
                // 같은 컨트롤러면 대입을 건너뛰고, 바꿨다면 바로 한 번
                // 애니메이션 상태를 지금 즉시 계산해서 뼈대에 반영(Update(0f))
                if (_animator != null && _animator.runtimeAnimatorController != _defaultController)
                {
                    _animator.runtimeAnimatorController = _defaultController;
                    _animator.Update(0f);
                }
                // 해제 경로
                OnMainHandVisualApplied?.Invoke(WeaponCategory.None);
                OnMainHandEquipChanged?.Invoke();
                return;
            }
            // 1. Vault에서 아이템 조회
            if (Vault.Get(dbKey) is not EquipmentItem item)
            {
                Debug.LogWarning($"[Equip] ApplyMainHandVisual — dbKey={dbKey} EquipmentItem 아님, 중단");
                return;
            }

            var socket = GetSocket(item.Category);
            Debug.Log($"[Equip] ApplyMainHandVisual — item={item.Title} Category={item.Category} socket={(socket != null ? socket.name : "NULL!!")}");

            if (socket == null) return;
            // 2. Instantiate → 소켓에 부착
            if (item.ArtPrefab != null)
            {
                Debug.Log($"[Equip] ArtPrefab 경로로 Instantiate: {item.ArtPrefab.name}");
                var instance = Instantiate(item.ArtPrefab, socket);
                ResetSocketTransform(instance.transform, socket);
                if (generation != _equipToken)
                {
                    // 오브젝트 풀 대신 Destroy 사용 — GameObjectPool은 Addressable 인스턴스 혼용 시
                    // 메모리 누수 위험이 있고(GameObjectPool.cs 주석 참고), 무기 장착은 몬스터/투사체처럼
                    // 고빈도가 아니라 플레이어가 어쩌다 누르는 액션이라 지금은 감수.
                    // TODO : 나중에 AddressableObjectPool이 생기면 그때 전환
                    Destroy(instance);// 그 사이 더 최신 장착이 들어옴
                }
                _currentVisual = instance;
                _currentVisualIsAddressable = false;
            }
            else if (item.RemoteArtPrefab != null && item.RemoteArtPrefab.RuntimeKeyIsValid())
            {
                Debug.Log($"[Equip] RemoteArtPrefab(Addressable) 경로로 Instantiate 시도");
                var instance = await Addressables.InstantiateAsync(item.RemoteArtPrefab, socket).ToUniTask();
                ResetSocketTransform(instance.transform, socket);
                if (generation != _equipToken)
                {
                    Addressables.ReleaseInstance(instance);
                    return;
                }
                _currentVisual = instance;
                _currentVisualIsAddressable = true;
            }
            else
            {
                Debug.LogWarning($"[Equip] ArtPrefab도 RemoteArtPrefab도 사용 불가 — 시각 오브젝트 없음 (ArtPrefab null, RemoteArtPrefab valid={item.RemoteArtPrefab?.RuntimeKeyIsValid()})");
            }

            // Bow는 '등에 화살통'이 동시에 붙어야 자연스럽다
            if (item.Category == WeaponCategory.Bow && _quiverPrefab != null && _backSocket != null)
            {
                var quiverInstance = Instantiate(_quiverPrefab, _backSocket);
                ResetSocketTransform(quiverInstance.transform, _backSocket);
                _currentQuiverVisual = quiverInstance;
            }

            if (_animationMap != null && _animator != null)
            {
                var controller = _animationMap.GetController(item.Category);

                if (controller != null && _animator.runtimeAnimatorController != controller)
                {
                    _animator.runtimeAnimatorController = controller;
                    _animator.Update(0f);   // 애니메이션을 앞으로 흘리지 않고,
                                            // 클립의 현재 시간에 해당하는 각 뼈의 회전·위치 계산만 지금 한 번 함 (T포즈 방지)
                }
            }
            // 장착 성공 경로
            OnMainHandVisualApplied?.Invoke(item.Category);
            OnMainHandEquipChanged?.Invoke();
        }

        private Transform GetSocket(WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.Bow => _leftHandSocket,
                WeaponCategory.Ring => _fingerSocket,
                WeaponCategory.Bracelet => _wristSocket,
                WeaponCategory.Necklace => _neckSocket,
                _ => _rightHandSocket,
            };
        }

        private void ReleaseCurrentVisual()
        {
            if (_currentVisual != null)
            {
                if (_currentVisualIsAddressable)
                    Addressables.ReleaseInstance(_currentVisual);
                else
                    Destroy(_currentVisual);    // 풀 미사용 이유는 ApplyMainHandVisual의 ArtPrefab 경로 주석 참고
                _currentVisual = null;
            }

            if (_currentQuiverVisual != null)
            {
                Destroy(_currentQuiverVisual);  // 풀 미사용 이유는 ApplyMainHandVisual의 ArtPrefab 경로 주석 참고
                _currentQuiverVisual = null;
            }
        }

        #endregion

        #region 헬퍼 메서드

        // Instantiate(prefab, socket)는 부모(소켓 본)의 스케일을 그대로 물려받는데,
        // 캐릭터 본은 FBX 임포트 스케일 때문에 1이 아닌 경우가 많아 무기가 안 보일 만큼 작아진다.
        // 위치/회전은 소켓 원점에 맞추고, 스케일은 부모 스케일을 역산해서 상쇄 — 항상 실제 크기(world scale 1)로 붙게 만든다.
        private static void ResetSocketTransform(Transform instance, Transform socket)
        {
            instance.localPosition = Vector3.zero;
            instance.localRotation = Quaternion.identity;
            var parentScale = socket.lossyScale;
            instance.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        }

        #endregion
    }
}
