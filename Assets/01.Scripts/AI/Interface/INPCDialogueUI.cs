using R3;

namespace MySecondWorld.AI
{
    public interface INPCDialogueUI
    {
        Observable<string> OnSubmit { get; }
        Observable<Unit> OnClose { get; }
        void Open(string npcGuid, NPCMemorySO memory = null, NPCSystemPromptSO systemPrompt = null);
        void Close();
        void SetInputEnabled(bool enabled);
        void ShowThinking();
        void ClearInput();
    }
}
