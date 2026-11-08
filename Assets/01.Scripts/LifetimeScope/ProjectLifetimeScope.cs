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
using MySecondWorld.Customization;
using MySecondWorld.Inventory;
using MySecondWorld.Network.Web;
using MySecondWorld.Interaction;

/// <summary>
/// VContainerSettings에 등록되어 앱 시작 시 자동 인스턴스화되는 최상위 LifetimeScope.
/// UIManager, NetworkManager, PlayerAuthenticator와 전역 이벤트 인프라를 등록한다.
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private bool _useServerMode = false; // true: Groq 서버 모드, false: 로컬 모드

    [Header("캐릭터 커스터마이즈")]
    [SerializeField] private AvatarPartKeyTableSO _avatarPartKeyTable;
    [SerializeField] private CharacterAppearanceSO _defaultAppearanceSO;
    protected override void Configure(IContainerBuilder builder)
    {
        ServerModeFlag.Set(_useServerMode);

        var options = builder.RegisterMessagePipe(o => o.InstanceLifetime = InstanceLifetime.Singleton);
      
#if !UNITY_SERVER || UNITY_EDITOR
        // Web 리소스 시스템 (클라이언트 및 에디터 전용)
        builder.Register<WebResourceDownloader>(Lifetime.Singleton);
        builder.Register<TextureMemoryCache>(Lifetime.Singleton);
        builder.Register<AudioClipMemoryCache>(Lifetime.Singleton);
        builder.Register<DiskCacheService>(Lifetime.Singleton);
        builder.Register<IWebResourceService, WebResourceService>(Lifetime.Singleton);

        if (_uiManager != null)
            builder.RegisterComponent(_uiManager).As<UIManager>().As<IUIManager>();

        builder.RegisterComponentInHierarchy<TouchEventListener>();
        builder.RegisterComponentInHierarchy<TapTargetResolver>();
        builder.RegisterComponentInHierarchy<LoginUI>();
        builder.RegisterComponentInHierarchy<MenuUI>();
        builder.RegisterComponentInHierarchy<ChatUI>();
        builder.RegisterComponentInHierarchy<CharacterCustomizeUI>();
        builder.RegisterComponentInHierarchy<InventoryUI>();
        builder.RegisterComponentInHierarchy<ShopUI>().As<IShopUI>();
        builder.RegisterComponentInHierarchy<NPCMenuUI>().As<INPCMenuUI>();

        // 앱 전체 수명 동안 필요 — 형제 스코프(AIModelDownload, NPC) 모두 접근 가능
        if (_useServerMode)
            builder.Register<IDeviceCapability, NetworkDeviceCapability>(Lifetime.Singleton);
        //                "IDeviceCapability 요청이 오면 NetworkDeviceCapability 인스턴스 줘" -> NPCLifetimeScope
        else
            builder.Register<IDeviceCapability, LocalDeviceCapability>(Lifetime.Singleton);
#else
        // 헤드리스 서버: IUIManager를 아무도 등록 안 해서 GameNetworkManager 주입이 통째로 실패하던 문제 — 더미로 해결
        builder.Register<IUIManager, NullUIManager>(Lifetime.Singleton);
#endif

        builder.RegisterMessageBroker<AuthStateChangedEvent>(options);
        builder.RegisterMessageBroker<CharacterAppearanceSyncRequestedEvent>(options);
        builder.RegisterMessageBroker<RequestInitialCustomizeEvent>(options);

        builder.Register<SaveManager>(Lifetime.Singleton);
        builder.Register<PasswordCredentialStore>(Lifetime.Singleton);
        builder.Register<LocalAppearanceCache>(Lifetime.Singleton);
        builder.Register<CharacterAppearanceService>(Lifetime.Singleton);
        builder.Register<LocalPlayerInventoryProvider>(Lifetime.Singleton);
        builder.Register<CharacterGenderFilterService>(Lifetime.Singleton);

        builder.RegisterComponentInHierarchy<GameNetworkManager>();
        builder.RegisterComponentInHierarchy<PlayerAuthenticator>();
        builder.RegisterInstance(_avatarPartKeyTable);
        builder.RegisterInstance(_defaultAppearanceSO);

        builder.Register<AddressableLabelLoader>(Lifetime.Singleton);
        builder.RegisterEntryPoint<AddressablesService>(Lifetime.Singleton).AsSelf();
        builder.RegisterMessageBroker<AddressablesProgressEvent>(options);
    }
}
