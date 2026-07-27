using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축2: 발동 타이밍 — "이 스킬이 언제 실제로 발동될지"를 결정하는 전략 인터페이스
    public interface ICastTiming
    {
        // "SkillExecutor가 실제 효과를 적용하기 전에, 이 지점에서 대기가 필요하면 대기하고 오라"는 자리(role)를 가리킨다.
        UniTask WaitBeforeExecute(SkillContext ctx);
    }
}