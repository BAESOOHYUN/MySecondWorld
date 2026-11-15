using UnityEngine;
using UnityEngine.InputSystem;

namespace MySecondWorld.Combat.Player
{
    // 임시: 슬롯 UI(6단계) 전에 PlayerSkillSlots를 시험하기 위한 숫자키 1~4 입력.
    // 에디터/개발 빌드에서만 컴파일된다. 6단계에서 UI가 생기면 삭제한다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RequireComponent(typeof(PlayerSkillSlots))]
    public class SkillSlotDebugInput : MonoBehaviour
    {
        [Tooltip("조준 지점 = 캐릭터 앞쪽 이 거리")]
        [SerializeField] private float _aimDistance = 2f;

        private PlayerSkillSlots _slots;

        private void Awake() => _slots = GetComponent<PlayerSkillSlots>();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) Cast(0);
            if (kb.digit2Key.wasPressedThisFrame) Cast(1);
            if (kb.digit3Key.wasPressedThisFrame) Cast(2);
            if (kb.digit4Key.wasPressedThisFrame) Cast(3);
        }

        private void Cast(int slot)
        {
            Debug.Log($"[Slot] 키 입력: 슬롯 {slot}");
            var point = transform.position + transform.forward * _aimDistance;
            _slots.TryCastSlot(slot, point);
        }
    }
#endif
}
