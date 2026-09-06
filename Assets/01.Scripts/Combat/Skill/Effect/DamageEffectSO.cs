using UnityEngine;

namespace MySecondWorld.Combat
{
    [CreateAssetMenu(fileName = "DamageEffectSO", menuName = "Combat/Effect/Damage")]
    public class DamageEffectSO : SkillEffectSO
    {
        public override void Apply(IDamageable target, SkillContext ctx)
        {
            Debug.Log($"[Damage] {target} 에게 {ctx.Power} 데미지 적용");
            target.TakeDamage(ctx.Power, ctx.Caster);
        }
    }
}