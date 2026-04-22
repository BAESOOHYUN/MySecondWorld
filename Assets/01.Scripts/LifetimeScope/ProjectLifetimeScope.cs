using UnityEngine;
using Mirror;
using MessagePipe;
using VContainer;
using VContainer.Unity;
using MySecondWorld.Network;
using MySecondWorld.Network.Chat;
using MySecondWorld.UI;

/// <summary>
/// VContainerSettings에 등록되어 앱 시작 시 자동 인스턴스화되는 최상위 LifetimeScope.
/// UIManager, NetworkManager, PlayerAuthenticator와 전역 이벤트 인프라를 등록한다.
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    [SerializeField] private UIManager _uiManager;

    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<AuthStateChangedEvent>(options);

        builder.RegisterComponent(_uiManager);
        builder.RegisterComponentInHierarchy<PlayerAuthenticator>();
        builder.RegisterComponentInHierarchy<LoginUI>();
        builder.RegisterComponentInHierarchy<MenuUI>();
        builder.RegisterComponentInHierarchy<ChatUI>();
    }
}
