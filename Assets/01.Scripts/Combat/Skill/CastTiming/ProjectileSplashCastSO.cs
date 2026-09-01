using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 투사체 스킬의 "타겟 후보 결정"과 "범위 판정"을 처리하는 클래스.
    // "투사체 기반 스킬"의 실행 전 대기 로직을 담당한다.
    // - 발사체를 생성하고 씬에 발사한다.
    // - 서버 모드에서는 발사체의 충돌(HitTask)을 기다려 판정한다.
    // - 충돌 대상이 없거나 스플래시 반경이 없으면 단일 대상만 후보로 설정한다.
    // - 충돌 대상이 있고 스플래시 반경이 있으면, 충돌 지점을 중심으로
    //   SphereAttackResolver를 통해 범위 내 모든 적 후보를 탐색한다. 
    [CreateAssetMenu(fileName = "ProjectileSplashCastSO", menuName = "Combat/Cast Timing/Projectile Splash")]
    public class ProjectileSplashCastSO : CastTimingSO
    {
        public override async UniTask WaitBeforeExecute(SkillContext ctx)
        {
            if (ctx.Shot == null)
            {
                Debug.LogError("[ProjectileSplashCastSO] SkillContext에 발사 정보(Shot)가 없다");
                ctx.SetCandidates(Array.Empty<IDamageable>());
                return;
            }

            // ProjectileLauncher를 통해 발사체를 실제로 씬에 생성. 실패하면 후보 대상 없음으로 처리.
            var projectile = ProjectileLauncher.Launch(ctx.Shot.Value, ctx.Caster.scene);
            if (projectile == null)
            {
                ctx.SetCandidates(Array.Empty<IDamageable>());
                return;
            }

            // 발사체 충돌 대기 :
            // 발사체가 날아가서 충돌할 때까지 비동기적으로 대기.
            // 충돌한 대상(IDamageable)을 target으로 받음.
            var target = await projectile.HitTask;

            // 스플래시 판정 : 
            // 만약 충돌 대상이 없거나, 스플래시 반경이 0 이하라면:
            // 충돌 대상이 없으면 후보 없음. 충돌 대상이 있으면 그 대상만 후보로 설정.
            float splashRadius = ctx.Shot.Value.SplashRadius;
            if (target == null || splashRadius <= 0f)
            {
                ctx.SetCandidates(target == null
                    ? Array.Empty<IDamageable>()
                    : new[] { target });
                return;
            }
            // 스플래시 범위 처리 :
            // 발사체가 마지막으로 맞은 위치(LastHitPoint)를 스플래시 중심점(Epicenter)으로 설정.
            // SphereAttackResolver.QueryCandidates를 통해 중심점과 반경 안에 있는 모든 적 후보를 탐색.
            // 그 결과를 ctx.SetCandidates로 저장.
            ctx.SetEpicenter(projectile.LastHitPoint, splashRadius);
            var splashCandidates = SphereAttackResolver.QueryCandidates(
                ctx.Caster, projectile.LastHitPoint, splashRadius, ctx.Shot.Value.HitMask);
            ctx.SetCandidates(splashCandidates);
        }
    }
}