namespace MySecondWorld.Combat
{
    /// <summary>
    /// 피격 경직(Hit Stun), 기절, 빙결 등 일시적으로 이동 및 행동이 제어되는 대상이 구현하는 계약.
    /// </summary>
    public interface IStunnable
    {
        /// <summary>
        /// 지정된 지속 시간(초) 동안 대상을 경직/기절 상태로 만듭니다.
        /// </summary>
        void ApplyStun(float duration);

        /// <summary>
        /// 현재 경직/기절 상태인지 여부입니다.
        /// </summary>
        bool IsStunned { get; }
    }
}
