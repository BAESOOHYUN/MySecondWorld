using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 축2 — 투사체를 실제로 날려 "물리적으로 부딪히는 순간"까지 대기하는 발동 타이밍.
    //
    // 이 대기가 곧 "닿을 때 데미지"의 구현이다. await가 끝나기 전에는
    // 축1·축3이 실행되지 않으므로 HP가 깎일 방법 자체가 없다.
    [CreateAssetMenu(fileName = "ProjectileCastSO", menuName = "Combat/Cast Timing/Projectile")]
    public class ProjectileCastSO : CastTimingSO
    {
        public override async UniTask WaitBeforeExecute(SkillContext ctx)
        {
            if (ctx.Shot == null)
            {
                Debug.LogError("[ProjectileCastSO] SkillContext에 발사 정보(Shot)가 없다 — 캐스터가 채워 보내야 한다");
                ctx.SetCandidates(Array.Empty<IDamageable>());
                return;
            }

            // 발사하고, 실제로 부딪히거나 수명이 다할 때까지 여기서 멈춰 있는다.
            var target = await ProjectileLauncher.Launch(ctx.Shot.Value, ctx.Caster.scene);

            // 대기 중에 확정된 대상으로 후보를 교체한다. 축1은 이 결과만 본다.
            // 발사 시점의 후보를 쓰지 않는 게 핵심 — 그 사이 대상이 움직이거나 죽었을 수 있다.
            ctx.SetCandidates(target == null
                ? Array.Empty<IDamageable>()
                : new[] { target });
        }
    }
}