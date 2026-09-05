using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.AI
{
    public interface ILLMService
    {
        public bool IsReady { get; }
        public void SetSystemPrompt(string prompt, int maxTokens);
        public void ClearHistory();
        public UniTask<string> ChatAsync(string userInput, Action<string> onToken, CancellationToken ct);
        public UniTask<string> ExtractFactAsync(string prompt, CancellationToken ct);
    }
}
