using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MessagePipe;
using VContainer;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Save;
using MySecondWorld.Inventory;
using Cleverous.VaultInventory;

namespace MySecondWorld.UI
{
    public enum InventoryCategoryTab
    {
        All,
        Equipment,
        Weapon,
        Accessory,
        Consumable
    }

    public class InventoryUI : MonoBehaviour
    {
        [Header("슬롯")]
        [SerializeField] private Transform _slotContent; // GridLayoutGroup 부착
        [SerializeField] private InventorySlotButton _slotButtonPrefab;

        [Header("카테고리 탭")]
        [SerializeField] private Button _tabAllButton;
        [SerializeField] private Button _tabEquipmentButton;
        [SerializeField] private Button _tabWeaponButton;
        [SerializeField] private Button _tabAccessoryButton;
        [SerializeField] private Button _tabConsumableButton;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton; // 허공(배경) 클릭 시 닫기

        [Header("중앙 장비 페이퍼돌")]
        [SerializeField] private EquipmentPaperdollUI _paperdollUI;

        private LocalPlayerInventoryProvider _inventoryProvider;
        private CharacterAppearanceService _appearanceService;
        private readonly List<InventorySlotButton> _spawnedSlots = new();
        private InventoryCategoryTab _currentTab = InventoryCategoryTab.All;

        // Open() 시점에 구독한 인스턴스를 기억 — Close() 때 다른 인스턴스로 바뀌어도 정확히 해당 구독만 해제
        private Cleverous.VaultInventory.Inventory _subscribedInventory;
        private IWeaponEquipActionsProvider _subscribedWeaponActions;

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider inventoryProvider,
            CharacterAppearanceService appearanceService
            )
        {
            _inventoryProvider = inventoryProvider;
            _appearanceService = appearanceService;
        }

        public bool IsOpen => gameObject.activeSelf;
        public event System.Action OnClosed;
        private ShopUI _activeShop;

        public void SetShopMode(ShopUI shop)
        {
            _activeShop = shop;
            if (gameObject.activeSelf)
                Refresh();
        }

        public void SetBackdropActive(bool active)
        {
            if (_backdropButton != null)
                _backdropButton.gameObject.SetActive(active);
        }

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Close);

            if (_tabEquipmentButton != null) _tabEquipmentButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Equipment));
            if (_tabAccessoryButton != null) _tabAccessoryButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Accessory));
            if (_tabWeaponButton != null) _tabWeaponButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Weapon));
            if (_tabConsumableButton != null) _tabConsumableButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Consumable));
            if (_tabAllButton != null) _tabAllButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.All));
            UpdateTabVisuals();

            if (_paperdollUI != null)
            {
                _paperdollUI.OnUnequipRequested += HandlePaperdollUnequip;
                _paperdollUI.OnSlotSelected += HandlePaperdollSlotSelected;
                _paperdollUI.OnTabChanged += HandlePaperdollTabChanged;
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnDestroy()
        {
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandStateChanged -= OnWeaponStateChanged;

            if (_appearanceService != null)
                _appearanceService.OnAppearanceChanged -= OnAppearanceStateChanged;

            if (_paperdollUI != null)
            {
                _paperdollUI.OnUnequipRequested -= HandlePaperdollUnequip;
                _paperdollUI.OnSlotSelected -= HandlePaperdollSlotSelected;
                _paperdollUI.OnTabChanged -= HandlePaperdollTabChanged;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            SetBackdropActive(true);
            if (_currentTab == InventoryCategoryTab.All && (_tabAllButton == null || !_tabAllButton.gameObject.activeInHierarchy))
            {
                _currentTab = InventoryCategoryTab.Equipment;
            }
            UpdateTabVisuals();

            _subscribedInventory = _inventoryProvider.Inventory;
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged += OnInventoryChanged; // 열려있는 동안만 구독 — 상점/바닥획득 등 다른 화면에서 바뀐 것도 즉시 반영

            _subscribedWeaponActions = _inventoryProvider.WeaponActions;
            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandStateChanged += OnWeaponStateChanged;

            if (_appearanceService != null)
            {
                _appearanceService.OnAppearanceChanged -= OnAppearanceStateChanged;
                _appearanceService.OnAppearanceChanged += OnAppearanceStateChanged;
            }

            Refresh();
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;

            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;
            _subscribedInventory = null;

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandStateChanged -= OnWeaponStateChanged;
            _subscribedWeaponActions = null;

            if (_appearanceService != null)
                _appearanceService.OnAppearanceChanged -= OnAppearanceStateChanged;

            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        private bool _isChangingTab;

        public void SetTab(InventoryCategoryTab tab)
        {
            if (_isChangingTab) return;
            _isChangingTab = true;

            try
            {
                _currentTab = tab;
                UpdateTabVisuals();

                if (_paperdollUI != null)
                {
                    if (tab == InventoryCategoryTab.Equipment && _paperdollUI.CurrentTab != PaperdollTab.Armor)
                        _paperdollUI.SetTab(PaperdollTab.Armor);
                    else if (tab == InventoryCategoryTab.Accessory && _paperdollUI.CurrentTab != PaperdollTab.Accessory)
                        _paperdollUI.SetTab(PaperdollTab.Accessory);
                    else if (tab == InventoryCategoryTab.Weapon)
                        _paperdollUI.SelectSlot(EquipmentSlotType.Weapon);
                }

                Refresh();
            }
            finally
            {
                _isChangingTab = false;
            }
        }

        private void UpdateTabVisuals()
        {
            SetTabButtonVisual(_tabEquipmentButton, _currentTab == InventoryCategoryTab.Equipment);
            SetTabButtonVisual(_tabAccessoryButton, _currentTab == InventoryCategoryTab.Accessory);
            SetTabButtonVisual(_tabWeaponButton, _currentTab == InventoryCategoryTab.Weapon);
            SetTabButtonVisual(_tabConsumableButton, _currentTab == InventoryCategoryTab.Consumable);
            SetTabButtonVisual(_tabAllButton, _currentTab == InventoryCategoryTab.All);
        }

        private void SetTabButtonVisual(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected 
                    ? new Color(0.18f, 0.65f, 0.95f, 1f) 
                    : new Color(0.12f, 0.18f, 0.28f, 0.85f);
            }
            var txt = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt != null)
            {
                txt.color = isSelected ? Color.white : new Color(0.7f, 0.8f, 0.9f, 0.8f);
                txt.fontStyle = isSelected ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            }
        }

        private bool MatchesCategory(RootItem item, InventoryCategoryTab tab)
        {
            if (tab == InventoryCategoryTab.All) return true;

            // 1. 소모품 탭: ConsumableItem이거나 화살통(Quiver)인 경우
            if (item is ConsumableItem || (item is EquipmentItem quiverItem && quiverItem.Category == WeaponCategory.Quiver))
                return tab == InventoryCategoryTab.Consumable;

            if (item is EquipmentItem eq)
            {
                // 2. 악세사리 탭: 등/귀 장식 등 순수 외형 장식만 판정 (반지/팔찌/목걸이는 캐스터 무기이므로 무기 탭으로 이동)
                bool isAccessory = eq.Restriction != null && (eq.Restriction.Title == "BackAttachment" || eq.Restriction.Title == "Ear");

                if (isAccessory)
                    return tab == InventoryCategoryTab.Accessory;

                // 3. 무기 탭: 화살통을 제외한 모든 무기 (검, 단검, 도끼, 창, 활, 반지, 팔찌, 목걸이 등)
                bool isWeapon = eq.Category != WeaponCategory.None && eq.Category != WeaponCategory.Quiver;
                if (isWeapon)
                    return tab == InventoryCategoryTab.Weapon;

                // 4. 장비/방어구 탭 (상의, 하의, 장갑, 신발, 투구 등)
                bool isEquipment = eq.Category == WeaponCategory.None && eq.Restriction != null;
                if (isEquipment)
                    return tab == InventoryCategoryTab.Equipment;
            }

            return false;
        }

        private void OnInventoryChanged(int index) => Refresh();
        private void OnWeaponStateChanged() => Refresh();
        private void OnAppearanceStateChanged() => Refresh();

        private void Refresh()
        {
            ClearSlots();

            if (_paperdollUI != null)
            {
                _paperdollUI.Refresh(_appearanceService, _inventoryProvider?.WeaponActions, _subscribedInventory);
            }

            var inv = _inventoryProvider?.Inventory;
            if (inv == null) return;

            for (int i = 0; i < inv.MaxSlots; i++)
            {
                var stack = inv.Get(i); // 특정 슬롯의 아이템을 읽어오는 Getter 메서드
                if (stack?.Source == null) continue; // 빈 슬롯은 표시 안 함 — 보유한 것만 보여주는 게 이번 Phase의 핵심 결정

                // 1. 성별 필터링: 이성 전용 장비는 가방 슬롯에서 숨김
                if (stack.Source is EquipmentItem equipItem && _appearanceService != null)
                {
                    if (!_appearanceService.IsItemMatchingGender(equipItem))
                        continue;
                }

                // 2. 카테고리 탭 필터링
                if (!MatchesCategory(stack.Source, _currentTab))
                    continue;

                int capturedIndex = i;
                var slot = Instantiate(_slotButtonPrefab, _slotContent);

                bool isConsumable = stack.Source is ConsumableItem; // 소모품(포션 등)인지 판별
                bool isEquippable = stack.Source is EquipmentItem eq
                                    && eq.Category != WeaponCategory.Quiver
                                    && (eq.Category != WeaponCategory.None || eq.Restriction != null);
                bool isEquippedWeapon = stack.Source is EquipmentItem weaponCheck
                                    && weaponCheck.Category != WeaponCategory.None
                                    && weaponCheck.Category != WeaponCategory.Quiver
                                    && _inventoryProvider.WeaponActions?.CurrentMainHandDbKey == weaponCheck.GetDbKey();

                bool isEquippedPart = stack.Source is EquipmentItem partCheck
                                    && partCheck.Category == WeaponCategory.None
                                    && partCheck.Restriction != null
                                    && _appearanceService != null
                                    && _appearanceService.IsPartEquipped(partCheck);

                bool isEquipped = isEquippedWeapon || isEquippedPart;
                bool isShopMode = _activeShop != null && _activeShop.IsOpen;

                slot.Setup(
                   stack.Source.UiIcon,
                   stack.StackSize,
                   isEquippable,
                   isConsumable,
                   isEquipped,
                   onDiscard: () =>
                   {
                       if (isShopMode && stack.Source is EquipmentItem eqItem)
                       {
                           _activeShop.RequestSell(eqItem);
                           return;
                       }

                       // 로컬 예측: 버리려는 아이템이 현재 장착 무기면 즉시 로컬 비주얼 해제
                       if (_inventoryProvider.WeaponActions != null 
                           && stack.Source is EquipmentItem weaponItem
                           && _inventoryProvider.WeaponActions.CurrentMainHandDbKey == weaponItem.GetDbKey())
                       {
                           _inventoryProvider.WeaponActions.UnequipMainHand();
                       }

                       // 버리려는 아이템이 현재 착용 중인 파츠(장비/악세)면 즉시 장착 해제
                       if (_appearanceService != null 
                           && stack.Source is EquipmentItem discardedPart
                           && discardedPart.Category == WeaponCategory.None
                           && _appearanceService.IsPartEquipped(discardedPart))
                       {
                           _appearanceService.UnequipWearableItem(discardedPart);
                       }

                       _inventoryProvider.Actions?.DiscardItem(capturedIndex); // UI에서 "버리기" 버튼 클릭 시 그 슬롯의 아이템을 '전량 제거'.
                   },
                   onAction: ResolveEquipAction(stack.Source, capturedIndex),
                   isShopMode: isShopMode);

                _spawnedSlots.Add(slot);
            }
        }

        // 클릭한 아이템 종류에 따라 실제로 무슨 액션을 실행할지 결정.
        // 우선순위: 화살통 > 무기 > 커스터마이징 파츠(장비/악세사리) > (그 외) 소모품 사용
        private System.Action ResolveEquipAction(RootItem source, int slotIndex)
        {
            // 1. 무기면 무기 장착 / 해제
            if (source is EquipmentItem weaponItem 
                && weaponItem.Category != WeaponCategory.None
                && weaponItem.Category != WeaponCategory.Quiver
                )
                return () =>
                {
                    int dbKey = weaponItem.GetDbKey();
                    bool alreadyEquipped = _inventoryProvider.WeaponActions?.CurrentMainHandDbKey == dbKey;
                    Debug.Log($"[Equip] 무기 {(alreadyEquipped ? "해제" : "장착")} 클릭: {weaponItem.Title} dbKey={dbKey}");
                    if (alreadyEquipped)
                        _inventoryProvider.WeaponActions?.UnequipMainHand();
                    else
                        _inventoryProvider.WeaponActions?.EquipMainHand(dbKey);
                };

            // 2. 커스터마이징 파츠(장비/방어구, 악세사리)면 파츠 장착 / 해제
            if (source is EquipmentItem partItem 
                && partItem.Category == WeaponCategory.None 
                && partItem.Restriction != null)
                return () =>
                {
                    bool alreadyEquipped = _appearanceService != null && _appearanceService.IsPartEquipped(partItem);
                    Debug.Log($"[Equip] 파츠 {(alreadyEquipped ? "해제" : "장착")} 클릭: {partItem.Title}");
                    if (alreadyEquipped)
                        _appearanceService?.UnequipWearableItem(partItem);
                    else
                        _appearanceService?.EquipWearableItem(partItem);
                };

            // 3. 소모품이면 "사용"
            return () =>  _inventoryProvider.Actions?.UseItem(slotIndex);
        }

        private void HandlePaperdollUnequip(EquipmentSlotType slotType, EquipmentItem item)
        {
            if (item == null) return;

            if (slotType == EquipmentSlotType.Weapon)
            {
                _inventoryProvider.WeaponActions?.UnequipMainHand();
            }
            else
            {
                _appearanceService?.UnequipWearableItem(item);
            }
            Refresh();
        }

        private void HandlePaperdollSlotSelected(EquipmentSlotType slotType, EquipmentItem item)
        {
            if (item == null)
            {
                switch (slotType)
                {
                    case EquipmentSlotType.Weapon:
                    case EquipmentSlotType.Shield:
                        SetTab(InventoryCategoryTab.Weapon);
                        break;
                    case EquipmentSlotType.HelmetAttachment:
                    case EquipmentSlotType.ShoulderAttach:
                    case EquipmentSlotType.ElbowAttach:
                    case EquipmentSlotType.BackAttachment:
                    case EquipmentSlotType.HipsAttachment:
                    case EquipmentSlotType.Ear:
                    case EquipmentSlotType.Ring:
                    case EquipmentSlotType.Necklace:
                        SetTab(InventoryCategoryTab.Accessory);
                        break;
                    default:
                        SetTab(InventoryCategoryTab.Equipment);
                        break;
                }
            }
        }

        private void HandlePaperdollTabChanged(PaperdollTab tab)
        {
            if (tab == PaperdollTab.Armor && _currentTab != InventoryCategoryTab.Equipment)
            {
                SetTab(InventoryCategoryTab.Equipment);
            }
            else if (tab == PaperdollTab.Accessory && _currentTab != InventoryCategoryTab.Accessory)
            {
                SetTab(InventoryCategoryTab.Accessory);
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in _spawnedSlots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _spawnedSlots.Clear();
        }
    }
}