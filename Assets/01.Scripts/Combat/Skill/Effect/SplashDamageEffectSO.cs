using UnityEngine;

namespace MySecondWorld.Combat
{
    // 범위 공격 스킬의 데미지 분배를 담당하는 효과 클래스.
    // 투사체나 스킬이 폭발/범위 공격을 할 때, 중심점(Epicenter)과 반경(SplashRadius)을 기준으로
    // 대상에게 거리 비례 데미지를 적용하는 역할을 한다.
    // - Epicenter가 없으면 단일 대상에게 고정 데미지를 준다.
    // - Epicenter가 있으면 중심에서 가까울수록 데미지가 크고,
    //   반경 끝에 가까울수록 데미지가 줄어든다. (중심: 100% → 반경 끝: 0%)
    [CreateAssetMenu(fileName = "SplashDamageEffectSO", menuName = "Combat/Effect/Splash Damage")]
    public class SplashDamageEffectSO : SkillEffectSO
    {
        public override void Apply(IDamageable target, SkillContext ctx)
        {
            // Epicenter가 없으면 그냥 고정 데미지
            if (ctx.Epicenter == null)
            {
                target.TakeDamage(ctx.Power);
                return;
            }

            // 대상 Transform을 가져올 수 없으면 고정 데미지
            var targetTransform = (target as MonoBehaviour)?.transform;
            if (targetTransform == null)
            {
                target.TakeDamage(ctx.Power);
                return;
            }

            // Splash 반경이 없으면 고정 데미지
            float radius = ctx.SplashRadius;
            if (radius <= 0f)
            {
                target.TakeDamage(ctx.Power);
                return;
            }
            // Epicenter와 대상 사이 거리 계산
            float distance = Vector3.Distance(targetTransform.position, ctx.Epicenter.Value);
            // 거리 비율(t): 0 = 중심, 1 = 반경 끝
            float t = Mathf.Clamp01(distance / radius);
            // 데미지 = Power * (1 - t)
            // 중심에서 100% → 반경 끝에서 0%
            float damage = ctx.Power * (1f - t);
            target.TakeDamage(damage);
        }
    }
}