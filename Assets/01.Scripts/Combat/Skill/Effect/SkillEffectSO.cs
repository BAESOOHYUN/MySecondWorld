using UnityEngine;

namespace MySecondWorld.Combat
{
    // 축3 구현체를 SO 에셋으로 만들기 위한 베이스. 무상태 규칙은 TargetSelectorSO 주석 참고.
    public abstract class SkillEffectSO : ScriptableObject, ISkillEffect
    {
        public abstract void Apply(IDamageable target, SkillContext ctx);
    }
}