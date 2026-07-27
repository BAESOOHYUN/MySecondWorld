using System.Collections.Generic;

namespace MySecondWorld.Combat
{
    public class SingleTargetSelector : ITargetSelector
    {
        // 후보 중 '가장 가까운 대상 하나만' 선택. ctx.Candidates는 호출부(PlayerAttackController 등)에서
        // 이미 거리순으로 정렬해 담아준다는 전제.
        // TODO : '타겟팅' 된 몬스터를 우선 처리하는 코드 추가
        public IEnumerable<IDamageable> GetTargets(SkillContext ctx)
        {
            if (ctx.Candidates.Count > 0)
                yield return ctx.Candidates[0];
        }
    }
}