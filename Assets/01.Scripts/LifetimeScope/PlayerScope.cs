using UnityEngine;
using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.Network.Player;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;
using MySecondWorld.Customization;
using MySecondWorld.Character;

/// <summary>
/// Mirror-spawned 플레이어 프리팹 전용 LifetimeScope.
/// 프리팹 루트에 부착하고 Inspector에서 Parent = ProjectLifetimeScope 연결.
/// </summary>
public class PlayerScope : LifetimeScope
{
    [SerializeField] private AvatarPartKeyTableSO _avatarPartKeyTable;
    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();

        builder.RegisterMessageBroker<PlayerJumpedEvent>(options);
        builder.RegisterMessageBroker<PlayerFalledEvent>(options);
        builder.RegisterMessageBroker<PlayerLandedEvent>(options);
        builder.RegisterMessageBroker<PlayerSpeedChangedEvent>(options);
        builder.RegisterMessageBroker<CharacterAppearanceChangedEvent>(options);
        builder.RegisterMessageBroker<CharacterAppearanceSavedEvent>(options);

        builder.RegisterComponentInHierarchy<MovementController>();
        builder.RegisterComponentInHierarchy<PlayerAnimationController>();
        builder.RegisterComponentInHierarchy<ThirdPersonCameraController>();

        builder.RegisterComponentInHierarchy<CharacterCustomizer>();
        builder.RegisterInstance(_avatarPartKeyTable);
    }
}
