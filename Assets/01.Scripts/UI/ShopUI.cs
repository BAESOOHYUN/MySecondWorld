using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using VContainer;
using Cleverous.VaultInventory;
using MySecondWorld.AI;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.UI
{
    public class ShopUI : MonoBehaviour, IShopUI
    {
        [SerializeField] private Canvas _canvas;   // ShopCanvas의 Canvas 컴포넌트

        [Header("카탈로그")]
        [SerializeField] private Transform _slotContent; // GridLayoutGroup 부착
        [SerializeField] private ShopSlotButton _slotButtonPrefab;


        [Header("물물교환 대가")]
        [Tooltip("이번 테스트 슬라이스에서 모든 구매에 공통으로 쓰이는 대가 아이템 1개")]
        [SerializeField] private RootItem _costItem;


        [Header("구매 확인 팝업")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _confirmYesButton;
        [SerializeField] private Button _confirmNoButton;
        [SerializeField] private Button _confirmCloseButton;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton; // 허공(배경) 클릭 시 닫기
        [SerializeField] private Button _confirmBackdropButton; // 확인 팝업 허공 클릭 시 팝업 닫기

        private LocalPlayerInventoryProvider _inventoryProvider;
        private InventoryUI _inventoryUI;
        private readonly List<ShopSlotButton> _spawnedSlots = new();
        private EquipmentItem _pendingPurchase; // 구매 대기중인 장비 아이템
        private EquipmentItem _pendingSale;     // 판매 대기중인 장비 아이템
        private readonly Subject<Unit> _onCloseSubject = new();

        public Observable<Unit> OnClose => _onCloseSubject;
        public bool IsOpen => _canvas != null && _canvas.enabled;

        [Inject]
        public void Construct(LocalPlayerInventoryProvider inventoryProvider, InventoryUI inventoryUI = null)
        {
            _inventoryProvider = inventoryProvider;
            _inventoryUI = inventoryUI;
        }

        private void Start()
        {
            _closeButton.onClick.AddListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Close);

            _confirmYesButton.onClick.AddListener(ConfirmPurchase);
            _confirmNoButton.onClick.AddListener(() => _confirmPanel.SetActive(false));
            if (_confirmCloseButton != null)
                _confirmCloseButton.onClick.AddListener(() => _confirmPanel.SetActive(false));
            if (_confirmBackdropButton != null)
                _confirmBackdropButton.onClick.AddListener(() => _confirmPanel.SetActive(false));

            if (_inventoryUI != null)
                _inventoryUI.OnClosed += OnInventoryClosedExternally;

            _confirmPanel.SetActive(false);
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_inventoryUI != null)
                _inventoryUI.OnClosed -= OnInventoryClosedExternally;
            _onCloseSubject.Dispose();
        }

        private void OnInventoryClosedExternally()
        {
            if (_canvas != null && _canvas.enabled)
            {
                Close();
            }
        }

        private void Update()
        {
            if (_canvas != null && _canvas.enabled && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_confirmPanel != null && _confirmPanel.activeSelf)
                    _confirmPanel.SetActive(false);
                else
                    Close();
            }
        }

        public void Open(IReadOnlyList<EquipmentItem> catalog)
        {
            _canvas.enabled = true;
            Refresh(catalog);

            if (_inventoryUI != null)
            {
                if (!_inventoryUI.IsOpen) _inventoryUI.Open();
                _inventoryUI.SetBackdropActive(false); // 상점 열릴 때 단일 백드롭 체제로 전환
                _inventoryUI.SetShopMode(this);        // 인벤토리를 상점 연동 모드로 전환 (판매 지원)
            }
        }

        public void Close()
        {
            if (_canvas != null && !_canvas.enabled) return;

            if (_confirmPanel != null)
                _confirmPanel.SetActive(false);

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

        private void Refresh(IReadOnlyList<EquipmentItem> catalog)
        {
            ClearSlots();

            if (catalog == null) return;

            foreach (var item in catalog)
            {
                if (item == null) continue;
                var capturedItem = item;
                var slot = Instantiate(_slotButtonPrefab, _slotContent);
                slot.Setup(item.UiIcon, onBuy: () => RequestPurchase(capturedItem));
                _spawnedSlots.Add(slot);
            }
        }

        // 상점 카탈로그 항목 클릭 → 구매 확인 팝업 표시
        private void RequestPurchase(EquipmentItem item)
        {
            if (item == null) return;
            _pendingSale = null;
            _pendingPurchase = item;

            string costName = _costItem != null ? _costItem.name : "골드";
            _confirmText.text = $"<color=#FFE066>{item.name}</color> 구매\n<size=13><color=#AAAAAA>비용: {costName} 1개 소모</color></size>";

            var yesText = _confirmYesButton?.GetComponentInChildren<TMP_Text>(true);
            if (yesText != null) yesText.text = "구매";

            _confirmPanel.SetActive(true);
        }

        // 인벤토리 가방 아이템 클릭 → 판매 확인 팝업 표시
        public void RequestSell(EquipmentItem item)
        {
            if (item == null) return;
            _pendingPurchase = null;
            _pendingSale = item;

            string currencyName = _costItem != null ? _costItem.name : "골드";
            _confirmText.text = $"<color=#FFE066>{item.name}</color> 판매\n<size=13><color=#AAAAAA>보상: {currencyName} 1개 획득</color></size>";

            var yesText = _confirmYesButton?.GetComponentInChildren<TMP_Text>(true);
            if (yesText != null) yesText.text = "판매";

            _confirmPanel.SetActive(true);
        }

        // 구매/판매 확정 실행
        private void ConfirmPurchase()
        {
            _confirmPanel.SetActive(false);

            if (_pendingPurchase != null && _costItem != null)
            {
                // 구매: 대가(골드) 지불 -> 장비 획득
                _inventoryProvider.Actions?.TradeItem(_costItem.GetDbKey(), _pendingPurchase.GetDbKey());
                _pendingPurchase = null;
            }
            else if (_pendingSale != null && _costItem != null)
            {
                // 판매: 장비 지불 -> 대가(골드) 획득
                // 장착 중인 무기를 판매하는 경우 로컬 무기 비주얼도 즉시 해제
                if (_inventoryProvider.WeaponActions != null 
                    && _inventoryProvider.WeaponActions.CurrentMainHandDbKey == _pendingSale.GetDbKey())
                {
                    _inventoryProvider.WeaponActions.UnequipMainHand();
                }

                _inventoryProvider.Actions?.TradeItem(_pendingSale.GetDbKey(), _costItem.GetDbKey());
                _pendingSale = null;
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
