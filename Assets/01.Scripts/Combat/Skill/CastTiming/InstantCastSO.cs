using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    [CreateAssetMenu(fileName = "InstantCastSO", menuName = "Combat/Cast Timing/Instant")]
    public class InstantCastSO : CastTimingSO
    {
        // 즉시 발동 = 바로 리턴 — 대기도, 대상 교체도 없다.
        // 탐지 단계가 채워둔 ctx.Candidates가 그대로 축1로 넘어간다.
        public override UniTask WaitBeforeExecute(SkillContext ctx) => UniTask.CompletedTask;
    }
}