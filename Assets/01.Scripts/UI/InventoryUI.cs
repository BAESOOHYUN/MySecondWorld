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
    public class InventoryUI : MonoBehaviour
    {
        [Header("슬롯")]
        [SerializeField] private Transform _slotContent; // GridLayoutGroup 부착
        [SerializeField] private InventorySlotButton _slotButtonPrefab;

        [Header("버튼")]
        [SerializeField] private Button _closeButton;

        private LocalPlayerInventoryProvider _inventoryProvider;
        private CharacterAppearanceService _appearanceService;
        private readonly List<InventorySlotButton> _spawnedSlots = new();


        // Open() 시점에 구독한 인스턴스를 기억 — Close() 때 다른 인스턴스로 바뀌어도 정확히 해당 구독만 해제
        private Cleverous.VaultInventory.Inventory _subscribedInventory;

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider inventoryProvider,
            CharacterAppearanceService appearanceService
            )
        {
            _inventoryProvider = inventoryProvider;
            _appearanceService = appearanceService;
        }

        private void Start()
        {
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            _subscribedInventory = _inventoryProvider.Inventory;
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged += OnInventoryChanged; // 열려있는 동안만 구독 — 상점/바닥획득 등 다른 화면에서 바뀐 것도 즉시 반영

            Refresh();
        }

        private void Close()
        {
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= OnInventoryChanged;
            _subscribedInventory = null;
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
                if (stack?.Source == null) continue; // 빈 슬롯은 표시 안 함 — 보유한 것만 보여주는 게 이번 Phase의 핵심 결정

                int capturedIndex = i;
                var slot = Instantiate(_slotButtonPrefab, _slotContent);

                bool isConsumable = stack.Source is ConsumableItem; // 소모품(포션 등)인지 판별
                bool isEquippable = stack.Source is EquipmentItem eq
                                    && eq.Category != WeaponCategory.Quiver
                                    && (eq.Category != WeaponCategory.None || eq.Restriction != null);
                slot.Setup(
                   stack.Source.UiIcon,
                   stack.StackSize,
                   isEquippable,
                   isConsumable,
                   onDiscard: () => _inventoryProvider.Actions?.DiscardItem(capturedIndex), // UI에서 "버리기" 버튼 클릭 시 그 슬롯의 아이템을 '전량 제거'.
                   onAction: ResolveEquipAction(stack.Source, capturedIndex));

                _spawnedSlots.Add(slot);
            }
        }

        // 파츠 아이템 즉시 장착 : CharacterAppearanceService가 좌우 페어링, 세이브, 동기화까지 일괄 처리
        private void EquipPart(EquipmentItem item)
        {
            _appearanceService.EquipWearableItem(item);
        }

        // 클릭한 아이템 종류에 따라 실제로 무슨 액션을 실행할지 결정.
        // 우선순위: 화살통 > 무기/액세서리 > 커스터마이징 파츠 > (그 외) 소모품 사용
        private System.Action ResolveEquipAction(RootItem source, int slotIndex)
        {
            // 무기면 무기 장착
            if (source is EquipmentItem weaponItem 
                && weaponItem.Category != WeaponCategory.None
                && weaponItem.Category != WeaponCategory.Quiver
                )
                return () =>
                {
                    Debug.Log($"[Equip] 무기 장착 클릭: " +
                        $"{weaponItem.Title} dbKey={weaponItem.GetDbKey()} Category={weaponItem.Category} " +
                        $"/ WeaponActions={(_inventoryProvider.WeaponActions != null ? "OK" : "NULL!!")}");
                    _inventoryProvider.WeaponActions?.EquipMainHand(weaponItem.GetDbKey());
                };

            // 커스터마이징 파츠면 파츠 장착
            if (source is EquipmentItem partItem && partItem.Restriction != null)
                return () => EquipPart(partItem);

            // 소모품이면 "사용"
            return () =>  _inventoryProvider.Actions?.UseItem(slotIndex);
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