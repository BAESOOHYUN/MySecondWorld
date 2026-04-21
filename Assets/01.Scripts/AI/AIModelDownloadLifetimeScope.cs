using UnityEngine;
using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.AI;

public class AIModelDownloadLifetimeScope : LifetimeScope
{
    [SerializeField] private AIModelConfigSO _modelConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<AIModelDownloadProgressEvent>(options);
        builder.Register<IDeviceCapability, LocalDeviceCapability>(Lifetime.Singleton);
        builder.Register<AIModelDownloader>(Lifetime.Singleton);
        builder.RegisterEntryPoint<AIModelDownloadStarter>();
        builder.RegisterComponentInHierarchy<AIModelDownloadUI>();
        builder.RegisterInstance(_modelConfig);
    }
}