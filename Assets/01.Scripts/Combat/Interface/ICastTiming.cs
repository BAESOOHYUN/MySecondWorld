using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축2: 발동 타이밍 — "이 스킬이 언제 실제로 발동될지"를 결정하는 전략 인터페이스
    public interface ICastTiming : IDisposable
    {
        // 기존의 WaitBeforeExecute(즉시 발동)을 포함한 개념.
        // 대기가 필요하면 대기한 뒤, 그 시점에 실제로 확정된 대상 목록을 반환한다.
        // (예: ProjectileCast는 투사체가 실제로 충돌하는 순간까지 대기했다가 그 순간 맞은 대상만 반환)
        UniTask<IReadOnlyList<IDamageable>> ResolveTargets(SkillContext ctx);
    }
}