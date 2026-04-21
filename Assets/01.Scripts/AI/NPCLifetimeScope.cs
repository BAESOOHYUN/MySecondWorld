using VContainer;
using VContainer.Unity;
using MessagePipe;

namespace MySecondWorld.AI
{
    public class NPCLifetimeScope : LifetimeScope // 기본적으로 MonoBehaviour를 상속.
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // RegisterMessagePipe()
            // MessagePipe 시스템 자체를 컨테이너에 등록한다.
            // IPublisher, ISubscriber 같은 인터페이스들이 동작하기 위한 기반 설정이다.
            // 이걸 먼저 해야 MessagePipe가 VContainer 위에서 돌아간다.

            // RegisterMessageBroker<T>(options)
            // RegisterMessageBroker는 메시지 타입(struct 권장, class)을 등록한다. 
            // T 타입 메시지 하나에 대해 Publisher/ Subscriber 쌍을 등록한다.
            // 이걸 해야 IPublisher<T> 와 ISubscriber<T> 를 주입받을 수 있다.

            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<string, PlayerMessageSentEvent>(options);
            builder.RegisterMessageBroker<NPCResponseReceivedEvent>(options);
            builder.RegisterMessageBroker<NPCThinkingStartedEvent>(options);

            builder.RegisterComponentInHierarchy<NPCDialogueUI>();
        }
    }
}