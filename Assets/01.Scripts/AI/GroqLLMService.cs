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

        // 원하는 모델 지정 (둘 다 Groq에서 정상 동작 확인 완료)
        private const string MODEL = "qwen/qwen3.8-27b"; // ★ 가장 안정적인 한국어 지원 대화 모델
        // private const string MODEL = "llama-3.3-70b-versatile";

        private string _systemPrompt = "";
        public bool IsReady => true;
        public void SetSystemPrompt(string prompt) => _systemPrompt = prompt;
        public void ClearHistory() { } // Groq는 매 요청이 독립적 — 히스토리 없음

        public async UniTask<string> ChatAsync(string userInput, Action<string> onToken, CancellationToken ct)
        {
            Debug.Log($"[GroqLLM] ChatAsync 호출됨! 입력: '{userInput}', 모델: '{MODEL}'");
            string json = BuildJson(userInput);

            for (int attempt = 0; attempt < 3; attempt++) // 현재 몇 번째 시도인지를 나타냄. 최대 3회 재시도
            {
                try
                {
                    Debug.Log($"[GroqLLM] 시도 {attempt + 1}/3 - PostAsync 시작");
                    string raw = await PostAsync(json, ct);
                    if (raw == null)
                    {
                        Debug.LogWarning($"[GroqLLM] 시도 {attempt + 1}/3 - PostAsync 반환값이 null");
                        return string.Empty;
                    }

                    if (raw == "RATE_LIMIT")
                    {
                        Debug.LogWarning($"[GroqLLM] Rate limit 감지. 대기 후 재시도...");
                        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Pow(2, attempt)), cancellationToken: ct);
                        continue;
                    }

                    string content = ParseContent(raw);
                    if (!string.IsNullOrEmpty(content))
                    {
                        Debug.Log($"[GroqLLM] 답변 수신 완료 ({content.Length}자): {content}");
                        SimulateStreaming(content, onToken);
                        return content;
                    }
                    else
                    {
                        Debug.LogWarning($"[GroqLLM] ParseContent 결과가 비어있음. 원본 응답: {raw}");
                    }
                    return string.Empty;
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning("[GroqLLM] ChatAsync 취소됨 (OperationCanceledException)");
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GroqLLM] ChatAsync 예외: {e.Message}\n{e.StackTrace}");
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        public async UniTask<string> ExtractFactAsync(string prompt, CancellationToken ct)
        {
            string json = BuildJson(prompt);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string raw = await PostAsync(json, ct);
                    if (string.IsNullOrEmpty(raw))
                        return string.Empty;
                    if (raw == "RATE_LIMIT")
                    {
                        // 429 수신 시 1초, 2초, 4초 대기 후 재시도
                        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Pow(2, attempt)), cancellationToken: ct);
                        continue;
                    }
                    string content = ParseContent(raw);
                    return content;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    Debug.LogError($"[GroqLLM] ExtractFactAsync 예외: {e.Message}");
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        private async UniTask<string> PostAsync(string json, CancellationToken ct)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(API_URL, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 10; // ★ 10초 타임아웃 설정 (10초 내 응답 없으면 즉시 오류 반환)
            req.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            req.SetRequestHeader("Content-Type", "application/json"); 
            req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"); // Cloudflare 403 차단 방지

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[GroqLLM] 통신 연결 실패/타임아웃: {e.Message}");
                return null; // 실패 시 즉시 null 반환 -> ChatAsync가 string.Empty 반환
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GroqLLM] HTTP 오류 {req.responseCode}: {req.error} / {req.downloadHandler?.text}");
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
                var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                string text = obj["choices"][0]["message"]["content"]?.ToString() ?? string.Empty;
                return text.Trim().Trim('\0'); // 널 문자 및 공백 정리
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

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}