namespace MySecondWorld.Network.Player
{
    public struct PlayerJumpedEvent { }
    public struct PlayerFalledEvent { }
    public struct PlayerLandedEvent { }
    public struct PlayerSpeedChangedEvent
    {
        public float Speed;
    }
}