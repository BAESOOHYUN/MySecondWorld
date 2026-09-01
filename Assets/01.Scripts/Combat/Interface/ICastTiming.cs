using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축2: 발동 타이밍 — "이 스킬이 언제 실제로 발동될지"를 결정하는 전략.
    //
    // 대기가 필요하면 대기한다. 대기 중에 대상이 확정되는 종류(투사체)라면
    // 확정된 대상을 ctx.SetCandidates(...)로 기록해서 축1에 넘긴다.
    public interface ICastTiming
    {
        UniTask WaitBeforeExecute(SkillContext ctx);
    }
}