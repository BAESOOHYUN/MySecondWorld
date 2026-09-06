using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Customization;

namespace MySecondWorld.UI
{
    public enum EquipmentSlotType
    {
        Head,       // 투구 / 머리
        Torso,      // 갑옷 / 상의
        Hands,      // 장갑
        Legs,       // 하의
        Feet,       // 신발 / 무릎
        Weapon,     // 주무기
        Shield,     // 방패 / 보조장비
        Accessory   // 악세사리 / 등 장식
    }

    /// <summary>
    /// 캐릭터 주변에 공중에 떠 있는 8개 페이퍼돌 장비 슬롯 뷰.
    /// 슬롯 클릭 시 장착 해제 및 정보 조회 이벤트를 발생시킵니다.
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

            [NonSerialized] public EquipmentItem CurrentItem;

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
                        IconImage.color = new Color(1f, 1f, 1f, 0.35f);
                    }
                }

                if (Button != null)
                {
                    Button.onClick.RemoveAllListeners();
                    Button.onClick.AddListener(() => onClick?.Invoke(SlotType));
                }
            }

            public void SetSelected(bool selected)
            {
                if (SelectedBorder != null)
                    SelectedBorder.SetActive(selected);
            }
        }

        [Header("8개 페이퍼돌 장비 슬롯 리스트")]
        [SerializeField] private List<PaperdollSlotView> _slots = new();

        public event Action<EquipmentSlotType, EquipmentItem> OnSlotSelected;
        private EquipmentSlotType? _currentSelectedSlot;

        public EquipmentSlotType? CurrentSelectedSlot => _currentSelectedSlot;

        /// <summary>
        /// 외형 서비스 및 무기 액션 프로바이더로부터 현재 착용 중인 장비들을 조회하여 슬롯을 갱신합니다.
        /// </summary>
        public void Refresh(CharacterAppearanceService appearanceService, IWeaponEquipActionsProvider weaponActions)
        {
            foreach (var slot in _slots)
            {
                EquipmentItem item = ResolveEquippedItem(slot.SlotType, appearanceService, weaponActions);
                slot.Bind(item, HandleSlotClick);
                slot.SetSelected(_currentSelectedSlot == slot.SlotType);
            }
        }

        public List<EquipmentItem> GetAllEquippedItems(CharacterAppearanceService appearanceService, IWeaponEquipActionsProvider weaponActions)
        {
            var list = new List<EquipmentItem>();
            foreach (var slot in _slots)
            {
                var item = ResolveEquippedItem(slot.SlotType, appearanceService, weaponActions);
                if (item != null) list.Add(item);
            }
            return list;
        }

        public EquipmentItem GetEquippedItem(EquipmentSlotType slotType, CharacterAppearanceService appearanceService, IWeaponEquipActionsProvider weaponActions)
        {
            return ResolveEquippedItem(slotType, appearanceService, weaponActions);
        }

        public void SelectSlot(EquipmentSlotType slotType)
        {
            _currentSelectedSlot = slotType;
            foreach (var slot in _slots)
            {
                slot.SetSelected(slot.SlotType == slotType);
                if (slot.SlotType == slotType)
                {
                    OnSlotSelected?.Invoke(slot.SlotType, slot.CurrentItem);
                }
            }
        }

        public void ClearSelection()
        {
            _currentSelectedSlot = null;
            foreach (var slot in _slots)
            {
                slot.SetSelected(false);
            }
        }

        private void HandleSlotClick(EquipmentSlotType slotType)
        {
            if (_currentSelectedSlot == slotType)
            {
                ClearSelection();
                OnSlotSelected?.Invoke(slotType, null);
            }
            else
            {
                SelectSlot(slotType);
            }
        }

        private EquipmentItem ResolveEquippedItem(
            EquipmentSlotType slotType, 
            CharacterAppearanceService appearanceService, 
            IWeaponEquipActionsProvider weaponActions)
        {
            if (appearanceService == null) return null;

            switch (slotType)
            {
                case EquipmentSlotType.Head:
                    return appearanceService.GetEquippedItem(PartType.HeadCoverings) 
                        ?? appearanceService.GetEquippedItem(PartType.HelmetAttachment);

                case EquipmentSlotType.Torso:
                    return appearanceService.GetEquippedItem(PartType.Torso);

                case EquipmentSlotType.Hands:
                    return appearanceService.GetEquippedItem(PartType.HandRight) 
                        ?? appearanceService.GetEquippedItem(PartType.HandLeft);

                case EquipmentSlotType.Legs:
                    return appearanceService.GetEquippedItem(PartType.LegRight) 
                        ?? appearanceService.GetEquippedItem(PartType.LegLeft)
                        ?? appearanceService.GetEquippedItem(PartType.Hips);

                case EquipmentSlotType.Feet:
                    return appearanceService.GetEquippedItem(PartType.KneeAttachRight)
                        ?? appearanceService.GetEquippedItem(PartType.KneeAttachLeft);

                case EquipmentSlotType.Accessory:
                    return appearanceService.GetEquippedItem(PartType.BackAttachment)
                        ?? appearanceService.GetEquippedItem(PartType.Ear);

                case EquipmentSlotType.Weapon:
                    // 무기는 WeaponActions에서 현재 장착된 dbKey와 일치하는 아이템을 찾아야 함
                    return null; // InventoryUI에서 전체 아이템 중 매칭하여 전달

                case EquipmentSlotType.Shield:
                    return null;

                default:
                    return null;
            }
        }
    }
}
