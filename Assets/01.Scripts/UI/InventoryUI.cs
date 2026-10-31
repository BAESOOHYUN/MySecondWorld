using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cleverous.VaultInventory;
using MessagePipe;
using VContainer;
using R3;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Inventory;

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

        [Header("성별 필터 탭")]
        [SerializeField] private Toggle _tabAllGenderToggle;
        [SerializeField] private Toggle _tabFemaleToggle;
        [SerializeField] private Toggle _tabMaleToggle;

        [Header("중앙 장비 페이퍼돌")]
        [SerializeField] private EquipmentPaperdollUI _paperdollUI;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton; // 허공(배경) 클릭 시 닫기

        private LocalPlayerInventoryProvider _inventoryProvider;
        private static readonly Dictionary<EquipmentSlotType, InventoryCategoryTab> SlotToTab = new() // 슬롯 → 탭 : 이 슬롯은 어느 탭에 속하나 (이미 있음)
        {
            { EquipmentSlotType.HeadCoverings,    InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Torso,            InventoryCategoryTab.Equipment },
            { EquipmentSlotType.ArmUpper,         InventoryCategoryTab.Equipment },
            { EquipmentSlotType.ArmLower,         InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Hands,            InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Hips,             InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Legs,             InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Feet,             InventoryCategoryTab.Equipment },
            { EquipmentSlotType.HelmetAttachment, InventoryCategoryTab.Accessory },
            { EquipmentSlotType.ShoulderAttach,   InventoryCategoryTab.Accessory },
            { EquipmentSlotType.ElbowAttach,      InventoryCategoryTab.Accessory },
            { EquipmentSlotType.BackAttachment,   InventoryCategoryTab.Accessory },
            { EquipmentSlotType.HipsAttachment,   InventoryCategoryTab.Accessory },
            { EquipmentSlotType.Ear,              InventoryCategoryTab.Accessory },
            { EquipmentSlotType.Ring,             InventoryCategoryTab.Accessory },
            { EquipmentSlotType.Necklace,         InventoryCategoryTab.Accessory },
            { EquipmentSlotType.Weapon,           InventoryCategoryTab.Weapon },
            { EquipmentSlotType.Shield,           InventoryCategoryTab.Weapon },
        };
        private static readonly Dictionary<PartType, EquipmentSlotType> PartToSlot = new() // 파츠 → 슬롯 : 이 파츠는 어느 슬롯에 들어가나 (새로 만듦)
        {
            { PartType.HeadCoverings,     EquipmentSlotType.HeadCoverings },
            { PartType.Torso,             EquipmentSlotType.Torso },
            { PartType.ArmUpperLeft,      EquipmentSlotType.ArmUpper },
            { PartType.ArmUpperRight,     EquipmentSlotType.ArmUpper },
            { PartType.ArmLowerLeft,      EquipmentSlotType.ArmLower },
            { PartType.ArmLowerRight,     EquipmentSlotType.ArmLower },
            { PartType.HandLeft,          EquipmentSlotType.Hands },
            { PartType.HandRight,         EquipmentSlotType.Hands },
            { PartType.Hips,              EquipmentSlotType.Hips },
            { PartType.LegLeft,           EquipmentSlotType.Legs },
            { PartType.LegRight,          EquipmentSlotType.Legs },
            { PartType.KneeAttachLeft,    EquipmentSlotType.Feet },
            { PartType.KneeAttachRight,   EquipmentSlotType.Feet },
            { PartType.HelmetAttachment,  EquipmentSlotType.HelmetAttachment },
            { PartType.ShoulderAttachLeft,  EquipmentSlotType.ShoulderAttach },
            { PartType.ShoulderAttachRight, EquipmentSlotType.ShoulderAttach },
            { PartType.ElbowAttachLeft,   EquipmentSlotType.ElbowAttach },
            { PartType.ElbowAttachRight,  EquipmentSlotType.ElbowAttach },
            { PartType.BackAttachment,    EquipmentSlotType.BackAttachment },
            { PartType.HipsAttachment,    EquipmentSlotType.HipsAttachment },
            { PartType.Ear,               EquipmentSlotType.Ear },
        };
        private CharacterAppearanceService _appearanceService;
        private CharacterGenderFilterService _genderFilterService;
        private readonly List<InventorySlotButton> _spawnedSlots = new();
        private InventoryCategoryTab _currentTab = InventoryCategoryTab.All;

        // Open() 시점에 구독한 인스턴스를 기억 — Close() 때 다른 인스턴스로 바뀌어도 정확히 해당 구독만 해제
        private Cleverous.VaultInventory.Inventory _subscribedInventory;
        private IWeaponEquipActionsProvider _subscribedWeaponActions;
        private IDisposable _genderFilterSubscription;
        private bool _isChangingTab;
        private bool _openRequested; // 이 UI가 열린적이 있는지 체크

        public bool IsOpen => gameObject.activeSelf;
        public event Action OnClosed;
        private ShopUI _activeShop;

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider inventoryProvider,
            CharacterAppearanceService appearanceService,
            CharacterGenderFilterService genderFilterService)
        {
            _inventoryProvider = inventoryProvider;
            _appearanceService = appearanceService;
            _genderFilterService = genderFilterService;
        }


        private void Start()
        {
            // 버튼 리스너 등록
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Close);

            // 카테고리 탭 버튼
            if (_tabEquipmentButton != null)
                _tabEquipmentButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Equipment));
            if (_tabAccessoryButton != null)
                _tabAccessoryButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Accessory));
            if (_tabWeaponButton != null)
                _tabWeaponButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Weapon));
            if (_tabConsumableButton != null)
                _tabConsumableButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.Consumable));
            if (_tabAllButton != null)
                _tabAllButton.onClick.AddListener(() => SetTab(InventoryCategoryTab.All));
            UpdateTabVisuals();

            // 성별 필터 토글
            if (_tabAllGenderToggle != null)
                _tabAllGenderToggle.onValueChanged.AddListener(isOn => { if (isOn) _genderFilterService.SetGenderFilter(CharacterGenderFilter.All); });
            if (_tabFemaleToggle != null)
                _tabFemaleToggle.onValueChanged.AddListener(isOn => { if (isOn) _genderFilterService.SetGenderFilter(CharacterGenderFilter.Female); });
            if (_tabMaleToggle != null)
                _tabMaleToggle.onValueChanged.AddListener(isOn => { if (isOn) _genderFilterService.SetGenderFilter(CharacterGenderFilter.Male); });
            UpdateGenderFilterVisuals();

            // 페이퍼돌 이벤트
            if (_paperdollUI != null)
            {
                _paperdollUI.OnUnequipRequested += HandlePaperdollUnequip;
                _paperdollUI.OnSlotSelected += HandlePaperdollSlotSelected;
                _paperdollUI.OnTabChanged += HandlePaperdollTabChanged;
            }

            if (!_openRequested) // 아직 아무도 안 열었을 때만 닫아 둔다. (Toggle이라 UI close로 시작시 첫번째 open 입력이 안먹히는 경우를 방지)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnDestroy()
        {
            if (_inventoryProvider != null)
                _inventoryProvider.OnProvided -= HandleProvided;   // 닫혀 있는 동안은 신호를 받지 않는다

            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandStateChanged -= OnWeaponStateChanged;

            if (_appearanceService != null)
                _appearanceService.OnAppearanceChanged -= OnAppearanceStateChanged;

            _genderFilterSubscription?.Dispose();

            if (_paperdollUI != null)
            {
                _paperdollUI.OnUnequipRequested -= HandlePaperdollUnequip;
                _paperdollUI.OnSlotSelected -= HandlePaperdollSlotSelected;
                _paperdollUI.OnTabChanged -= HandlePaperdollTabChanged;
            }
        }

        private void HandleProvided() => Open();   // 인벤토리가 뒤늦게 도착하면 구독과 목록을 다시 잡는다
        
        public void Open()
        {
            if (_inventoryProvider?.Inventory == null) return;   // 캐릭터 도착 전에는 열지 않는다

            _openRequested = true;          // Start()가 아직 안 돌았어도 "열려고 한 것"을 기억
            gameObject.SetActive(true);
            SetBackdropActive(true);

            if (_currentTab == InventoryCategoryTab.All && (_tabAllButton == null || !_tabAllButton.gameObject.activeInHierarchy))
            {
                _currentTab = InventoryCategoryTab.Equipment;
            }
            UpdateTabVisuals();

            if (_inventoryProvider != null)
            {
                _inventoryProvider.OnProvided -= HandleProvided;   // 이미 구독 중이어도 안전하게 먼저 해제
                _inventoryProvider.OnProvided += HandleProvided;   // 뒤늦게 인벤토리가 도착하면 Open()을 다
            }

            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged; // Open()이 중복 호출되어도 이중 구독 방지
            _subscribedInventory = _inventoryProvider.Inventory;
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged += OnInventoryChanged; // 열려있는 동안만 구독 — 상점/바닥획득 등 다른 화면에서 바뀐 것도 즉시 반영

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandStateChanged -= OnWeaponStateChanged;
            _subscribedWeaponActions = _inventoryProvider.WeaponActions;
            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandStateChanged += OnWeaponStateChanged;

            if (_appearanceService != null)
            {
                _appearanceService.OnAppearanceChanged -= OnAppearanceStateChanged;
                _appearanceService.OnAppearanceChanged += OnAppearanceStateChanged;
            }

            // 성별 필터 변화 구독
            _genderFilterSubscription?.Dispose();
            _genderFilterSubscription = _genderFilterService.OnGenderFilterChanged.Subscribe(OnGenderFilterChanged);

            Refresh();
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;

            if (_inventoryProvider != null)
                _inventoryProvider.OnProvided -= HandleProvided;   // 닫혀 있는 동안은 신호를 받지 않는다

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
                    // 선택된 슬롯이 원래 속한 탭(SlotToTab[selected], 가운데 착용슬롯의 종류)이 이동할 탭(tab, 카테고리 버튼)과 다르면
                    if (_paperdollUI.CurrentSelectedSlotType is EquipmentSlotType selected && SlotToTab[selected] != tab)
                        _paperdollUI.ClearSelection(); //하이라이트 해제

                    if (tab == InventoryCategoryTab.Equipment && _paperdollUI.CurrentTab != PaperdollTab.Armor)
                        _paperdollUI.SetTab(PaperdollTab.Armor);
                    else if (tab == InventoryCategoryTab.Accessory && _paperdollUI.CurrentTab != PaperdollTab.Accessory)
                        _paperdollUI.SetTab(PaperdollTab.Accessory);
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

        private void UpdateGenderFilterVisuals()
        {
            var current = _genderFilterService.CurrentFilter;

            _tabAllGenderToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.All);
            _tabFemaleToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.Female);
            _tabMaleToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.Male);
        }

        private void OnGenderFilterChanged(CharacterGenderFilter filter)
        {
            UpdateGenderFilterVisuals();
            Refresh();
        }

        // 아이템 필터링 로직 (Refresh 호출 시)
        private bool ShouldShowItem(EquipmentItem item)
        {
            if (_genderFilterService.CurrentFilter == CharacterGenderFilter.All)
                return true;

            // 아이템 이름의 "_" 구분 토큰 중 "Female"/"Male"이 정확히 있는지로 판단
            // ("female"은 부분 문자열로 "male"을 포함하므로 Contains만 쓰면 안 됨)
            string itemName = item.name.ToLower();

            if (_genderFilterService.CurrentFilter == CharacterGenderFilter.Female)
                return HasGenderToken(itemName, "female");
            else if (_genderFilterService.CurrentFilter == CharacterGenderFilter.Male)
                return HasGenderToken(itemName, "male");

            return true;
        }

        private static bool HasGenderToken(string lowerName, string token)
        {
            foreach (var part in lowerName.Split('_'))
            {
                if (part == token) return true;
            }
            return false;
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
                // 2.커스터마이징 파츠(방어구 / 악세사리): 어느 탭인지는 GetPartTab이 결정
                // (장비)	  상의, 상완, 하완, 손, 하의, 다리, 무릎(Feet), 머리 덮개
                // (악세사리) 투구 장식, 어깨, 팔꿈치, 등, 허리 장식, 귀
                if (eq.Category == WeaponCategory.None && eq.Restriction != null)
                    return tab == GetPartTab(eq);

                // 3. 무기 탭: 화살통을 제외한 모든 무기
                // (무기)     검, 단검, 도끼, 창, 활, 반지, 팔찌, 목걸이 등
                bool isWeapon = eq.Category != WeaponCategory.None && eq.Category != WeaponCategory.Quiver;
                if (isWeapon)
                    return tab == InventoryCategoryTab.Weapon;
            }

            return false;
        }

        private static InventoryCategoryTab GetPartTab(EquipmentItem item)
        {
            // 아이템 = 택배 상자, 상자에 붙은 이름표(Restriction.Title)가 “ShoulderAttachLeft”
            // 표 ①(PartToSlot) = "이 이름표는 어느 사물함(착용 슬롯)에 들어가나?" → 어깨 사물함
            // 표 ②(SlotToTab) = "그 사물함은 어느 층(탭)에 있나?" → 악세사리 층

            if (Enum.TryParse(item.Restriction.Title, out PartType part)
                        && PartToSlot.TryGetValue(part, out var slot))
                return SlotToTab[slot];

            Debug.LogWarning($"[Inventory] 탭을 알 수 없는 파츠: {item.Title} ({item.Restriction.Title}) → 우선 장비 탭으로 표시");
            return InventoryCategoryTab.Equipment;   // 표에 없는 파츠(Head 등)는 장비 탭
                                                     // (*현재 도달할 일 없음. TODO : 소모품으로 영구 해금하기)
        }


        private void OnInventoryChanged(int index) => Refresh();
        private void OnWeaponStateChanged() => Refresh();
        private void OnAppearanceStateChanged() => Refresh();

        private void Refresh()
        {
            ClearSlots();

            if (_paperdollUI != null)
            {
                var appearance = _inventoryProvider?.Inventory != null ? _appearanceService : null;   // 인벤토리 도착 전엔 착용 슬롯도 비움
                _paperdollUI.Refresh(appearance, _inventoryProvider?.WeaponActions, _subscribedInventory);
            }

            var inv = _inventoryProvider?.Inventory;
            if (inv == null) return;

            for (int i = 0; i < inv.MaxSlots; i++)
            {
                var stack = inv.Get(i); // 특정 슬롯의 아이템을 읽어오는 Getter 메서드
                if (stack?.Source == null) continue; // 빈 슬롯은 표시 안 함 — 보유한 것만 보여주는 게 이번 Phase의 핵심 결정

                // 1. 성별 필터링: 남/녀/모두 토글로 선택한 성별만 표시
                if (stack.Source is EquipmentItem equipItem && !ShouldShowItem(equipItem))
                    continue;

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
        // 순서: 커스터마이징 파츠(장비/악세사리) → 무기 → (그 외) 소모품 사용
        // 화살통(Quiver)은 어느 분기에도 안 걸린다 — 장착 버튼 자체가 없음(isEquippable에서 제외)
        private System.Action ResolveEquipAction(RootItem source, int slotIndex)
        {
            // 1. 커스터마이징 파츠(장비/방어구, 악세사리)면 파츠 장착 / 해제
            if (source is EquipmentItem partItem
                && partItem.Category == WeaponCategory.None
                && partItem.Restriction != null)
                return () =>
                {
                    bool alreadyEquipped = _appearanceService != null && _appearanceService.IsPartEquipped(partItem);
                    Debug.Log($"[Equip] 파츠 {(alreadyEquipped ? "해제" : "장착")} 클릭: {partItem.Title}");
                 
                    if (alreadyEquipped)
                    {
                        _appearanceService?.UnequipWearableItem(partItem);
                        _paperdollUI?.ClearSelection();   // 해제하면 하이라이트도 끈다
                    }
                    else
                    {
                        _appearanceService?.EquipWearableItem(partItem);
                        if (_appearanceService != null
                            && _appearanceService.TryGetPartType(partItem, out var part)
                            && PartToSlot.TryGetValue(part, out var slotType)) // TryGetValue로 조회하는 이유 : 머리카락, 얼굴, 눈썹 같은 파츠는 페이퍼돌 슬롯이 없어서 표에 없는 것이 정상이기 때문
                            _paperdollUI?.HighlightSlot(slotType);   // 장착한 슬롯만 표시
                    }
                };

            // 2. 무기면 무기 장착 / 해제
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
                    {
                        _paperdollUI?.ClearSelection();
                        _inventoryProvider.WeaponActions?.UnequipMainHand();
                    }
                    else
                    {
                        _inventoryProvider.WeaponActions?.EquipMainHand(dbKey);
                        _paperdollUI?.HighlightSlot(EquipmentSlotType.Weapon); // 장착/해제 모두 "방금 건드린 슬롯"을 표시
                    }
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
            _paperdollUI?.ClearSelection();   // 슬롯을 눌러 해제해도 하이라이트를 끈다
            Refresh();
        }

        // "(페이퍼돌)슬롯이 선택됐다"는 알림을 받았을 때 인벤토리 탭을 그 슬롯의 탭으로 옮기는 함수
        private void HandlePaperdollSlotSelected(EquipmentSlotType slotType)
        {
            SetTab(SlotToTab[slotType]);
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