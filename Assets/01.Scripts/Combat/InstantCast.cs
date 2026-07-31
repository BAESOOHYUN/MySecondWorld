using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축2 구현체 — 대기 없이 즉시 발동하는 가장 단순한 타이밍 전략
    public class InstantCast : ICastTiming
    {
        // 캐스트 시점에 이미 스캔된 후보(ctx.Candidates)를 그대로 "확정된 대상"으로 반환한다.
        // '대기가 없으므로' 판정 시점과 명중 시점 사이에 시간차가 없어 그대로 써도 안전(회귀 없음).
        public UniTask<IReadOnlyList<IDamageable>> ResolveTargets(SkillContext ctx) => UniTask.FromResult(ctx.Candidates);

        public void Dispose() { }  // // 들고 있는 리소스가 없어서 빈 구현
    }
}

// ① 플레이어 공격 입력
// ② 애니메이션 재생 (여기서 시간이 걸림 — 진짜 "대기")
// ③ 애니메이션 이벤트 발생 → PlayerAttackController.OnAttackHit() 호출
// ④ OnAttackHit() 안에서 Physics.OverlapSphere로 스캔 → candidates 확정 → SkillContext 생성
// ⑤ SkillExecutor.Execute(ctx) 호출 → 여기서 비로소 ICastTiming.ResolveTargets(ctx) 실행
// → InstantCast.ResolveTargets() 입장에서는 자기 차례가 왔을 때 이미 명중 판정이 끝나 있는 상태