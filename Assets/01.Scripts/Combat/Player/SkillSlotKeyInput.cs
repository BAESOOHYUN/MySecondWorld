using UnityEngine;
using UnityEngine.InputSystem;

namespace MySecondWorld.Combat.Player
{
    // PC 입력: 숫자키 1~4로 슬롯을 시전한다. 모바일은 슬롯 UI 버튼(SkillSlotButton)이 같은 TryCastSlot을 부른다.
    // 키보드가 없는 기기에서는 Keyboard.current가 null이라 아무 일도 하지 않는다.
    [RequireComponent(typeof(PlayerSkillSlots))]
    public class SkillSlotKeyInput : MonoBehaviour
    {
        private PlayerSkillSlots _slots;

        private void Awake() => _slots = GetComponent<PlayerSkillSlots>();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) _slots.TryCastSlot(0);
            if (kb.digit2Key.wasPressedThisFrame) _slots.TryCastSlot(1);
            if (kb.digit3Key.wasPressedThisFrame) _slots.TryCastSlot(2);
            if (kb.digit4Key.wasPressedThisFrame) _slots.TryCastSlot(3);
        }
    }
}
