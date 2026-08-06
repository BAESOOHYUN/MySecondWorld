using MySecondWorld.Combat.Companion;

namespace MySecondWorld.AI.Companion
{
    public class CompanionInteractable : NPCInteractable
    {
        [UnityEngine.SerializeField] private CompanionAI _companionAI;

        public override void Interact()
        {
            if (_companionAI != null && _companionAI.IsInCombat)
                return;

            base.Interact();
        }

        protected override void OnConversationStateChanged(bool paused)
        {
            if (_companionAI == null) return;

            if (paused) _companionAI.PauseFollow();
            else _companionAI.ResumeFollow();
        }
    }
}
