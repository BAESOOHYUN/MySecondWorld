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

        [Header("버튼")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton; // 허공(배경) 클릭 시 닫기
        [SerializeField] private Button _confirmBackdropButton; // 확인 팝업 허공 클릭 시 팝업 닫기

        private LocalPlayerInventoryProvider _inventoryProvider;
        private readonly List<ShopSlotButton> _spawnedSlots = new();
        private EquipmentItem _pendingPurchase; // 구매 대기중인 장비 아이템

        public Observable<Unit> OnClose => _backdropButton != null
            ? Observable.Merge(_closeButton.OnClickAsObservable().AsUnitObservable(), _backdropButton.OnClickAsObservable().AsUnitObservable())
            : _closeButton.OnClickAsObservable().AsUnitObservable();

        [Inject]
        public void Construct(LocalPlayerInventoryProvider inventoryProvider)
        {
            _inventoryProvider = inventoryProvider;
        }

        private void Start()
        {
            _closeButton.onClick.AddListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Close);

            _confirmYesButton.onClick.AddListener(ConfirmPurchase);
            _confirmNoButton.onClick.AddListener(() => _confirmPanel.SetActive(false));
            if (_confirmBackdropButton != null)
                _confirmBackdropButton.onClick.AddListener(() => _confirmPanel.SetActive(false));

            _confirmPanel.SetActive(false);
            _canvas.enabled = false;
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
        }

        public void Close()
        {
            _confirmPanel.SetActive(false);
            _canvas.enabled = false;
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

        // 항목 클릭 → 구매 확인 팝업 표시 (즉시 구매하지 않음)
        private void RequestPurchase(EquipmentItem item)
        {
            _pendingPurchase = item;
            string costName = _costItem != null ? _costItem.name : "대가 아이템";
            _confirmText.text = $"{item.name} 구매 — {costName} 1개 소모";
            _confirmPanel.SetActive(true);
        }

        // 구매 확인
        private void ConfirmPurchase()
        {
            _confirmPanel.SetActive(false);
            if (_pendingPurchase == null || _costItem == null) return;
            
            _inventoryProvider.Actions?.TradeItem(_costItem.GetDbKey(), _pendingPurchase.GetDbKey());
            _pendingPurchase = null; // 대가 아이템 소모
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
