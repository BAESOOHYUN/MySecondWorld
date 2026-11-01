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
    [RequireComponent(typeof(Cleverous.VaultInventory.Inventory))]
    public class NetworkWeaponEquip : NetworkBehaviour, IWeaponEquipActionsProvider
    {
        // 미장착 표시. Vault도 빈 슬롯을 -1로 쓴다. 0은 실제 아이템 dbKey일 수 있어서 쓰지 않는다
        private const int Unequipped = -1;

        // 이 Category 이외에는 MainHand 장착 자체를 거부한다(2026-07-20 스코프 조정 —
        // 둔기류/너클/Staff/Sceptre는 맞는 애니메이션이 없어 이번 Phase 제외).
        private static readonly HashSet<WeaponCategory> AllowedMainHandCategories = new()
        {
            WeaponCategory.Sword, WeaponCategory.Dagger, WeaponCategory.Axe, WeaponCategory.Polearm,
            WeaponCategory.Bow,
            WeaponCategory.Ring, WeaponCategory.Bracelet, WeaponCategory.Necklace,
        };

        [Header("장착 소켓")]
        [SerializeField] private Transform _rightHandSocket;// 오른손 — Sword/Dagger/Axe/Polearm 장착 + Bow 장착 시 화살(ArrowToShoot) 부착
        [SerializeField] private Transform _leftHandSocket; // 왼손 — Bow 전용(활은 왼손으로 쥐고 오른손으로 시위를 당기는 그립)
        [SerializeField] private Transform _fingerSocket;
        [SerializeField] private Transform _wristSocket;
        [SerializeField] private Transform _neckSocket;
        [SerializeField] private Transform _backSocket; // 화살통 — 등/어깨 본

        [Header("궁수 전용")]
        [SerializeField] private GameObject _quiverPrefab; // 화살통 — 특정 소유 아이템과 무관한 고정 시각 프롭(화살과 동일 패턴)

        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private WeaponAnimationMapSO _animationMap;

        [SyncVar(hook = nameof(OnMainHandChanged))]
        private int _mainHandDbKey = Unequipped;

        [SyncVar(hook = nameof(OnMainHandInventorySlotChanged))]
        private int _mainHandInventorySlotIndex = Unequipped;

        private int _predictedMainHandDbKey = Unequipped; // 로컬 예측값 — SyncVar 왕복 안 기다리고 UI가 즉시 정답을 보게
        private int _predictedMainHandInventorySlot = Unequipped;
        private int _equipToken; // 빠른 연속 교체 시, 먼저 시작된 느린 로드가 나중에 끝나 최신 장착을 덮어쓰는 걸 막기 위한 토큰
        private bool _currentVisualIsAddressable;
        private GameObject _currentVisual;
        private GameObject _currentQuiverVisual;
        private Cleverous.VaultInventory.Inventory _inventory;
        private RuntimeAnimatorController _defaultController;
        private LocalPlayerInventoryProvider _localInventoryProvider;

        public int CurrentMainHandDbKey => _predictedMainHandDbKey;
        public int CurrentMainHandInventorySlotIndex => _predictedMainHandInventorySlot;

        // 메인핸드 시각 오브젝트가 적용된 뒤 발행. 인자는 방금 장착된 카테고리(해제 시 None).
        // Combat 어셈블리의 타입을 쓰지 않는 게 중요하다 — 여기서 ArrowNockPoint를 직접 참조하면
        // Network → Combat 참조가 생겨 기존 Combat → Network 참조와 순환이 된다.
        // 이벤트로 뒤집으면 Combat 쪽이 일방적으로 구독하므로 방향이 하나로 유지된다.
        public event System.Action<WeaponCategory> OnMainHandVisualApplied; // 무기 모델이 적용됨. 그 무기의 종류(카테고리)를 같이 넘김
        public event System.Action OnMainHandEquipChanged; // 장착 상태가 바뀔 때 UI 새로고침 신호

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

        // IWeaponEquipActionsProvider 구현 — InventoryUI는 이 메서드만 알면 되고, Command는 몰라도 된다
        public void EquipMainHand(int weaponDbKey, int inventorySlotIndex)
        {
            Debug.Log($"[Equip] EquipMainHand(client) 호출 dbKey={weaponDbKey}");

            // [핵심] 서버 응답 기다리지 않고 내 손에 0.01초 즉시 무기 부착
            if (isLocalPlayer)
            {
                // 무기를 칸 번호로 장착
                // 인벤토리에 같은 장비가 여러개일때, 어떤 슬롯(inventorySlotIndex) 장비를 사용하는지 찾아냄.
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

        // 서버 전용 — 다른 컴포넌트(인벤토리 버리기/거래 등)가 이 무기의 장착 상태를 강제로 해제할 때 호출.
        // 넘긴 dbKey가 현재 장착 중인 것과 다르면 무시한다(엉뚱한 무기가 실수로 풀리는 것 방지).
        [Server]
        public void ServerUnequipIfMatches(int dbKey)
        {
            if (_mainHandDbKey == dbKey)
            {
                _mainHandDbKey = Unequipped; // [Command] 우회 없이 서버 변수 직접 대입 -> [SyncVar] 훅 발동하여 모든 클라이언트로 전파
            }
        }

        // 세이브용 — 장착 중인 무기 dbKey. 미장착은 -1
        [Server]
        public int ServerGetMainHandDbKey() => _mainHandDbKey;

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

            // TODO: 둔기·너클·Staff·Sceptre는 애니메이션이 생기면 허용
            if (!AllowedMainHandCategories.Contains(item.Category))
            {
                Debug.LogWarning($"[Equip][Server] 거부 — Category={item.Category}가 허용 목록에 없음");
                return false;
            }

            // 칸을 모르면(-1, 세이브 복원(로그인 직후, 서버가 스스로 장착을 복구하는 순간))
            // 처음 발견한 칸(item 정보 == 슬롯정보인 칸)을 쓴다.
            if (inventorySlotIndex < 0)
                inventorySlotIndex = FindInventorySlotOf(item);

            if (_inventory == null 
                || inventorySlotIndex < 0 
                || inventorySlotIndex >= _inventory.MaxSlots 
                || _inventory.Get(inventorySlotIndex)?.Source != item) // 그 칸에 들어 있는 아이템이 장착하려는 아이템과 다르거나, 칸이 비어있다.
            {
                Debug.LogWarning($"[Equip][Server] 거부 — {inventorySlotIndex}번 칸에 {item.Title}이 없음");
                return false;
            }

            _mainHandInventorySlotIndex = inventorySlotIndex;    // [SyncVar] 훅 발동 → OnMainHandInventorySlotChanged 실행
            _mainHandDbKey = weaponDbKey;                   // [SyncVar] 훅 발동 → OnMainHandChanged 실행
            return true;
        }

        private int FindInventorySlotOf(EquipmentItem item)
        {
            for (int i = 0; i < _inventory.MaxSlots; i++)
            {
                if (_inventory.Get(i)?.Source == item)
                    return i;
            }
            return -1; // 가방 어디에도 그 아이템이 없을 때
        }

        // 인벤토리가 바뀐 뒤 호출. 장착한 칸이 비거나 다른 아이템으로 바뀌었으면 해제한다.
        // 같은 무기가 다른 칸에 있어도 대신 장착하지 않는다.
        // 예) 2번 칸 도끼 장착 중 → 2번을 버리면 해제, 5번을 버리면 모션 유지
        [Server]
        public void ServerValidateMainHand()
        {
            if (_mainHandDbKey == Unequipped) return;
            // stack은 slot의 내용물
            var stack = _inventory != null
                        && _mainHandInventorySlotIndex >= 0 && _mainHandInventorySlotIndex < _inventory.MaxSlots
                        ? _inventory.Get(_mainHandInventorySlotIndex) : null;
            // 서버가 기억하는 장착 중인 무기(_mainHandDbKey)와 
            // 장착한 칸(_mainHandInventorySlotIndex)에 지금 들어 있는 아이템의 종류 번호(stack.Source.GetDbKey())
            bool steelThere = stack?.Source != null && stack.Source.GetDbKey() == _mainHandDbKey;
            if (steelThere) return; // 칸에 아직 그 무기가 있음 → 그대로 둔다

            // 여기까지 왔다면 칸이 비었거나 다른 아이템 → 해제
            _mainHandInventorySlotIndex = Unequipped;
            _mainHandDbKey = Unequipped;
        }

        private void OnMainHandInventorySlotChanged(int oldSlot, int newSlot)
        {
            if (newSlot == _predictedMainHandInventorySlot) return; // 이미 로컬 예측으로 반영한 값
            _predictedMainHandInventorySlot = newSlot;
            OnMainHandEquipChanged?.Invoke(); // UI (새로고침)
        }

       [Command]
        public void CmdEquipMainHand(int weaponDbKey, int inventorySlotIndex) => ServerTryEquipMainHand(weaponDbKey, inventorySlotIndex);
        
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

        #region 무기 시각 적용 (private)

        private void OnMainHandChanged(int oldKey, int newKey)
        {
            if (newKey == _predictedMainHandDbKey) return;   // 로컬 예측해서 이미 적용한 값이면 건너뛰기.
            Debug.Log($"[Equip] OnMainHandChanged(SyncVar hook) {oldKey} → {newKey}");
            _equipToken++;
#if !UNITY_SERVER
            ApplyMainHandVisual(newKey, _equipToken).Forget(); // 무기 모델 Instantiate — 렌더링용
#endif
        }

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
                    Destroy(_currentVisual);    // 풀 미사용 이유는 176번째 줄 주석 참고
                _currentVisual = null;
            }

            if (_currentQuiverVisual != null)
            {
                Destroy(_currentQuiverVisual);  // 풀 미사용 이유는 176번째 줄 주석 참고
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