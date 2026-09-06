namespace MySecondWorld.Combat
{
    /// <summary>
    /// 전투에 참여하며 현재 공격 및 전투 상태를 외부에서 확인할 수 있는 대상의 계약.
    /// </summary>
    public interface ICombatant
    {
        /// <summary>
        /// 현재 전투 중인지 여부입니다 (최근 공격 시도 또는 교전 중).
        /// </summary>
        bool IsInCombat { get; }

        /// <summary>
        /// 현재 공격 애니메이션 또는 공격 시퀀스가 진행 중인지 여부입니다 (슈퍼아머 판정 등).
        /// </summary>
        bool IsAttacking { get; }

        /// <summary>
        /// 이 전투원이 속한 진영입니다 (플레이어 편 vs 몬스터 편).
        /// </summary>
        CombatFaction Faction { get; }
    }
}
