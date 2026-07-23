using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MessagePipe;
using VContainer;
using MySecondWorld.Customization;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("슬롯")]
        [SerializeField] private Transform _slotContent; // GridLayoutGroup 부착
        [SerializeField] private InventorySlotButton _slotButtonPrefab;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;

        private LocalPlayerInventoryProvider _inventoryProvider;
        private IPublisher<RequestOpenCustomizeUIEvent> _openCustomizeUIPub;
        private readonly List<InventorySlotButton> _spawnedSlots = new();

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider inventoryProvider,
            IPublisher<RequestOpenCustomizeUIEvent> openCustomizeUIPub)
        {
            _inventoryProvider = inventoryProvider;
            _openCustomizeUIPub = openCustomizeUIPub;
        }

        private void Start()
        {
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            var inv = _inventoryProvider.Inventory;
            if (inv != null)
                inv.OnChanged += OnInventoryChanged; // 열려있는 동안만 구독 — 상점/바닥획득 등 다른 화면에서 바뀐 것도 즉시 반영

            Refresh();
        }

        private void Close()
        { 
            var inv = _inventoryProvider.Inventory;
            if (inv != null)
                inv.OnChanged -= OnInventoryChanged;
            gameObject.SetActive(false);
        }

        private void OnInventoryChanged(int index) => Refresh();

        private void Refresh()
        {
            ClearSlots();

            var inv = _inventoryProvider.Inventory;
            if (inv == null) return;

            for (int i = 0; i < inv.MaxSlots; i++)
            {
                var stack = inv.Get(i); // 특정 슬롯의 아이템을 읽어오는 Getter 메서드
                if (stack?.Source ==  null) continue; // 빈 슬롯은 표시 안 함 — 보유한 것만 보여주는 게 이번 Phase의 핵심 결정

                int capturedIndex = i;
                var slot = Instantiate(_slotButtonPrefab, _slotContent);
                bool isPart = stack.Source is EquipmentItem eq && eq.Restriction != null; // 파츠(그룹 A/B/C)인지 판별
                bool isConsumable = stack.Source is ConsumableItem; // 소모품(포션 등)인지 판별

                slot.Setup(
                    stack.Source.UiIcon,
                    stack.StackSize,
                    isPart,
                    isConsumable,
                    onDiscard: () => _inventoryProvider.Actions?.DiscardItem(capturedIndex),    // UI에서 "버리기" 버튼 클릭 시 그 슬롯의 아이템을 '전량 제거'.
                    onAction: isPart
                        ? () => _openCustomizeUIPub.Publish(new RequestOpenCustomizeUIEvent())  // "장착"
                        : () => _inventoryProvider.Actions?.UseItem(capturedIndex));            // "사용"

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
    }
}