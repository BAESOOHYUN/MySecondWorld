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
        [SerializeField] private Transform _backSocket; // 화살통 — 등/어깨 본

        [Header("궁수 전용")]
        [SerializeField] private GameObject _bowArrowPrefab; // Bow 장착 시 오른손에 항상 붙는 화살 — 특정 소유 아이템과 무관한 고정 시각 프롭
        [SerializeField] private GameObject _quiverPrefab; // 화살통 — 특정 소유 아이템과 무관한 고정 시각 프롭(화살과 동일 패턴)


        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private WeaponAnimationMapSO _animationMap;


        [SyncVar(hook = nameof(OnMainHandChanged))]
        private int _mainHandDbKey = Unequipped;
        private bool _currentVisualIsAddressable;
        private GameObject _currentVisual;
        private GameObject _currentArrowVisual;
        private GameObject _currentQuiverVisual;

        private Cleverous.VaultInventory.Inventory _inventory;
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
            Debug.Log("[Equip] OnStartLocalPlayer — WeaponActions 등록 완료");
        }

        // IWeaponEquipActionsProvider 구현 — InventoryUI는 이 메서드만 알면 되고, Command는 몰라도 된다
        public void EquipMainHand(int weaponDbKey)
        {
            Debug.Log($"[Equip] EquipMainHand(client) 호출 dbKey={weaponDbKey}");
            CmdEquipMainHand(weaponDbKey);
        }

        [Command]
        public void CmdEquipMainHand(int weaponDbKey)
        {
            Debug.Log($"[Equip][Server] CmdEquipMainHand 수신 dbKey={weaponDbKey}");
            if (weaponDbKey == Unequipped)
            {
                _mainHandDbKey = Unequipped;  // 해제 요청은 무조건 허용
                Debug.Log("[Equip][Server] 해제 요청 처리");
                return;
            }

            if (Vault.Get(weaponDbKey) is not EquipmentItem item)// return;   // dbKey가 무기 아이템이 아님
            {
                Debug.LogWarning($"[Equip][Server] 거부 — dbKey={weaponDbKey}가 EquipmentItem이 아님 (Vault.Get 결과: {Vault.Get(weaponDbKey)})");
                return;
            }
            if (!AllowedMainHandCategories.Contains(item.Category))// return; // 이번 Phase 제외 대상
            {
                Debug.LogWarning($"[Equip][Server] 거부 — Category={item.Category}가 허용 목록에 없음");
                return;
            }
            if (!(_inventory?.Contains(item, 1) ?? false))// return;          // 미소유면 제외
            {
                Debug.LogWarning($"[Equip][Server] 거부 — 인벤토리 미보유 (item={item.Title}, _inventory null? {_inventory == null})");
                return;
            }
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
            Debug.Log($"[Equip] OnMainHandChanged(SyncVar hook) {oldKey} → {newKey}");
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
                Debug.Log("[Equip] ApplyMainHandVisual — 해제 처리");
                if (_animator != null)
                    _animator.runtimeAnimatorController = _defaultController;
                return;
            }

            if (Vault.Get(dbKey) is not EquipmentItem item)
            {
                Debug.LogWarning($"[Equip] ApplyMainHandVisual — dbKey={dbKey} EquipmentItem 아님, 중단");
                return;
            }

            var socket = GetSocket(item.Category);
            Debug.Log($"[Equip] ApplyMainHandVisual — item={item.Title} Category={item.Category} socket={(socket != null ? socket.name : "NULL!!")}");

            if (socket == null) return;

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

            // Bow는 '왼손에 활 자체' + '오른손에 화살' + '등에 화살통'이 동시에 붙어야 자연스럽다
            if (item.Category == WeaponCategory.Bow)
                SpawnArrowVisual();
            if (item.Category == WeaponCategory.Bow && _quiverPrefab != null && _backSocket != null)
            {
                var quiverInstance = Instantiate(_quiverPrefab, _backSocket);
                ResetSocketTransform(quiverInstance.transform, _backSocket);
                _currentQuiverVisual = quiverInstance;
            }

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
                    Destroy(_currentVisual);    // 풀 미사용 이유는 176번째 줄 주석 참고
                _currentVisual = null;
            }

            if (_currentArrowVisual != null)
            {
                Destroy(_currentArrowVisual);   // 풀 미사용 이유는 176번째 줄 주석 참고
                _currentArrowVisual = null;
            }

            if (_currentQuiverVisual != null)
            {
                Destroy(_currentQuiverVisual);  // 풀 미사용 이유는 176번째 줄 주석 참고
                _currentQuiverVisual = null;
            }
        }

        private void SpawnArrowVisual()
        {
            if (_bowArrowPrefab == null || _rightHandSocket == null) return;
            _currentArrowVisual = Instantiate(_bowArrowPrefab, _rightHandSocket);
            ResetSocketTransform(_currentArrowVisual.transform, _rightHandSocket);
        }

        // PlayerAttackController.OnShootRelease()가 발사 시점에 지금 들고 있는 화살을 넘겨받기 위해 호출.
        // 넘겨준 뒤엔 손이 비어 보이지 않도록 새 장식용 화살을 즉시 다시 붙인다.
        public GameObject TakeCurrentArrowVisual()
        {
            var arrow = _currentArrowVisual;
            _currentArrowVisual = null;
            SpawnArrowVisual();
            return arrow;
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