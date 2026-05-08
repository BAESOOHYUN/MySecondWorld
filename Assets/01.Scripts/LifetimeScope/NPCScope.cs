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
            builder.RegisterComponent(GetComponent<NPCVoicePlayer>());
            builder.RegisterComponent(GetComponent<EdgeTTSServiceUnityWebRequest>()).As<ITTSService>(); // 씬에서 EdgeTTSService 찾아서 등록, .As 뜻 'ITTSService로 달라
        }
    }
}