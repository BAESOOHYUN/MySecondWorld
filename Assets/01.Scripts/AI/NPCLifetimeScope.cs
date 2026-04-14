using VContainer;
using VContainer.Unity;
using MessagePipe;

namespace MySecondWorld.AI
{
    public class NPCLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // RegisterMessagePipe()
            // MessagePipe 시스템 자체를 컨테이너에 등록한다.
            // IPublisher, ISubscriber 같은 인터페이스들이 동작하기 위한 기반 설정이다.
            // 이걸 먼저 해야 MessagePipe가 VContainer 위에서 돌아간다.

            // RegisterMessageBroker<T>(options)
            // T 타입 메시지 하나에 대해 Publisher/ Subscriber 쌍을 등록한다.
            // 이걸 해야 IPublisher<T> 와 ISubscriber<T> 를 주입받을 수 있다.

            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<PlayerMessageSentEvent>(options);
            builder.RegisterMessageBroker<NPCResponseReceivedEvent>(options);
            builder.RegisterMessageBroker<NPCThinkingStartedEvent>(options);

            // RegisterMessageBroker는 순수 C# 객체를 등록한다. 
            // 그런데 NPCBrain은 MonoBehaviour다. MonoBehaviour는 new NPCBrain() 으로 생성할 수 없다.
            // 순수 C# 객체 → Register<T>() — VContainer가 직접 new로 만든다
            // MonoBehaviour → RegisterComponentInHierarchy<T>() — 씬 Hierarchy에서 찾아서 가져온다

            // RegisterComponentInHierarchy는 FindObjectOfType<T>()와 비슷하게 동작한다고 보면 된다.
            // 씬에 이미 올라가 있는 컴포넌트를 VContainer가 관리 대상으로 등록하는 것이다.

            builder.RegisterComponentInHierarchy<NPCBrain>();
            builder.RegisterComponentInHierarchy<NPCInteractable>();
            builder.RegisterComponentInHierarchy<NPCDialogueUI>();

        }
    }
}