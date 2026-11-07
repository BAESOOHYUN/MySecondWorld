using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.Network.Player;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;
using MySecondWorld.Customization;
using MySecondWorld.Network;
using MySecondWorld.Combat.Player;

/// <summary>
/// Mirror-spawned 플레이어 프리팹 전용 LifetimeScope.
/// 프리팹 루트에 부착하고 Inspector에서 Parent = ProjectLifetimeScope 연결.
/// </summary>
public class PlayerScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();

        builder.RegisterMessageBroker<PlayerJumpedEvent>(options);
        builder.RegisterMessageBroker<PlayerFalledEvent>(options);
        builder.RegisterMessageBroker<PlayerLandedEvent>(options);
        builder.RegisterMessageBroker<PlayerSpeedChangedEvent>(options);

        builder.RegisterComponent(GetComponent<MovementController>());
        builder.RegisterComponent(GetComponent<PlayerAnimationController>());
        builder.RegisterComponent(GetComponentInChildren<ThirdPersonCameraController>());
        builder.RegisterComponent(GetComponent<ObservableCharacterCustomizer>());
        builder.RegisterComponent(GetComponent<NetworkCharacterAppearance>());
        builder.RegisterComponent(GetComponent<NetworkPlayerController>());
        builder.RegisterComponent(GetComponent<NetworkInventoryActions>());
        builder.RegisterComponent(GetComponent<NetworkWeaponEquip>());
        builder.RegisterComponent(GetComponent<PlayerHealthState>());
    }
}
