using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI
{
    public class GroqLLMService : ILLMService
    {
        private const string API_URL = "https://api.groq.com/openai/v1/chat/completions";
        private const string API_KEY = "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";
        private const string MODEL = "llama-3.3-70b-versatile";
        private string _systemPrompt = "";
        public bool IsReady => true;
        public void SetSystemPrompt(string prompt) => _systemPrompt = prompt;
        public void ClearHistory() { } // Groq는 매 요청이 독립적 — 히스토리 없음

        public async UniTask<string> ChatAsync(string userInput, Action<string> onToken, CancellationToken ct)
        {
            string json = BuildJson(userInput);

            for (int attempt = 0; attempt < 4; attempt++) // 현재 몇 번째 시도인지를 나타냄. 최대 3회 재시도
            {
                try
                {
                    string raw = await PostAsync(json, ct);
                    if (raw == null)
                        return string.Empty;
                    if (raw == "RATE_LIMIT")
                    {
                        if (attempt < 3)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Pow(2, attempt)), cancellationToken: ct);
                            continue;
                        }
                        return string.Empty;
                    }
                    string content = ParseContent(raw);
                    Debug.Log($"[GroqLLM] 응답 수신: {content.Length}자");
                    SimulateStreaming(content, onToken);
                    return content;
                }
                catch (OperationCanceledException) { throw; }
            }
            return string.Empty;
        }

        public async UniTask<string> ExtractFactAsync(string prompt, CancellationToken ct)
        {
            string json = BuildJson(prompt);

            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    string raw = await PostAsync(json, ct);
                    if (raw == null)
                        return string.Empty;
                    if (raw == "RATE_LIMIT")
                    {
                        if (attempt < 3)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Pow(2, attempt)), cancellationToken: ct);
                            continue;
                        }
                        return string.Empty;
                    }
                    return ParseContent(raw);
                }
                catch (OperationCanceledException) { throw; }
            }
            return string.Empty;
        }

        private async UniTask<string> PostAsync(string json, CancellationToken ct)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(API_URL, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[GroqLLM] 연결 실패: {e.Message}");
                return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GroqLLM] HTTP {req.responseCode}: {req.error}");
                return req.responseCode == 429 ? "RATE_LIMIT" : null;
            }

            return req.downloadHandler.text;
        }

        private string BuildJson(string userInput)
        {
            string sys = EscapeJson(_systemPrompt);
            string usr = EscapeJson(userInput);
            return $"{{\"model\":\"{MODEL}\",\"stream\":false,\"messages\":[{{\"role\":\"system\",\"content\":\"{sys}\"}},{{\"role\":\"user\",\"content\":\"{usr}\"}}]}}";
        }

        // {"choices":[{"message":{"content":"..."}}]} 에서 content 추출
        private static string ParseContent(string json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GroqLLM] JSON 파싱 실패: {e.Message}");
                return string.Empty;
            }
        }

        // non-streaming 응답을 문장 경계에서 나눠 onToken 호출
        // → NPCBrain의 문장 감지 + TTS 파이프라인 그대로 호환
        private static void SimulateStreaming(string text, Action<string> onToken)
        {
            if (string.IsNullOrEmpty(text) || onToken == null)
                return;

            var sb = new StringBuilder();
            foreach (char c in text)
            {
                sb.Append(c);
                if (c == '.' || c == '!' || c == '?' || c == '\n')
                    onToken.Invoke(sb.ToString());
            }
            // 마지막 갱신 (마침표 없이 끝나는 경우 대비)
            onToken.Invoke(sb.ToString());
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"")
             .Replace("\n", "\\n").Replace("\r", "\\r");
    }
}