using Cysharp.Threading.Tasks;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using OpenAI;
using OpenAI.Chat;

namespace MySecondWorld.AI
{
    public class GroqLLMService : ILLMService
    {
        private const string API_KEY = "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";
        private const string MODEL = "llama-3.3-70b-versatile";
        private readonly ChatClient _client;
        private string _systemPrompt = "";
        public bool IsReady => true;

        public GroqLLMService()
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.groq.com/openai/v1")
            };
            _client = new ChatClient(MODEL, new ApiKeyCredential(API_KEY), options);
        }

        public void SetSystemPrompt(string prompt) =>_systemPrompt = prompt;
        public void ClearHistory() { } // Groq는 매 요청이 독립적 — 히스토리 없음


        public async UniTask<string> ChatAsync(string userInput, Action<string> onToken, CancellationToken ct)
        {
            var messages = new List<ChatMessage>
            {
                // OpenAI .NET SDK가 제공하는 클래스
                new SystemChatMessage(_systemPrompt),   // role: system인 메시지 객체
                new UserChatMessage(userInput)          // role: user인 메시지 객체
            };

            for (int attempt = 0; attempt < 4; attempt++) // 현재 몇 번째 시도인지를 나타냄. 최대 3회 재시도
            {
                try
                {
                    var sb = new StringBuilder();
                    // CompleteChatStreamingAsync : LLM이 답변을 만들 때 완성을 기다리지 않고 생성되는 즉시 토큰 단위로 받음
                    await foreach (var update in _client.CompleteChatStreamingAsync(messages, cancellationToken: ct))
                    {
                        foreach(var part in update.ContentUpdate)
                        {
                            sb.Append(part.Text);
                            onToken?.Invoke(sb.ToString());
                        }
                    }
                    return sb.ToString();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) 
                {
                    if (attempt < 3 && e.Message.Contains("429")) //요청 한도 초과 오류
                    {
                        // 기다릴수록 서버가 한도를 회복할 가능성이 높아지기 때문에, 실패할수록 더 오래 기다리는 전략을 씀
                        // 2의 attempt제곱 = Exponential Backoff(지수 백오프)
                        // attempt 0 → 2^0 = 1초 대기 후 재시도
                        // attempt 1 → 2 ^ 1 = 2초 대기 후 재시도
                        // attempt 2 → 2 ^ 2 = 4초 대기 후 재시도
                        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Pow(2, attempt)), cancellationToken: ct);
                    }
                    else
                    {
                        Debug.LogError($"[GroqLLM] ChatAsync 오류: {e.Message}");
                        return string.Empty;
                    }
                }
            }
            return string.Empty;
        }

        public async UniTask<string> ExtractFactAsync(string prompt, CancellationToken ct)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(_systemPrompt),
                new UserChatMessage(prompt)
            };

            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    var result = await _client.CompleteChatAsync(messages, cancellationToken: ct);
                    return result.Value.Content[0].Text;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    if (attempt < 3 && e.Message.Contains("429"))
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Pow(2, attempt)), cancellationToken: ct);
                    }
                    else
                    {
                        Debug.LogError($"[GroqLLM] ExtractFactAsync 오류: {e.Message}");
                        return string.Empty;
                    }
                }
            }
            return string.Empty;
        }
    }
}