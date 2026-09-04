namespace MySecondWorld.Combat
{
    // 데미지를 받을 수 있는 대상이 구현해야 하는 계약
    public interface IDamageable
    {
        void TakeDamage(float amount);
        bool IsDead { get; }
        bool IsTargetable { get; } // 소환 해제 및 무적 상태 판별
        CombatFaction Faction { get; }
    }
}