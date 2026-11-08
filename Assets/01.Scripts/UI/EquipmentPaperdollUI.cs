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
        // 1. 방어구 / 의상 파츠 (8종)
        HeadCoverings,    // 투구 / 모자
        Torso,            // 상체 / 상의
        ArmUpper,         // 상완 보호구
        ArmLower,         // 하완 보호구
        Hands,            // 손 / 장갑
        Hips,             // 힙 / 하의
        Knees,            // 무릎 각반 (KneeAttach)
        Legs,             // 다리 / 신발 (LegLeft/Right)

        // 2. 악세사리 (5종)
        HelmetAttachment, // 투구 장식
        ShoulderAttach,   // 어깨 장식
        ElbowAttach,      // 팔꿈치 보호구
        BackAttachment,   // 등 장식 (날개/망토)
        HipsAttachment,   // 허리 장식

        // 3. 하단 상시 고정 무기 (2종)
        Weapon,           // 주무기
        Shield            // 보조무기 / 방패 / 화살통
    }

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
            Cleverous.VaultInventory.Inventory inventory)
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
            Cleverous.VaultInventory.Inventory inventory)
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
            Cleverous.VaultInventory.Inventory inventory)
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

        // 슬롯 → 파츠(아이템)	: 이 슬롯에 지금 뭐가 끼워져 있나 (이미 있음)
        private EquipmentItem ResolveEquippedItem(
            EquipmentSlotType slotType, 
            CharacterAppearanceService appearanceService, 
            IWeaponEquipActionsProvider weaponActions,
            Cleverous.VaultInventory.Inventory inventory)
        {
            switch (slotType)
            {
                // 1. 방어구 파츠 8종
                case EquipmentSlotType.HeadCoverings:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.HeadCoverings);

                case EquipmentSlotType.Torso:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.Torso);

                case EquipmentSlotType.ArmUpper:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.ArmUpperLeft) 
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.ArmUpperRight);

                case EquipmentSlotType.ArmLower:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.ArmLowerLeft) 
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.ArmLowerRight);

                case EquipmentSlotType.Hands:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.HandLeft) 
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.HandRight);

                case EquipmentSlotType.Hips:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.Hips);

                case EquipmentSlotType.Knees:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.KneeAttachLeft)
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.KneeAttachRight);

                case EquipmentSlotType.Legs:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.LegLeft)
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.LegRight);

                // 2. 악세사리 파츠 8종
                case EquipmentSlotType.HelmetAttachment:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.HelmetAttachment);

                case EquipmentSlotType.ShoulderAttach:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.ShoulderAttachLeft) 
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.ShoulderAttachRight);

                case EquipmentSlotType.ElbowAttach:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.ElbowAttachLeft) 
                        ?? appearanceService?.GetRequiredItemForCurrentLook(PartType.ElbowAttachRight);

                case EquipmentSlotType.BackAttachment:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.BackAttachment);

                case EquipmentSlotType.HipsAttachment:
                    return appearanceService?.GetRequiredItemForCurrentLook(PartType.HipsAttachment);

                // 3. 하단 상시 고정 무기 2종
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
                    return null;
            }
        }
    }
}
