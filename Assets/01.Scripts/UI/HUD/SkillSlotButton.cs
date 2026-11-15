using MySecondWorld.Combat.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.UI
{
    // 스킬 슬롯 1칸: 아이콘 + 원형 쿨다운 오버레이 + 키 번호. 누르면(터치/클릭) 해당 슬롯을 시전한다.
    // 씬에서는 Button이 붙은 오브젝트 아래에 Image들을 두고 이 컴포넌트에 연결한다.
    [RequireComponent(typeof(Button))]
    public class SkillSlotButton : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [Tooltip("Image Type을 Filled / Radial 360으로 설정. 쿨다운 동안 차오르며 줄어든다.")]
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMP_Text _keyLabel;
        [SerializeField] private TMP_Text _cooldownText;

        private PlayerSkillSlots _slots;
        private int _slotIndex;
        private float _cooldownTotal;   // 마지막 시전 때의 대기 시간(오버레이 비율 계산용)
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
            Unbind();
        }

        public void Bind(PlayerSkillSlots slots, int slotIndex)
        {
            Unbind();
            _slots = slots;
            _slotIndex = slotIndex;
            _slots.SlotCast += HandleSlotCast;

            var skill = slots.GetSkill(slotIndex);
            gameObject.SetActive(skill != null); // 비어 있는 슬롯은 숨긴다
            if (skill == null) return;

            if (_icon != null)
            {
                _icon.sprite = skill.Icon;
                _icon.enabled = skill.Icon != null;
            }
            if (_keyLabel != null) _keyLabel.text = (slotIndex + 1).ToString();
            SetCooldownVisual(0f);
        }

        public void Unbind()
        {
            if (_slots != null) _slots.SlotCast -= HandleSlotCast;
            _slots = null;
        }

        private void OnClick()
        {
            if (_slots != null) _slots.TryCastSlot(_slotIndex);
        }

        private void HandleSlotCast(int slot, float remaining)
        {
            if (slot == _slotIndex) _cooldownTotal = Mathf.Max(remaining, 0.01f);
        }

        private void Update()
        {
            if (_slots == null) return;
            SetCooldownVisual(_slots.GetCooldownRemaining(_slotIndex));
        }

        private void SetCooldownVisual(float remaining)
        {
            bool cooling = remaining > 0f;
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.enabled = cooling;
                if (cooling) _cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / _cooldownTotal);
            }
            if (_cooldownText != null)
                _cooldownText.text = cooling ? remaining.ToString(remaining >= 1f ? "0" : "0.0") : string.Empty;
        }
    }
}
