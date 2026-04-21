using VContainer;
using VContainer.Unity;

namespace MySecondWorld.AI
{
    public class NPCScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(GetComponent<NPCBrain>());
            builder.RegisterComponent(GetComponent<NPCInteractable>());
        }
    }
}