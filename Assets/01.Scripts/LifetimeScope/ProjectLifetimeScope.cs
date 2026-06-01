using UnityEngine;
using MessagePipe;
using VContainer;
using VContainer.Unity;
using MySecondWorld.Network;
using MySecondWorld.Network.Chat;
using MySecondWorld.UI;
using MySecondWorld.AI;
using MySecondWorld.Save;
using MySecondWorld.Bundle;

/// <summary>
/// VContainerSettings에 등록되어 앱 시작 시 자동 인스턴스화되는 최상위 LifetimeScope.
/// UIManager, NetworkManager, PlayerAuthenticator와 전역 이벤트 인프라를 등록한다.
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private bool _useServerMode = false; // true: Groq 서버 모드, false: 로컬 모드

    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<AuthStateChangedEvent>(options);

        builder.Register<SaveManager>(Lifetime.Singleton);

        builder.RegisterComponent(_uiManager);
        builder.RegisterComponentInHierarchy<TouchEventListener>();
        builder.RegisterComponentInHierarchy<GameNetworkManager>();
        builder.RegisterComponentInHierarchy<PlayerAuthenticator>();
        builder.RegisterComponentInHierarchy<LoginUI>();
        builder.RegisterComponentInHierarchy<MenuUI>();
        builder.RegisterComponentInHierarchy<ChatUI>();
        builder.RegisterComponentInHierarchy<SettingsUI>();

        // 앱 전체 수명 동안 필요 — 형제 스코프(AIModelDownload, NPC) 모두 접근 가능
        if (_useServerMode)
            builder.Register<IDeviceCapability, NetworkDeviceCapability>(Lifetime.Singleton);
        //                "IDeviceCapability 요청이 오면 NetworkDeviceCapability 인스턴스 줘" -> NPCLifetimeScope
        else
            builder.Register<IDeviceCapability, LocalDeviceCapability>(Lifetime.Singleton);

        builder.Register<ZoneAddressableLoader>(Lifetime.Singleton);
        builder.RegisterEntryPoint<AddressablesService>(Lifetime.Singleton).AsSelf();
        builder.RegisterMessageBroker<AddressablesProgressEvent>(options);
    }
}
