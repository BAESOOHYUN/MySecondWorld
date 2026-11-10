using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using R3;
using VContainer;
using Cleverous.VaultInventory;
using MySecondWorld.AI;
using MySecondWorld.Customization;
using MySecondWorld.Inventory;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Network;

namespace MySecondWorld.UI
{
    // 상점 창. 카탈로그 표시 → 구매 확인 팝업 → 서버에 교환(TradeItem) 요청.
    // 판매는 상점 모드로 열린 인벤토리 UI가 RequestSell을 불러 같은 확인 팝업을 쓴다.
    public class ShopUI : MonoBehaviour, IShopUI
    {
        [Header("패널")]
        [SerializeField] private Canvas _canvas;

        [Header("카탈로그")]
        [SerializeField] private Transform _slotContent; // GridLayoutGroup 부착
        [SerializeField] private ShopSlotButton _slotButtonPrefab;

        [Header("물물교환 대가")]
        [Tooltip("구매·판매에 공통으로 쓰이는 대가(통화) 아이템. 구매하면 1개를 내고, 판매하면 1개를 받는다")]
        [SerializeField] private RootItem _costItem;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton; // 허공(배경) 클릭 시 닫기

        [Header("성별 필터 탭")]
        [SerializeField] private Toggle _shopTabAllGenderToggle;
        [SerializeField] private Toggle _shopTabFemaleToggle;
        [SerializeField] private Toggle _shopTabMaleToggle;

        #region 필드
        // ── DI ──
        private LocalPlayerInventoryProvider _inventoryProvider;
        private InventoryUI _inventoryUI;
        private CharacterAppearanceService _appearanceService;
        private CharacterGenderFilterService _genderFilterService;
        private IUIManager _uiManager;
        private ConfirmPanelUI _confirmPanel; // 독립 캔버스(높은 소트오더)로 UIManager가 지연 생성

        // ── 카탈로그·슬롯 ──
        private readonly List<ShopSlotButton> _spawnedSlots = new();
        private IReadOnlyList<EquipmentItem> _lastCatalog; // 상점이 열려있는 동안 토글이 바뀌면 이 원본 카탈로그를 다시 필터링

        // ── 거래 대기 (확인 팝업에서 "확정"을 누를 때까지 기억) ──
        private EquipmentItem _pendingPurchase; // 구매 대기중인 장비 아이템
        private EquipmentItem _pendingSale; // 판매 대기중인 장비 아이템
        private int _pendingSaleSlotIndex = -1;

        // ── 열림 상태·이벤트 ──
        private int _openedFrame = -1; // Open()과 같은 프레임에 들어온 잔여 클릭(백드롭 등)으로 즉시 닫히는 것 방지
        private readonly Subject<Unit> _onCloseSubject = new();
        private IDisposable _genderFilterSubscription; // 인벤토리 쪽에서 필터가 바뀌어도 상점 토글이 따라 바뀌게

        public Observable<Unit> OnClose => _onCloseSubject;
        public bool IsOpen => _canvas != null && _canvas.enabled;
        #endregion

        #region 생명주기

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider inventoryProvider,
            InventoryUI inventoryUI = null,
            CharacterAppearanceService appearanceService = null,
            CharacterGenderFilterService genderFilterService = null,
            IUIManager uiManager = null)
        {
            _inventoryProvider = inventoryProvider;
            _inventoryUI = inventoryUI;
            _appearanceService = appearanceService;
            _genderFilterService = genderFilterService;
            _uiManager = uiManager;
        }

        private void Start()
        {
            // 상점 닫기 버튼
            _closeButton.onClick.AddListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Close);

            // 구매 확인 팝업 — UIManager 동적 캔버스로 지연 생성 (독립 소트오더)
            _confirmPanel = _uiManager?.GetOrCreateDynamicCanvas<ConfirmPanelUI>()?.GetComponent<ConfirmPanelUI>();

            // 성별 필터 토글
            if (_shopTabAllGenderToggle != null)
                _shopTabAllGenderToggle.onValueChanged.AddListener(isOn => { if (isOn) _genderFilterService?.SetGenderFilter(CharacterGenderFilter.All); });
            if (_shopTabFemaleToggle != null)
                _shopTabFemaleToggle.onValueChanged.AddListener(isOn => { if (isOn) _genderFilterService?.SetGenderFilter(CharacterGenderFilter.Female); });
            if (_shopTabMaleToggle != null)
                _shopTabMaleToggle.onValueChanged.AddListener(isOn => { if (isOn) _genderFilterService?.SetGenderFilter(CharacterGenderFilter.Male); });

            _genderFilterSubscription = _genderFilterService?.OnGenderFilterChanged.Subscribe(_ =>
            {
                UpdateGenderFilterVisuals();
                if (IsOpen)
                    RefreshCatalog(FilterCatalogByGender(_lastCatalog)); // 열려있는 동안 토글이 바뀌면 즉시 다시 필터링
            });
            UpdateGenderFilterVisuals();

            if (_inventoryUI != null)
                _inventoryUI.OnClosed += OnInventoryClosedExternally;

            _canvas.enabled = false;
        }

        private void Update()
        {
            if (_canvas == null || !_canvas.enabled || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (_confirmPanel != null && _confirmPanel.IsOpen) return; // ConfirmPanelUI가 자체적으로 Escape 처리

            Close();
        }

        private void OnDestroy()
        {
            if (_inventoryUI != null)
                _inventoryUI.OnClosed -= OnInventoryClosedExternally;

            _genderFilterSubscription?.Dispose();
            _onCloseSubject.Dispose();
        }

        #endregion

        #region 열기·닫기

        public void Open(IReadOnlyList<EquipmentItem> catalog)
        {
            _openedFrame = Time.frameCount;
            _canvas.enabled = true;
            _lastCatalog = catalog;

            // 성별 필터 적용된 카탈로그로 표시
            var filteredCatalog = FilterCatalogByGender(catalog);
            RefreshCatalog(filteredCatalog);

            if (_inventoryUI != null)
            {
                _inventoryUI.Open(); // 이미 열려 있어도 다시 부른다 (중복 호출 처리는 InventoryUI 쪽)
                _inventoryUI.SetTab(InventoryCategoryTab.All);
                _inventoryUI.SetBackdropActive(false);
                _inventoryUI.SetShopMode(this);
            }
            else
            {
                Debug.LogWarning("[ShopUI] InventoryUI가 주입되지 않아 상점과 함께 열 수 없습니다.");
            }
        }

        public void Close()
        {
            if (_canvas != null && !_canvas.enabled) return;
            if (Time.frameCount == _openedFrame) return; // 상점이 열린 바로 그 프레임의 Close 요청은 잔여 클릭이므로 무시

            _confirmPanel?.Hide();

            if (_canvas != null)
                _canvas.enabled = false;

            if (_inventoryUI != null)
            {
                _inventoryUI.SetShopMode(null);
                if (_inventoryUI.IsOpen)
                    _inventoryUI.Close();
            }

            _onCloseSubject.OnNext(Unit.Default);
        }

        private void OnInventoryClosedExternally()
        {
            if (_canvas != null && _canvas.enabled)
            {
                Close();
            }
        }

        #endregion

        #region 카탈로그

        private void RefreshCatalog(IReadOnlyList<EquipmentItem> catalog)
        {
            ClearSlots();

            if (catalog == null) return;

            foreach (var item in catalog)
            {
                if (item == null) continue;
                var capturedItem = item;
                var slot = Instantiate(_slotButtonPrefab, _slotContent);
                slot.Setup(item.UiIcon, onBuy: () => RequestPurchase(capturedItem));
                slot.SetGenderBadge(_appearanceService?.GetItemGender(item));
                _spawnedSlots.Add(slot);
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

        #endregion

        #region 구매·판매

        // 상점 카탈로그 항목 클릭 → 구매 확인 팝업 표시
        private void RequestPurchase(EquipmentItem item)
        {
            if (item == null) return;
            _pendingSale = null;
            _pendingPurchase = item;

            // 아이템 이름은 Title, 대가 이름은 _costItem.Title (없으면 "코인")
            string costName = _costItem != null ? _costItem.Title : "코인";
            string message = $"<color=#FFE066>{item.Title}</color> 구매\n<size=13><color=#AAAAAA>비용: {costName} 1개 소모</color></size>";

            _confirmPanel?.Show("구매 확인", message, "구매", ConfirmTrade);
        }

        // 인벤토리 가방 아이템 클릭 → 판매 확인 팝업 표시
        public void RequestSell(EquipmentItem item, int inventorySlotIndex)
        {
            if (item == null) return;
            _pendingPurchase = null;
            _pendingSale = item;
            _pendingSaleSlotIndex = inventorySlotIndex;   // 서버에 보낼 판매 칸 번호 기억

            string currencyName = _costItem != null ? _costItem.Title : "코인";
            string message = $"<color=#FFE066>{item.Title}</color> 판매\n<size=13><color=#AAAAAA>보상: {currencyName} 1개 획득</color></size>";
            
            _confirmPanel?.Show("판매 확인", message, "판매", ConfirmTrade);
        }

        // 확인 팝업의 확정 버튼 — 대기 중인 구매 또는 판매를 서버에 요청한다
        private void ConfirmTrade()
        {
            if (_pendingPurchase != null && _costItem != null)
            {
                // 구매: 대가 아이템을 내고 장비를 받는다
                _inventoryProvider.Actions?.TradeItem(_costItem.GetDbKey(), _pendingPurchase.GetDbKey(), -1); //-1은 슬롯 칸을 '모른다/상관없다' 의미
                _pendingPurchase = null;
            }
            else if (_pendingSale != null && _costItem != null)
            {
                // 판매: 장비를 내고 대가 아이템을 받는다
                // 판매할 칸 번호를 서버에 보내고, 서버가 그 칸을 검증해서 뺀다. 장착 칸이 비면 ServerValidateMainHand가 해제
                _inventoryProvider.Actions?.TradeItem(_pendingSale.GetDbKey(), _costItem.GetDbKey(), _pendingSaleSlotIndex);
                _pendingSale = null;
                _pendingSaleSlotIndex = -1;   // 다 쓴 값은 초기화
            }
        }

        #endregion

        #region 성별 필터

        private void UpdateGenderFilterVisuals()
        {
            if (_genderFilterService == null) return;

            var current = _genderFilterService.CurrentFilter;

            _shopTabAllGenderToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.All);
            _shopTabFemaleToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.Female);
            _shopTabMaleToggle?.SetIsOnWithoutNotify(current == CharacterGenderFilter.Male);
        }

        private IReadOnlyList<EquipmentItem> FilterCatalogByGender(IReadOnlyList<EquipmentItem> catalog)
        {
            if (_genderFilterService == null || _genderFilterService.CurrentFilter == CharacterGenderFilter.All)
                return catalog;

            var filtered = new List<EquipmentItem>();
            foreach (var item in catalog)
            {
                if (item == null) continue;
                if (GenderFilterMatcher.Matches(_genderFilterService.CurrentFilter, _appearanceService.GetItemGender(item)))
                    filtered.Add(item);
            }

            return filtered;
        }

        #endregion
    }
}
