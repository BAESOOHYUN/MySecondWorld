namespace MySecondWorld.Foundation
{
    /// <summary>
    /// 피격 경직, 사망, UI 오픈 등으로 인해 이동이 일시적으로 잠금되거나 해제될 수 있는 대상의 계약.
    /// </summary>
    public interface IMovementLockable
    {
        bool MovementLocked { get; set; }
    }
}
