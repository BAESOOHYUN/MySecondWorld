using System.Collections.Generic;

namespace MySecondWorld.Combat
{
    // 축1: 타겟팅 — "확정된 대상 후보 중 실제로 이 스킬을 적용할 대상"을 결정하는 전략 인터페이스
    public interface ITargetSelector
    {
        // IEnumerable<T>는 "0개든, 1개든, N개든 순회(foreach)할 수 있다"는 것만 약속하는 가장 느슨한 타입
        // SingleTargetSelector.cs 안의 yield return 문법과 짝을 이뤄 써야하기 때문에 IEnumerable를 선택

        // ctx는 캐스터 위치 등 부가 정보용으로 유지(향후 CircleAoESelector 등이 쓸 수 있도록).
        // 실제 판정 대상은 resolvedCandidates(캐스트 타이밍이 확정한 목록)로 명시적으로 분리해서 받는다.
        IEnumerable<IDamageable> GetTargets(SkillContext ctx);
    }
}
