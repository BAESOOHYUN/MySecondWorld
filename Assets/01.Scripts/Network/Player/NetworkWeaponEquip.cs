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
        // "미장착" 센티널. dbKey 기본값(int.MinValue, DataEntity 미등록 상태)과 동일하게 맞춰서 0을 쓰지 않는다
        // (0은 실제로 등록된 아이템의 정상 dbKey일 수 있다.)
        private const int Unequipped = int.MinValue;

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

        [Header("궁수 전용")]
        [SerializeField] private GameObject _bowArrowPrefab; // Bow 장착 시 오른손에 항상 붙는 화살 — 특정 소유 아이템과 무관한 고정 시각 프롭


        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private WeaponAnimationMapSO _animationMap;


        [SyncVar(hook = nameof(OnMainHandChanged))]
        private int _mainHandDbKey = Unequipped;

        private Cleverous.VaultInventory.Inventory _inventory;
        private GameObject _currentVisual;
        private bool _currentVisualIsAddressable;
        private GameObject _currentArrowVisual;
        private int _equipToken; // 빠른 연속 교체 시, 먼저 시작된 느린 로드가 나중에 끝나 최신 장착을 덮어쓰는 걸 막기 위한 토큰
        private RuntimeAnimatorController _defaultController;
        private LocalPlayerInventoryProvider _localInventoryProvider;

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
        }

        // IWeaponEquipActionsProvider 구현 — InventoryUI는 이 메서드만 알면 되고, Command는 몰라도 된다
        public void EquipMainHand(int weaponDbKey) => CmdEquipMainHand(weaponDbKey);

        [Command]
        public void CmdEquipMainHand(int weaponDbKey)
        {
            if (weaponDbKey == Unequipped)
            {
                _mainHandDbKey = Unequipped;  // 해제 요청은 무조건 허용
                return;
            }

            if (Vault.Get(weaponDbKey) is not EquipmentItem item) return;   // dbKey가 무기 아이템이 아님
            if (!AllowedMainHandCategories.Contains(item.Category)) return; // 이번 Phase 제외 대상
            if (!(_inventory?.Contains(item, 1) ?? false)) return;          // 미소유면 제외
            // _inventory가 null            →  null  ?? false  →  false(인벤토리 없음) →  !false  →true
            // _inventory 있고 아이템 보유O →  true  ?? false  →  true(인벤O,아이템O)  →  !true   →false
            // _inventory 있고 아이템 보유X →  false ?? false  →  false(아이템 없음)   →  !false  →true

            _mainHandDbKey = weaponDbKey; // 여기서 대입하며 [SyncVar] 훅 발동 → OnMainHandChanged 실행
        }

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

        #region 무기 시각 적용 (private)
        private void OnMainHandChanged(int oldKey, int newKey)
        {
            _equipToken++;
#if !UNITY_SERVER
            ApplyMainHandVisual(newKey, _equipToken).Forget(); // 무기 모델 Instantiate — 렌더링용
#endif
        }

        private async UniTask ApplyMainHandVisual(int dbKey, int generation)
        {
            ReleaseCurrentVisual();
            
            if (dbKey == Unequipped)
            {
                if (_animator != null)
                    _animator.runtimeAnimatorController = _defaultController;
                return;
            }

            if (Vault.Get(dbKey) is not EquipmentItem item) return;

            var socket = GetSocket(item.Category);
            if (socket == null) return;

            if (item.ArtPrefab != null)
            {
                var instance = Instantiate(item.ArtPrefab, socket);
                if (generation != _equipToken)
                {
                    Destroy(instance);// 그 사이 더 최신 장착이 들어옴
                }
                _currentVisual = instance;
                _currentVisualIsAddressable = false;
            }
            else if (item.RemoteArtPrefab != null && item.RemoteArtPrefab.RuntimeKeyIsValid())
            {
                var instance = await Addressables.InstantiateAsync(item.RemoteArtPrefab, socket).ToUniTask();
                if (generation != _equipToken)
                {
                    Addressables.ReleaseInstance(instance);
                    return;
                }
                _currentVisual = instance;
                _currentVisualIsAddressable = true;
            }

            // Bow는 왼손에 활 자체 + 오른손에 화살(ArrowToShoot)이 동시에 붙어야 자연스럽다
            if (item.Category == WeaponCategory.Bow && _bowArrowPrefab != null && _rightHandSocket != null)
                _currentArrowVisual = Instantiate(_bowArrowPrefab, _rightHandSocket);

            if (_animationMap != null && _animator != null)
            {
                var controller = _animationMap.GetController(item.Category);
                if (controller != null)
                    _animator.runtimeAnimatorController = controller;
            }
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
                    Destroy(_currentVisual);
                _currentVisual = null;
            }
            if (_currentArrowVisual != null)
            {
                Destroy(_currentArrowVisual);
                _currentArrowVisual = null;
            }
        }
        #endregion
    }
}