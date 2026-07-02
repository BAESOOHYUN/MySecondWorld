using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.UI;
using MySecondWorld.Network;
using MySecondWorld.AI.Voice;
using Whisper;

namespace MySecondWorld.AI
{
    /// <summary>
    /// NPC 메시지 인프라 + 대화 UI를 담당하는 LifetimeScope.
    /// MainScene에 배치. NPCScope의 부모 스코프.
    /// </summary>
    public class NPCLifetimeScope : LifetimeScope
    {
        // Configure()는 컨테이너를 만드는 단계라 [Inject]를 쓸 수 없어서 Resolve를 직접 쓴다.
        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<string, PlayerMessageSentEvent>(options);
            builder.RegisterMessageBroker<NPCResponseReceivedEvent>(options);
            builder.RegisterMessageBroker<NPCThinkingStartedEvent>(options);
            builder.RegisterMessageBroker<NPCDialogueEvent>(options);
            builder.RegisterMessageBroker<NPCTTSSentenceEvent>(options);
            builder.RegisterMessageBroker<NPCStopTTSEvent>(options);
            builder.RegisterMessageBroker<NPCSpeakFinishedEvent>(options);
            builder.RegisterMessageBroker<NPCSentenceStartedEvent>(options);

            // UseServerMode flag에 따라 ISTTService 구현체 교체
            // 마이크는 1개, 공유하기 때문에 MainScene에 위치
            var deviceCapability = Parent.Container.Resolve<IDeviceCapability>();
            //                     ↑ 부모 컨테이너에서  ↑ IDeviceCapability 인스턴스 꺼내줘
            //                  ProjectLifetimeScope

            if (deviceCapability.UseServerMode)
                builder.RegisterInstance(gameObject.AddComponent<GroqWhisperSTTService>()).As<ISTTService>();
            else
            {
                builder.RegisterComponentInHierarchy<WhisperManager>();
                builder.RegisterInstance(gameObject.AddComponent<WhisperSTTService>()).As<ISTTService>();
            }

            var uiManager = Parent.Container.Resolve<UIManager>();
            var uiGO = uiManager.GetOrCreateDynamicCanvas<NPCDialogueUI>();
            builder.RegisterComponent(uiGO.GetComponent<NPCDialogueUI>()).As<INPCDialogueUI>();
        }
    }
}
