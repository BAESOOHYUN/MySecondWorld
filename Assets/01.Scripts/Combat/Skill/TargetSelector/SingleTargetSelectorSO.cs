using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat
{
    [CreateAssetMenu(fileName = "SingleTargetSelectorSO", menuName = "Combat/Target Selector/Single Target")]
    public class SingleTargetSelectorSO : TargetSelectorSO
    {
        // 후보 중 첫 번째 하나만. 후보는 탐지 단계(SphereAttackResolver)에서 이미 거리순 정렬돼 있으므로
        // 첫 번째가 곧 "가장 가까운 대상"이다.
        public override IEnumerable<IDamageable> GetTargets(SkillContext ctx)
        {
            if (ctx.Candidates.Count > 0)
                yield return ctx.Candidates[0];
        }
    }
}