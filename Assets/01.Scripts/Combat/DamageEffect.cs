using UnityEngine;

namespace MySecondWorld.Combat
{
    // 축3 구현체 — 타겟에게 데미지를 적용하는 효과
    public class DamageEffect : ISkillEffect
    {
        public void Apply(IDamageable target, SkillContext ctx)
        {
            Debug.Log($"[Damage] {target} 에게 {ctx.Power} 데미지 적용");
            target.TakeDamage(ctx.Power);
        }
    }
}