using UnityEngine;
using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.AI;
using MySecondWorld.UI;
using System;
/// <summary>
/// OpenWorldRobby씬 GameObject에 부착
/// </summary>
public class AIModelDownloadLifetimeScope : LifetimeScope
{
    [SerializeField] private AIModelConfigSO _modelConfig;
    [SerializeField] private bool _useServerMode = false; // true: Groq 서버 모드, false: 로컬 모드

    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<AIModelDownloadProgressEvent>(options);

        if (_useServerMode)
            builder.Register<IDeviceCapability, NetworkDeviceCapability>(Lifetime.Singleton);
        else
            builder.Register < IDeviceCapability,LocalDeviceCapability>(Lifetime.Singleton);

        builder.Register<AIModelDownloader>(Lifetime.Singleton);
        builder.RegisterEntryPoint<AIModelDownloadStarter>();
        var uiManager = Parent.Container.Resolve<UIManager>();
        var uiGO = uiManager.GetOrCreateDynamicCanvas<AIModelDownloadUI>();
        builder.RegisterComponent(uiGO.GetComponent<AIModelDownloadUI>());
        builder.RegisterInstance(_modelConfig);
    }
}