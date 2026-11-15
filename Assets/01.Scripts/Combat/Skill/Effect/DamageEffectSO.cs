using UnityEngine;

namespace MySecondWorld.Combat
{
    [SkillPart("대상에게 공격력만큼 데미지를 준다", ExclusiveGroup = "데미지")]
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