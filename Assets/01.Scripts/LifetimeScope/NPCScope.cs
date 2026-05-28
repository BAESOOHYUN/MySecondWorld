using VContainer;
using VContainer.Unity;
using MySecondWorld.AI.Voice;

namespace MySecondWorld.AI
{
    /// <summary>
    /// SubScenes에 배치된 NPC에게 직접 부착
    /// </summary>
    public class NPCScope : LifetimeScope
    {
        protected override LifetimeScope FindParent() => Find<NPCLifetimeScope>();

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(GetComponent<NPCBrain>());
            builder.RegisterComponent(GetComponent<NPCInteractable>());
            builder.RegisterComponent(GetComponent<NPCVoicePlayer>());builder.RegisterComponent(GetComponent<NPCLipSyncController>());

            var deviceCapability = Parent.Container.Resolve<IDeviceCapability>();
            //                     ↑ 부모 컨테이너에서  ↑ IDeviceCapability 인스턴스 꺼내줘
            //                  ProjectLifetimeScope
            if (deviceCapability.UseServerMode)
            {
                builder.Register<GroqLLMService>(Lifetime.Scoped).As<ILLMService>(); 
                builder.RegisterComponent(GetComponent<AzureTTSService>()).As<ITTSService>();
            }
            else
            {
                builder.RegisterComponentInHierarchy<LocalLLMService>().As<ILLMService>();
                builder.RegisterComponent(GetComponent<EdgeTTSServiceNativeWebSocket>()).As<ITTSService>(); // 씬에서 EdgeTTSService 찾아서 등록, .As 뜻 'ITTSService로 달라
            }
        }
    }
}