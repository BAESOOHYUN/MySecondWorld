using System.Collections.Generic;

namespace MySecondWorld.Combat
{
    public class SingleTargetSelector : ITargetSelector
    {
        // 캐스트 타이밍이 확정한 후보 중 '가장 가까운 대상 하나만' 선택.
        // TODO : '타겟팅' 된 몬스터를 우선 처리하는 코드 추가
        public IEnumerable<IDamageable> GetTargets(SkillContext ctx, IReadOnlyList<IDamageable> resolvedCandidates)
        {
            if (resolvedCandidates.Count > 0)
                yield return resolvedCandidates[0];
        }
    }
}