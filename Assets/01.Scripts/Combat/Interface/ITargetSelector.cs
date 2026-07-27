using System.Collections.Generic;

namespace MySecondWorld.Combat
{
    // 축1: 타겟팅 — "이 스킬이 누구에게 적용될지"를 결정하는 전략 인터페이스
    public interface ITargetSelector
    {
        // IEnumerable<T>는 "0개든, 1개든, N개든 순회(foreach)할 수 있다"는 것만 약속하는 가장 느슨한 타입
        // SingleTargetSelector.cs 안의 yield return 문법과 짝을 이뤄 써야하기 때문에 IEnumerable를 선택
        IEnumerable<IDamageable> GetTargets(SkillContext ctx);
    }
}
