using UnityEngine;

namespace MySecondWorld.Combat
{
    // 기존 축2+축3을 그 시각에 발동하는 클립
    // 축2(누구를: TargetSelectorSO) + 축3(무슨 효과: SkillEffectSO)을 묶어 StartTime 시각에 발동하는 클립.
    // 축1(언제)는 SkillDefinition이 타임라인 시작 전에 한 번 대기하므로 여기에 없다.

    [System.Serializable]
    public class EffectClip : SkillClip
    {
        [SerializeField] private TargetSelectorSO _targetSelector;
        [SerializeField] private SkillEffectSO[] _effects;

        public override void Execute(SkillContext ctx)
        {
            // 툴에서 막 추가한 빈 클립은 슬롯이 비어 있다 — 예외 대신 조용히 건너뛴다.
            if (_targetSelector == null || _effects == null) return;

            foreach (var target in _targetSelector.GetTargets(ctx))
                foreach (var effect in _effects)
                    effect.Apply(target, ctx);
        }
    }
}