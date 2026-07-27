namespace MySecondWorld.Combat.Monster
{
    // "AI가 지금 어떤 스킬을 쓸지" 결정하는 전략. null이면 지금은 쓸 스킬 없음(쿨다운 등으로 대기).
    public interface ISkillSelector
    {
        int? SelectSkill(AIContext context);
    }
}