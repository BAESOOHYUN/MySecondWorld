using System.Collections.Generic;

namespace MySecondWorld.Combat
{
    // 축1 구현체 — 범위 판정은 Hitbox의 OverlapSphere가 이미 수행했으므로,
    // 캐스트 타이밍이 확정한 후보 전원을 그대로 타겟으로 반환한다.
    public class CircleAoESelector : ITargetSelector
    {
        public IEnumerable<IDamageable> GetTargets(SkillContext ctx, IReadOnlyList<IDamageable> resolvedCandidates)
      => resolvedCandidates;
    }
}