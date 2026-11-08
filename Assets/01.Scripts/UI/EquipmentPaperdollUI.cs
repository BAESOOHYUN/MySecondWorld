using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Customization;

namespace MySecondWorld.UI
{
    using Inventory = Cleverous.VaultInventory.Inventory;
    /// <summary>
    /// 캐릭터 주변에 떠 있는 세부 장비 페이퍼돌 뷰.
    /// 방어구 8 + 악세사리 5 + 무기 2, 총 15칸을 한 화면에 항상 표시
    /// </summary>
    public class EquipmentPaperdollUI : MonoBehaviour
    {
        [Serializable]
        public class PaperdollSlotView
        {
            public EquipmentSlotType SlotType;
            public Button Button;
            public Image BackgroundImage;
            public Image IconImage;
            public TMP_Text LabelText;
            public GameObject SelectedBorder;
            public Sprite DefaultPlaceholderIcon;
            [NonSerialized] public EquipmentItem CurrentItem; // 이 칸에 지금 끼워진 아이템

            // 아이콘을 채우고 클릭 연결
            public void Bind(EquipmentItem item, Action<EquipmentSlotType> onClick)
            {
                CurrentItem = item;

                if (IconImage != null)
                {
                    if (item != null && item.UiIcon != null)
                    {
                        IconImage.sprite = item.UiIcon;
                        IconImage.color = Color.white;
                    }
                    else
                    {
                        IconImage.sprite = DefaultPlaceholderIcon;
                        IconImage.color = new Color(1f, 1f, 1f, 0f);
                    }
                }

                if (Button != null)
                {
                    Button.onClick.RemoveAllListeners();
                    Button.onClick.AddListener(() => onClick?.Invoke(SlotType));
                }
            }

            // 선택 테두리 켜고 끄기
            public void SetSelected(bool selected)
            {
                if (SelectedBorder != null)
                    SelectedBorder.SetActive(selected);
            }
        }

        [Header("전체 페이퍼돌 장비 슬롯 리스트")]
        [SerializeField] private List<PaperdollSlotView> _slots = new();
        public event Action<EquipmentSlotType> OnSlotSelected;
        public event Action<EquipmentSlotType, EquipmentItem> OnUnequipRequested;
        private EquipmentSlotType? _currentSelectedSlotType;
        public EquipmentSlotType? CurrentSelectedSlotType => _currentSelectedSlotType;

        /// <summary>
        /// 외형 서비스 및 무기 액션 프로바이더, 인벤토리로부터 현재 착용 중인 장비들을 조회하여 슬롯을 갱신합니다.
        /// </summary>
        public void Refresh(
            CharacterAppearanceService appearanceService, 
            IWeaponEquipActionsProvider weaponActions,
            Inventory inventory)
        {
            foreach (var slot in _slots)
            {
                EquipmentItem item = ResolveEquippedItem(slot.SlotType, appearanceService, weaponActions, inventory);
                slot.Bind(item, HandleSlotClick);
                slot.SetSelected(_currentSelectedSlotType == slot.SlotType);
            }
        }

        public List<EquipmentItem> GetAllEquippedItems(
            CharacterAppearanceService appearanceService, 
            IWeaponEquipActionsProvider weaponActions,
            Inventory inventory)
        {
            var list = new List<EquipmentItem>();
            foreach (var slot in _slots)
            {
                var item = ResolveEquippedItem(slot.SlotType, appearanceService, weaponActions, inventory);
                if (item != null) list.Add(item);
            }
            return list;
        }

        public EquipmentItem GetEquippedItem(
            EquipmentSlotType slotType, 
            CharacterAppearanceService appearanceService, 
            IWeaponEquipActionsProvider weaponActions,
            Inventory inventory)
        {
            return ResolveEquippedItem(slotType, appearanceService, weaponActions, inventory);
        }


        // 알림 없이 하이라이트만 켠다 — 장착 결과를 보여 줄 때 쓴다 (탭은 옮기지 않음)
        public void HighlightSlot(EquipmentSlotType slotType)
        {
            _currentSelectedSlotType = slotType;
            foreach (var slot in _slots)
                slot.SetSelected(slot.SlotType == slotType);
        }

        public void SelectSlot(EquipmentSlotType slotType)
        {
            HighlightSlot(slotType);
            OnSlotSelected?.Invoke(slotType);
        }

        public void ClearSelection()
        {
            _currentSelectedSlotType = null;
            foreach (var slot in _slots)
            {
                slot.SetSelected(false);
            }
        }

        private void HandleSlotClick(EquipmentSlotType slotType)
        {
            var slot = _slots.Find(s => s.SlotType == slotType);
            if (slot == null) return;

            if (_currentSelectedSlotType == slotType)
            {
                if (slot.CurrentItem != null) // 장착 아이템이 있을때
                {
                    // 선택된 장착 슬롯을 다시 누르면 해제
                    OnUnequipRequested?.Invoke(slotType, slot.CurrentItem);
                }
                else                          // 장착 아이템이 없을때
                {
                    // 선택된 빈 슬롯을 다시 누르면 선택 취소
                    ClearSelection();
                    OnSlotSelected?.Invoke(slotType);
                }
                return;
            }
            SelectSlot(slotType); // 처음 누르면 장착 여부와 상관없이 선택
        }

        // 이 슬롯에 지금 어떤 아이템이 끼워져 있는지 조회만 함 (표는 EquipmentSlotMap에 있다)
        // 슬롯 하나는 파츠가 여러 개일 수 있다(왼팔/오른팔, 모자/헬멧).
        // 앞에서부터 보고 아이템이 나온 첫 파츠의 것을 슬롯의 아이콘으로 쓴다. 없으면 빈 칸.
        private EquipmentItem ResolveEquippedItem(
            EquipmentSlotType slotType, 
            CharacterAppearanceService appearanceService, 
            IWeaponEquipActionsProvider weaponActions,
            Inventory inventory)
        {
            switch (slotType)
            {
                case EquipmentSlotType.Weapon:
                    if (weaponActions != null && inventory != null)
                    {
                        // 인벤토리에서 장착한 칸 번호의 아이템을 보여줌
                        int slot = weaponActions.CurrentMainHandInventorySlotIndex;
                        if (slot >= 0 && slot < inventory.MaxSlots
                            && inventory.Get(slot)?.Source is EquipmentItem eq)
                                return eq;
                    }
                    return null;

                case EquipmentSlotType.Shield:
                    return null;

                default:
                    // 방어구·악세사리: 슬롯에 속한 파츠들을 차례로 보고 처음 나온 아이템을 쓴다
                    foreach (var part in EquipmentSlotMap.GetParts(slotType))               // 이 칸의 파츠들을 차례로 보고
                    {
                        var item = appearanceService?.GetRequiredItemForCurrentLook(part);  // 이 파츠에 대응하는 아이템을 찾음
                        if (item != null) return item;                                      // 찾았으면 바로 돌려주고 끝
                    }
                    return null;                                                            // 하나도 없으면 빈 칸
            }
        }
    }
}
