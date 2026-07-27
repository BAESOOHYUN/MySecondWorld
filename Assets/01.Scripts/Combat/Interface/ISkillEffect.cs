namespace MySecondWorld.Combat
{
    // 축3: 효과 — 타겟에게 "무엇을" 적용할지를 정의하는 전략 인터페이스
    public interface ISkillEffect
    {
        void Apply(IDamageable target, SkillContext ctx);
    }
}