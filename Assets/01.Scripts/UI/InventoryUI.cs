using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cleverous.VaultInventory;
using VContainer;
using R3;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Inventory;

namespace MySecondWorld.UI
{
    using Inventory = Cleverous.VaultInventory.Inventory;

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
        #region 상수 · 정적 데이터

        // 슬롯 → 탭 : 이 슬롯은 어느 탭에 속하나 (이미 있음)
        private static readonly Dictionary<EquipmentSlotType, InventoryCategoryTab> SlotToTab = new()
        {
            { EquipmentSlotType.HeadCoverings,    InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Torso,            InventoryCategoryTab.Equipment },
            { EquipmentSlotType.ArmUpper,         InventoryCategoryTab.Equipment },
            { EquipmentSlotType.ArmLower,         InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Hands,            InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Hips,             InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Knees,            InventoryCategoryTab.Equipment },
            { EquipmentSlotType.Legs,             InventoryCategoryTab.Equipment },
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

        // 파츠 → 슬롯 : 이 파츠는 어느 슬롯에 들어가나 (새로 만듦)
        private static readonly Dictionary<PartType, EquipmentSlotType> PartToSlot = new()
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
            { PartType.KneeAttachLeft,    EquipmentSlotType.Knees },
            { PartType.KneeAttachRight,   EquipmentSlotType.Knees },
            { PartType.LegLeft,           EquipmentSlotType.Legs },
            { PartType.LegRight,          EquipmentSlotType.Legs },
            { PartType.HelmetAttachment,  EquipmentSlotType.HelmetAttachment },
            { PartType.ShoulderAttachLeft,  EquipmentSlotType.ShoulderAttach },
            { PartType.ShoulderAttachRight, EquipmentSlotType.ShoulderAttach },
            { PartType.ElbowAttachLeft,   EquipmentSlotType.ElbowAttach },
            { PartType.ElbowAttachRight,  EquipmentSlotType.ElbowAttach },
            { PartType.BackAttachment,    EquipmentSlotType.BackAttachment },
            { PartType.HipsAttachment,    EquipmentSlotType.HipsAttachment },
            { PartType.Ear,               EquipmentSlotType.Ear },
        };

        #endregion

        #region 인스펙터 설정

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

        #endregion

        #region 상태

        // ── 화면 상태 ──
        private readonly List<InventorySlotButton> _spawnedSlots = new();
        private InventoryCategoryTab _currentTab = InventoryCategoryTab.All;
        private ShopUI _activeShop;
        private bool _isChangingTab;
        private bool _openRequested; // 이 UI가 열린적이 있는지 체크

        // ── 구독 기억 ──
        // Open() 시점에 구독한 인스턴스를 기억 — Close() 때 다른 인스턴스로 바뀌어도 정확히 해당 구독만 해제
        private Inventory _subscribedInventory;
        private IWeaponEquipActionsProvider _subscribedWeaponActions;
        private IDisposable _genderFilterSubscription;

        #endregion

        #region 공개 프로퍼티 · 이벤트

        public bool IsOpen => gameObject.activeSelf;
        public event Action OnClosed;

        #endregion

        #region DI

        // 주입받는 참조 — Construct에서만 채워진다
        private LocalPlayerInventoryProvider _inventoryProvider;
        private CharacterAppearanceService _appearanceService;
        private CharacterGenderFilterService _genderFilterService;

        // VContainer가 이 UI를 만든 직후 필요한 서비스 3개를 넣어 주는 진입점 (인벤토리 제공자,
        // 외형 서비스, 성별 필터 서비스)
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

        #endregion

        #region 생명주기

        // 시작할 때 한 번: 버튼·토글·페이퍼돌 이벤트를 연결하고,
        // 아직 아무도 안 열었으면 창을 닫아 둔다
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

            // 아직 아무도 안 열었을 때만 닫아 둔다.
            // (Toggle이라 UI close로 시작시 첫번째 open 입력이 안먹히는 경우를 방지)
            if (!_openRequested)
                gameObject.SetActive(false);
        }

        // 매 프레임: 창이 열려 있을 때 ESC를 누르면 닫는다
        private void Update()
        {
            if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // 오브젝트가 파괴될 때: 걸어 둔 모든 구독(인벤토리·무기·외형·성별 필터·페이퍼돌)을 해제해 누수를 막는다
        private void OnDestroy()
        {
            if (_inventoryProvider != null)
                _inventoryProvider.OnProvided -= HandleProvided;   // 닫혀 있는 동안은 신호를 받지 않는다

            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandEquipChanged -= OnMainHandEquipChanged;

            if (_appearanceService != null)
                _appearanceService.OnAppearanceChanged -= OnAppearanceChanged;

            _genderFilterSubscription?.Dispose();

            if (_paperdollUI != null)
            {
                _paperdollUI.OnUnequipRequested -= HandlePaperdollUnequip;
                _paperdollUI.OnSlotSelected -= HandlePaperdollSlotSelected;
                _paperdollUI.OnTabChanged -= HandlePaperdollTabChanged;
            }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════════════
        // 동작 흐름: ① 창을 연다(Open) → ② 알림을 구독한다 → ③ 알림이 오면 목록을 다시 그린다(Refresh)
        //          → ④ 슬롯 버튼을 누르면 장착·해제 → ⑤ 탭을 바꾸면 ③으로 돌아간다
        // ═══════════════════════════════════════════════════════════════════════

        #region ① 열기 · 닫기

        // 인벤토리가 뒤늦게 도착하면 구독과 목록을 다시 잡는다
        private void HandleProvided() => Open();

        // 인벤토리 창을 연다: 필요한 이벤트를 구독하고,
        // 입은 칸 기록을 채운 뒤 목록을 그린다.
        // 캐릭터가 도착하기 전(인벤토리가 없을 때)에는 열지 않는다.
        // 중복 호출돼도 이중 구독이 되지 않게 먼저 해제하고 다시 건다.
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
                // 열려있는 동안만 구독 — 상점/바닥획득 등 다른 화면에서 바뀐 것도 즉시 반영
                _subscribedInventory.OnChanged += OnInventoryChanged;

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandEquipChanged -= OnMainHandEquipChanged;
            _subscribedWeaponActions = _inventoryProvider.WeaponActions;
            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandEquipChanged += OnMainHandEquipChanged;

            if (_appearanceService != null)
            {
                _appearanceService.OnAppearanceChanged -= OnAppearanceChanged;
                _appearanceService.OnAppearanceChanged += OnAppearanceChanged;
            }

            // 성별 필터 변화 구독
            _genderFilterSubscription?.Dispose();
            _genderFilterSubscription = _genderFilterService.OnGenderFilterChanged.Subscribe(OnGenderFilterChanged);

            // 입은 파츠의 칸 번호 기록이 비어 있으면(앱을 새로 시작한 직후) 같은 아이템이 든 첫 칸으로 채운다.
            // 채운 뒤에 목록을 그려야 입은 칸이 "해제"로 표시된다.
            _appearanceService?.RestoreEquippedInventorySlots(_subscribedInventory);
            Refresh();
        }

        // 인벤토리 창을 닫는다: Open()에서 건 구독을 모두 풀고 창을 숨긴 뒤 OnClosed를 알린다
        public void Close()
        {
            if (!gameObject.activeSelf) return;

            if (_inventoryProvider != null)
                _inventoryProvider.OnProvided -= HandleProvided;   // 닫혀 있는 동안은 신호를 받지 않는다

            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;
            _subscribedInventory = null;

            if (_subscribedWeaponActions != null)
                _subscribedWeaponActions.OnMainHandEquipChanged -= OnMainHandEquipChanged;
            _subscribedWeaponActions = null;

            if (_appearanceService != null)
                _appearanceService.OnAppearanceChanged -= OnAppearanceChanged;

            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        // 상점과 함께 열렸는지 기억한다(null이면 상점 모드 아님).
        // 열려 있으면 버튼이 판매 모드로 바뀌도록 목록을 다시 그린다
        public void SetShopMode(ShopUI shop)
        {
            _activeShop = shop;
            if (gameObject.activeSelf)
                Refresh();
        }

        // 창 뒤의 배경 버튼(허공 클릭으로 닫기)을 켜고 끈다
        public void SetBackdropActive(bool active)
        {
            if (_backdropButton != null)
                _backdropButton.gameObject.SetActive(active);
        }

        #endregion

        #region ② 변경 알림 처리 (Open이 구독해 둔 것)

        // 인벤토리의 칸이 바뀔 때마다(판매·바닥 줍기·버리기 등) 호출된다.
        // 입은 파츠의 칸이 비었으면 먼저 벗긴 뒤 목록을 다시 그린다.
        // (무기의 ServerValidateMainHand에 해당하는 검사)
        private void OnInventoryChanged(int index)
        {
            _appearanceService?.ReleasePartsWithEmptiedSlot(_subscribedInventory);
            Refresh();
        }

        // 장착한 무기(또는 그 칸)가 바뀌었다는 알림을 받으면 목록을 다시 그린다
        private void OnMainHandEquipChanged() => Refresh();

        // 외형(입은 파츠)이 바뀌었다는 알림을 받으면 목록을 다시 그린다
        private void OnAppearanceChanged() => Refresh();

        // 성별 필터가 바뀌었다는 알림을 받으면: 토글 표시를 맞추고 목록을 다시 그린다
        private void OnGenderFilterChanged(CharacterGenderFilter filter)
        {
            UpdateGenderFilterVisuals();
            Refresh();
        }

        #endregion

        #region ③ 목록 그리기

        // 인벤토리 목록을 처음부터 다시 그린다: 페이퍼돌을 갱신하고,
        // 성별 필터와 탭을 통과한 아이템마다 슬롯 버튼을 만든다.
        // 슬롯마다 장착 버튼 노출 여부, 장착 중 표시,
        // 버리기·장착 동작을 정해 연결한다.
        private void Refresh()
        {
            ClearSlots();

            if (_paperdollUI != null)
            {
                // 인벤토리 도착 전엔 착용 슬롯도 비움
                var appearance = _inventoryProvider?.Inventory != null ? _appearanceService : null;
                _paperdollUI.Refresh(appearance, _inventoryProvider?.WeaponActions, _subscribedInventory);
            }

            var inv = _inventoryProvider?.Inventory;
            if (inv == null) return;

            for (int i = 0; i < inv.MaxSlots; i++)
            {
                var stack = inv.Get(i); // 특정 슬롯의 아이템을 읽어오는 Getter 메서드
                // 빈 슬롯은 표시 안 함 — 보유한 것만 보여주는 게 이번 Phase의 핵심 결정
                if (stack?.Source == null) continue;

                // 1. 성별 필터링: 남/녀/모두 토글로 선택한 성별만 표시
                if (stack.Source is EquipmentItem equipItem && !ShouldShowItem(equipItem))
                    continue;

                // 2. 카테고리 탭 필터링
                if (!MatchesCategory(stack.Source, _currentTab))
                    continue;

                int capturedIndex = i;
                var slot = Instantiate(_slotButtonPrefab, _slotContent);

                bool isConsumable = stack.Source is ConsumableItem; // 소모품(포션 등)인지 판별

                // 장착 버튼을 보일지 정하는 사전 차단.
                // 파츠(방어구/악세사리)는 부위 정보(Restriction)가 있으면 보이고,
                // 무기는 서버가 허용하는 종류(MainHandRules)만 보여서 서버 거부와 같은 기준을 쓴다.
                // 아이템이 장비인가? ─ 아니오 ─> '장착' 버튼 없음
                bool isEquippable = stack.Source is EquipmentItem eq
                                    // ① 이 아이템은 무기가 아닌가? (= 상의 같은 파츠인가?)
                                    && (eq.Category == WeaponCategory.None
                                        // ② 예: 파츠다 → "어느 부위 파츠인지 정보가 있나"를 본다.
                                        // 부위 정보(Restriction)가 있으면 버튼 보임
                                        ? eq.Restriction != null
                                        // ③ 아니오: 무기다 → "서버가 허용하는 무기 종류인가"를 본다.
                                        // 서버가 허용하는 종류(IsAllowed)면 버튼 보임
                                        : MainHandRules.IsAllowed(eq.Category));

                bool isEquippedWeapon = stack.Source is EquipmentItem weaponCheck
                                    && weaponCheck.Category != WeaponCategory.None
                                    && weaponCheck.Category != WeaponCategory.Quiver
                                    // 장착한 칸 번호로 판단
                                    && _inventoryProvider.WeaponActions?.CurrentMainHandInventorySlotIndex == capturedIndex;

                bool isEquippedPart = stack.Source is EquipmentItem partCheck
                                    && partCheck.Category == WeaponCategory.None
                                    && partCheck.Restriction != null
                                    && _appearanceService != null
                                    && _appearanceService.IsPartEquipped(partCheck, capturedIndex);

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
                           _activeShop.RequestSell(eqItem, capturedIndex);
                           return;
                       }

                       // 로컬 예측: 버리려는 칸이 지금 장착한 칸이면 즉시 로컬 비주얼 해제
                       if (_inventoryProvider.WeaponActions != null
                           && _inventoryProvider.WeaponActions.CurrentMainHandInventorySlotIndex == capturedIndex)
                       {
                           _inventoryProvider.WeaponActions.UnequipMainHand();
                       }

                       // 버리려는 아이템이 현재 착용 중인 파츠(장비/악세)면 즉시 장착 해제
                       if (_appearanceService != null
                           && stack.Source is EquipmentItem discardedPart
                           && discardedPart.Category == WeaponCategory.None
                           && _appearanceService.IsPartEquipped(discardedPart, capturedIndex))
                       {
                           _appearanceService.UnequipWearableItem(discardedPart);
                       }

                       // UI에서 "버리기" 버튼 클릭 시 그 슬롯의 아이템을 '전량 제거'.
                       _inventoryProvider.Actions?.DiscardItem(capturedIndex);
                   },
                   onAction: ResolveEquipAction(stack.Source, capturedIndex),
                   isShopMode: isShopMode);

                _spawnedSlots.Add(slot);
            }
        }

        #endregion

        #region ④ 장착 · 해제 동작 (슬롯 버튼을 눌렀을 때)

        // 클릭한 아이템 종류에 따라 실제로 무슨 액션을 실행할지 결정.
        // 순서: 커스터마이징 파츠(장비/악세사리) → 무기 → (그 외) 소모품 사용
        // 화살통(Quiver)은 어느 분기에도 안 걸린다 — 장착 버튼 자체가 없음(isEquippable에서 제외)
        private System.Action ResolveEquipAction(RootItem source, int inventorySlotIndex)
        {
            // 1. 커스터마이징 파츠(장비/방어구, 악세사리)면 파츠 장착 / 해제
            if (source is EquipmentItem partItem
                && partItem.Category == WeaponCategory.None
                && partItem.Restriction != null)
                return () =>
                {
                    bool alreadyEquipped = _appearanceService != null && _appearanceService.IsPartEquipped(partItem, inventorySlotIndex);
                    Debug.Log($"[Equip] 파츠 {(alreadyEquipped ? "해제" : "장착")} 클릭: {partItem.Title}");

                    if (alreadyEquipped)
                    {
                        _appearanceService?.UnequipWearableItem(partItem);
                        _paperdollUI?.ClearSelection();   // 해제하면 하이라이트도 끈다
                    }
                    else
                    {
                        _appearanceService?.EquipWearableItem(partItem, inventorySlotIndex);
                        if (_appearanceService != null
                            && _appearanceService.TryGetPartType(partItem, out var part)
                            // TryGetValue로 조회하는 이유 : 머리카락, 얼굴,
                            // 눈썹 같은 파츠는 페이퍼돌 슬롯이 없어서 표에 없는 것이 정상이기 때문
                            && PartToSlot.TryGetValue(part, out var slotType))
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
                    bool alreadyEquipped = _inventoryProvider.WeaponActions?.CurrentMainHandInventorySlotIndex == inventorySlotIndex;
                    Debug.Log($"[Equip] 무기 {(alreadyEquipped ? "해제" : "장착")} 클릭: {weaponItem.Title} dbKey={dbKey}");

                    if (alreadyEquipped)
                    {
                        _paperdollUI?.ClearSelection();
                        _inventoryProvider.WeaponActions?.UnequipMainHand();
                    }
                    else
                    {
                        _inventoryProvider.WeaponActions?.EquipMainHand(dbKey, inventorySlotIndex);
                        // 장착/해제 모두 "방금 건드린 슬롯"을 표시
                        _paperdollUI?.HighlightSlot(EquipmentSlotType.Weapon);
                    }
                };

            // 3. 소모품이면 "사용"
            return () => _inventoryProvider.Actions?.UseItem(inventorySlotIndex);
        }

        // 페이퍼돌의 착용 슬롯을 눌러 벗기라고 요청하면: 무기면 무기를,
        // 파츠면 외형을 벗기고 하이라이트를 끈 뒤 다시 그린다
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

        #endregion

        #region ⑤ 탭 전환 · 페이퍼돌 연동

        // 카테고리 탭을 바꾼다: 탭 색을 갱신하고,
        // 페이퍼돌의 하이라이트·탭을 같이 맞춘 뒤 목록을 다시 그린다.
        // 페이퍼돌과 서로 탭을 바꾸라고 주고받다가 무한 반복되지 않게 _isChangingTab으로 막는다.
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
                    // 선택된 슬롯이 원래 속한 탭(SlotToTab[selected],
                    // 가운데 착용슬롯의 종류)이 이동할 탭(tab,
                    // 카테고리 버튼)과 다르면
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

        // "(페이퍼돌)슬롯이 선택됐다"는 알림을 받았을 때 인벤토리 탭을 그 슬롯의 탭으로 옮기는 함수
        private void HandlePaperdollSlotSelected(EquipmentSlotType slotType)
        {
            SetTab(SlotToTab[slotType]);
        }

        // 페이퍼돌의 탭(방어구/악세사리)이 바뀌면 인벤토리 카테고리 탭도 같은 쪽으로 맞춘다
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

        #endregion

        #region 헬퍼 메서드

        // 지금 그려진 슬롯 버튼을 모두 파괴하고 목록을 비운다 (Refresh가 시작할 때 부른다)
        private void ClearSlots()
        {
            foreach (var slot in _spawnedSlots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _spawnedSlots.Clear();
        }

        // 탭 버튼 5개의 색을 현재 선택된 탭 기준으로 다시 칠한다
        private void UpdateTabVisuals()
        {
            SetTabButtonVisual(_tabEquipmentButton, _currentTab == InventoryCategoryTab.Equipment);
            SetTabButtonVisual(_tabAccessoryButton, _currentTab == InventoryCategoryTab.Accessory);
            SetTabButtonVisual(_tabWeaponButton, _currentTab == InventoryCategoryTab.Weapon);
            SetTabButtonVisual(_tabConsumableButton, _currentTab == InventoryCategoryTab.Consumable);
            SetTabButtonVisual(_tabAllButton, _currentTab == InventoryCategoryTab.All);
        }

        // 탭 버튼 하나의 배경색과 글자(색·굵기)를 선택됨/선택 안 됨 상태로 바꾼다
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

        // 이 아이템이 주어진 탭에 속하는가 (소모품·파츠·무기를 구분해 판단한다)
        private bool MatchesCategory(RootItem item, InventoryCategoryTab tab)
        {
            if (tab == InventoryCategoryTab.All) return true;

            // 1. 소모품 탭: ConsumableItem이거나 화살통(Quiver)인 경우
            if (item is ConsumableItem || (item is EquipmentItem quiverItem && quiverItem.Category == WeaponCategory.Quiver))
                return tab == InventoryCategoryTab.Consumable;

            if (item is EquipmentItem eq)
            {
                // 2.커스터마이징 파츠(방어구 / 악세사리): 어느 탭인지는 GetPartTab이 결정
                // (장비)	  상의, 상완, 하완, 손, 하의, 다리, 무릎(Knees), 머리 덮개
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

        // 파츠 아이템이 장비 탭인지 악세사리 탭인지 알려 준다 (파츠 이름표 → 착용 슬롯 → 탭 순으로 표를 두 번 조회)
        private static InventoryCategoryTab GetPartTab(EquipmentItem item)
        {
            // 아이템 = 택배 상자,
            // 상자에 붙은 이름표(Restriction.Title)가 “ShoulderAttachLeft”
            // 표 ①(PartToSlot) = "이 이름표는 어느 사물함(착용 슬롯)에 들어가나?" → 어깨 사물함
            // 표 ②(SlotToTab) = "그 사물함은 어느 층(탭)에 있나?" → 악세사리 층

            if (Enum.TryParse(item.Restriction.Title, out PartType part)
                        && PartToSlot.TryGetValue(part, out var slot))
                return SlotToTab[slot];

            Debug.LogWarning($"[Inventory] 탭을 알 수 없는 파츠: {item.Title} ({item.Restriction.Title}) → 우선 장비 탭으로 표시");
            return InventoryCategoryTab.Equipment;   // 표에 없는 파츠(Head 등)는 장비 탭
                                                     // (*현재 도달할 일 없음.
                                                     // TODO : 소모품으로 영구 해금하기)
        }

        // 성별 필터 토글 3개의 체크 상태를 현재 필터 값에 맞춘다 (이벤트는 일으키지 않는다)
        private void UpdateGenderFilterVisuals()
        {
            var current = _genderFilterService.CurrentFilter;

            _tabAllGenderToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.All);
            _tabFemaleToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.Female);
            _tabMaleToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.Male);
        }

        // 이 아이템을 지금 성별 필터에서 보여줄까? (Refresh가 호출).
        // 이름의 성별 표기로 판단하고, 표기가 없는 공용 아이템은 항상 보여 준다
        private bool ShouldShowItem(EquipmentItem item)
        {
            var filter = _genderFilterService.CurrentFilter;
            if (filter == CharacterGenderFilter.All)
                return true;

            // 아이템 이름의 "_" 구분 토큰 중 "Female"/"Male"이 정확히 있는지로 판단
            // ("female"은 부분 문자열로 "male"을 포함하므로 Contains만 쓰면 안 됨)
            string itemName = item.name.ToLower();
            bool isFemaleOnly = HasGenderToken(itemName, "female");
            bool isMaleOnly = HasGenderToken(itemName, "male");

            // 선택한 성별의 아이템 + 성별 표기가 없는 공용 아이템을 보여 준다
            if (filter == CharacterGenderFilter.Female)
                return isFemaleOnly || !isMaleOnly;
            if (filter == CharacterGenderFilter.Male)
                return isMaleOnly || !isFemaleOnly;

            return true;
        }

        // 이름을 "_"로 쪼갠 조각 중에 token("female"/"male")과 정확히 같은 것이 있는가
        private static bool HasGenderToken(string lowerName, string token)
        {
            foreach (var part in lowerName.Split('_'))
            {
                if (part == token) return true;
            }
            return false;
        }

        #endregion
    }
}
