using UnityEngine;

namespace MySecondWorld.Combat
{
    // 콤보 = 스킬 여러 개를 순서대로 잇는 슬롯 항목. 같은 키를 누를 때마다 다음 단계가 나간다.
    // 단계 사이 판정: 이전 단계가 끝난 뒤 _comboWindow초 안에 눌러야 이어지고, 늦으면 1단계로 돌아간다.
    [CreateAssetMenu(fileName = "Combo_", menuName = "Combat/Combo Definition")]
    public class ComboDefinition : SlotSkill
    {
        [SerializeField] private SkillDefinition[] _steps;
        [Tooltip("이전 단계가 끝난 뒤 이어 누를 수 있는 시간(초).")]
        [Min(0f)] [SerializeField] private float _comboWindow = 0.8f;
        [Tooltip("단계 길이가 0에 가까운 스킬일 때 연타로 한꺼번에 나가는 걸 막는 최소 간격(초).")]
        [Min(0f)] [SerializeField] private float _minStepInterval = 0.3f;

        public System.Collections.Generic.IReadOnlyList<SkillDefinition> Steps => _steps;
        public float ComboWindow => _comboWindow;
        public float MinStepInterval => _minStepInterval;

        public override SkillDefinition TryAdvance(ref SlotCursor cursor, float now, float cooldownScale)
        {
            if (_steps == null || _steps.Length == 0) return null;
            if (now < cursor.ReadyAt) return null;

            // 이어 누를 시간이 지났으면 처음부터
            if (cursor.Step > 0 && now > cursor.ExpireAt) cursor.Step = 0;
            if (cursor.Step >= _steps.Length) cursor.Step = 0;

            var step = _steps[cursor.Step];
            if (step == null) { cursor.Step = 0; return null; }

            float stepLength = Mathf.Max(step.TotalDuration, _minStepInterval);
            bool isLast = cursor.Step == _steps.Length - 1;

            if (isLast)
            {
                // 마지막 단계: 끝난 뒤 콤보 쿨다운, 다음은 1단계
                cursor.ReadyAt = now + stepLength + Cooldown * cooldownScale;
                cursor.ExpireAt = 0f;
                cursor.Step = 0;
            }
            else
            {
                cursor.ReadyAt = now + stepLength * cooldownScale;
                cursor.ExpireAt = now + stepLength + _comboWindow;
                cursor.Step++;
            }
            return step;
        }
    }
}
