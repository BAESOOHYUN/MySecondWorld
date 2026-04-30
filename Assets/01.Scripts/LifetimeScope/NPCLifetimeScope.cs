using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.UI;
using MySecondWorld.Network;

namespace MySecondWorld.AI
{
    /// <summary>
    /// NPC 메시지 인프라 + 대화 UI를 담당하는 LifetimeScope.
    /// MainScene에 배치. NPCScope의 부모 스코프.
    /// </summary>
    public class NPCLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<string, PlayerMessageSentEvent>(options);
            builder.RegisterMessageBroker<NPCResponseReceivedEvent>(options);
            builder.RegisterMessageBroker<NPCThinkingStartedEvent>(options);
            builder.RegisterMessageBroker<NPCDialogueEvent>(options);

            var uiManager = Parent.Container.Resolve<UIManager>();
            var uiGO = uiManager.GetOrCreateDynamicCanvas<NPCDialogueUI>();
            builder.RegisterComponent(uiGO.GetComponent<NPCDialogueUI>());
        }
    }
}
