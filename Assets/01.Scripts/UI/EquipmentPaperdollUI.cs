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
        Legs,             // 다리 보호구
        Feet,             // 신발 / 무릎 (KneeAttach)

        // 2. 악세사리 / 부속 장비 (8종)
        HelmetAttachment, // 투구 장식
        ShoulderAttach,   // 어깨 장식
        ElbowAttach,      // 팔꿈치 보호구
        BackAttachment,   // 등 장식 (날개/망토)
        HipsAttachment,   // 허리 장식
        Ear,              // 귀 / 귀걸이
        Ring,             // 반지
        Necklace,         // 목걸이

        // 3. 하단 상시 고정 무기 (2종)
        Weapon,           // 주무기
        Shield            // 보조무기 / 방패 / 화살통
    }

    public enum PaperdollTab
    {
        Armor,
        Accessory
    }

    /// <summary>
    /// 캐릭터 주변에 떠 있는 세부 장비 페이퍼돌 뷰.
    /// [방어구] 탭 8슬롯 / [악세사리] 탭 8슬롯 동적 전환 및 하단 [주무기/보조무기] 상시 고정 노출을 지원합니다.
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
                        IconImage.color = new Color(1f, 1f, 1f, 0f);
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

        [Header("슬롯 컨테이너")]
        [SerializeField] private GameObject _armorSlotsContainer;
        [SerializeField] private GameObject _accessorySlotsContainer;
        [SerializeField] private GameObject _weaponSlotsContainer;

        [Header("탭 헤더 인디케이터")]
        [SerializeField] private TMP_Text _modeLabelText;
        [SerializeField] private Button _tabArmorButton;
        [SerializeField] private Button _tabAccessoryButton;

        [Header("전체 페이퍼돌 장비 슬롯 리스트")]
        [SerializeField] private List<PaperdollSlotView> _slots = new();

        public event Action<EquipmentSlotType, EquipmentItem> OnSlotSelected;
        public event Action<EquipmentSlotType, EquipmentItem> OnUnequipRequested;
        public event Action<PaperdollTab> OnTabChanged;
        private EquipmentSlotType? _currentSelectedSlot;
        private PaperdollTab _currentTab = PaperdollTab.Armor;

        public EquipmentSlotType? CurrentSelectedSlot => _currentSelectedSlot;
        public PaperdollTab CurrentTab => _currentTab;

        private void Awake()
        {
            if (_tabArmorButton != null)
                _tabArmorButton.onClick.AddListener(() => SetTab(PaperdollTab.Armor));
            if (_tabAccessoryButton != null)
                _tabAccessoryButton.onClick.AddListener(() => SetTab(PaperdollTab.Accessory));

            UpdateTabVisuals();
        }

        public void SetTab(PaperdollTab tab)
        {
            if (_currentTab == tab && _armorSlotsContainer != null && _armorSlotsContainer.activeSelf == (tab == PaperdollTab.Armor))
                return;

            // 이전에 선택한 슬롯의 하이라이트 제거
            if (_currentSelectedSlot.HasValue)
            {
                foreach (var slot in _slots)
                {
                    if (slot.SlotType == _currentSelectedSlot)
                    {
                        slot.SetSelected(false);
                        break;
                    }
                }
                _currentSelectedSlot = null;  // 선택 초기화
            }

            _currentTab = tab;
            UpdateTabVisuals();
            OnTabChanged?.Invoke(_currentTab);
        }

        private void UpdateTabVisuals()
        {
            if (_armorSlotsContainer != null)
                _armorSlotsContainer.SetActive(_currentTab == PaperdollTab.Armor);
            if (_accessorySlotsContainer != null)
                _accessorySlotsContainer.SetActive(_currentTab == PaperdollTab.Accessory);

            if (_weaponSlotsContainer != null)
                _weaponSlotsContainer.SetActive(true); // 무기는 항상 노출!

            if (_modeLabelText != null)
            {
                _modeLabelText.text = _currentTab == PaperdollTab.Armor ? "방어구 파츠" : "악세사리 파츠";
            }

            SetTabBtnStyle(_tabArmorButton, _currentTab == PaperdollTab.Armor);
            SetTabBtnStyle(_tabAccessoryButton, _currentTab == PaperdollTab.Accessory);
        }

        private void SetTabBtnStyle(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isActive 
                    ? new Color(0.2f, 0.7f, 1f, 0.95f) 
                    : new Color(0.12f, 0.18f, 0.28f, 0.8f);
            }
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.color = isActive ? Color.white : new Color(0.7f, 0.8f, 0.9f, 0.8f);
                txt.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
            }
        }

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
                slot.SetSelected(_currentSelectedSlot == slot.SlotType);
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
            var slot = _slots.Find(s => s.SlotType == slotType);
            if (slot != null && slot.CurrentItem != null)
            {
                // 장착 중인 아이템 클릭 시 장착 해제 요청 전달
                OnUnequipRequested?.Invoke(slotType, slot.CurrentItem);
            }
            else
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
        }

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
                    return appearanceService?.GetEquippedItem(PartType.HeadCoverings);

                case EquipmentSlotType.Torso:
                    return appearanceService?.GetEquippedItem(PartType.Torso);

                case EquipmentSlotType.ArmUpper:
                    return appearanceService?.GetEquippedItem(PartType.ArmUpperLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.ArmUpperRight);

                case EquipmentSlotType.ArmLower:
                    return appearanceService?.GetEquippedItem(PartType.ArmLowerLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.ArmLowerRight);

                case EquipmentSlotType.Hands:
                    return appearanceService?.GetEquippedItem(PartType.HandLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.HandRight);

                case EquipmentSlotType.Hips:
                    return appearanceService?.GetEquippedItem(PartType.Hips);

                case EquipmentSlotType.Legs:
                    return appearanceService?.GetEquippedItem(PartType.LegLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.LegRight);

                case EquipmentSlotType.Feet:
                    return appearanceService?.GetEquippedItem(PartType.KneeAttachLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.KneeAttachRight);

                // 2. 악세사리 파츠 8종
                case EquipmentSlotType.HelmetAttachment:
                    return appearanceService?.GetEquippedItem(PartType.HelmetAttachment);

                case EquipmentSlotType.ShoulderAttach:
                    return appearanceService?.GetEquippedItem(PartType.ShoulderAttachLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.ShoulderAttachRight);

                case EquipmentSlotType.ElbowAttach:
                    return appearanceService?.GetEquippedItem(PartType.ElbowAttachLeft) 
                        ?? appearanceService?.GetEquippedItem(PartType.ElbowAttachRight);

                case EquipmentSlotType.BackAttachment:
                    return appearanceService?.GetEquippedItem(PartType.BackAttachment);

                case EquipmentSlotType.HipsAttachment:
                    return appearanceService?.GetEquippedItem(PartType.HipsAttachment);

                case EquipmentSlotType.Ear:
                    return appearanceService?.GetEquippedItem(PartType.Ear);

                case EquipmentSlotType.Ring:
                    return null;

                case EquipmentSlotType.Necklace:
                    return null;

                // 3. 하단 상시 고정 무기 2종
                case EquipmentSlotType.Weapon:
                    if (weaponActions != null && inventory != null)
                    {
                        int dbKey = weaponActions.CurrentMainHandDbKey;
                        if (dbKey > 0)
                        {
                            for (int i = 0; i < inventory.MaxSlots; i++)
                            {
                                var stack = inventory.Get(i);
                                if (stack?.Source is EquipmentItem eq && eq.GetDbKey() == dbKey)
                                    return eq;
                            }
                        }
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
