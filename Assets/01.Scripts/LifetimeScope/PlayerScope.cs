using UnityEngine;
using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.Network.Player;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;
using MySecondWorld.Customization;
using MySecondWorld.Character;
using MySecondWorld.Save;
using MySecondWorld.Network;

/// <summary>
/// Mirror-spawned 플레이어 프리팹 전용 LifetimeScope.
/// 프리팹 루트에 부착하고 Inspector에서 Parent = ProjectLifetimeScope 연결.
/// </summary>
public class PlayerScope : LifetimeScope
{
    [SerializeField] private AvatarPartKeyTableSO _avatarPartKeyTable;
    [SerializeField] private CharacterAppearanceSO _defaultAppearanceSO;
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
        builder.RegisterComponentInHierarchy<NetworkCharacterAppearance>();
        builder.RegisterComponentInHierarchy<NetworkPlayerController>();

        builder.RegisterInstance(_avatarPartKeyTable);
        builder.RegisterInstance(_defaultAppearanceSO);

        builder.Register<CharacterSaveService>(Lifetime.Singleton);
    }
}
