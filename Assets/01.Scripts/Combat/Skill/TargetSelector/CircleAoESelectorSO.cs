using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat
{
    [SkillPart("범위 안의 후보 전원을 대상으로 삼는다")]
    [CreateAssetMenu(fileName = "CircleAoESelectorSO",   menuName = "Combat/Target Selector/Circle AoE")]
    public class CircleAoESelectorSO : TargetSelectorSO
    {
        // 범위 판정은 탐지 단계의 OverlapSphere가 이미 수행했으므로 후보 전원을 그대로 반환한다.
        public override IEnumerable<IDamageable> GetTargets(SkillContext ctx) => ctx.Candidates;
    }
}