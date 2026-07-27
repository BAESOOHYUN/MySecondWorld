using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축2 구현체 — 대기 없이 즉시 발동하는 가장 단순한 타이밍 전략
    public class InstantCast : ICastTiming
    {
        // "이미 완료된 작업"(UniTask.CompletedTask)을 돌려주면, await가 그 자리에서 바로 통과
        public UniTask WaitBeforeExecute(SkillContext ctx) => UniTask.CompletedTask;

        // TODO : DelayedCast(캐스팅 바 있는 스킬)라면 내부에서 await UniTask.Delay(castTime, ct);처럼 진짜 몇 초 걸리는 작업을 돌려줌
        // TODO : ProjectileCast(투사체)라면 "투사체가 타겟에 도달할 때까지"를 기다리는 작업을 돌려줌
    }
}