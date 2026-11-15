using MySecondWorld.Combat.Player;
using UnityEngine;

namespace MySecondWorld.UI
{
    // 스킬 슬롯 줄: 내 플레이어가 스폰되면 슬롯 버튼들을 그 플레이어의 PlayerSkillSlots에 연결한다.
    // 버튼 배열의 순서 = 슬롯 번호(0번 버튼 = 숫자키 1).
    public class SkillSlotBarHUD : MonoBehaviour
    {
        [SerializeField] private SkillSlotButton[] _buttons;

        private void OnEnable()
        {
            // HUD가 먼저 살아 있을 수도, 플레이어가 이미 스폰돼 있을 수도 있다. 양쪽 다 처리한다.
            if (PlayerSkillSlots.Local != null) Bind(PlayerSkillSlots.Local);
            PlayerSkillSlots.LocalReady += Bind;
        }

        private void OnDisable()
        {
            PlayerSkillSlots.LocalReady -= Bind;
            foreach (var button in _buttons)
                if (button != null) button.Unbind();
        }

        private void Bind(PlayerSkillSlots slots)
        {
            for (int i = 0; i < _buttons.Length; i++)
                if (_buttons[i] != null) _buttons[i].Bind(slots, i);
        }
    }
}
