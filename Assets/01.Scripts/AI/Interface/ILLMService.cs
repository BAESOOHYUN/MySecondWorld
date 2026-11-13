using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.AI
{
    public interface ILLMService
    {
        public bool IsReady { get; }
        public void SetSystemPrompt(string prompt, int maxTokens);
        public void ClearHistory();
        // history = 이전 대화 쌍(오래된 것이 앞). 없으면 null
        public UniTask<string> ChatAsync(string userInput, IReadOnlyList<(string user, string npc)> history,
                                        Action<string> onToken, CancellationToken ct);
        public UniTask<string> ExtractFactAsync(string prompt, CancellationToken ct);
    }
}
