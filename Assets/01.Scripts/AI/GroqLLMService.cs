using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI
{
    public class GroqLLMService : ILLMService
    {
        private const string API_URL = "https://api.groq.com/openai/v1/chat/completions";
        private static readonly string API_KEY = SecretKeyLoader.Load("groq_api_key");
        private string _systemPrompt = "";
        private int _maxTokens = 40;
        private readonly NPCCommonRulesSO _rules;
        public GroqLLMService(NPCCommonRulesSO rules) { _rules = rules; }
        public bool IsReady => true;

        // 추론 모델이 답 앞에 붙이는 <think>...</think> 블록
        private static readonly Regex ThinkBlock = new(@"<think>.*?</think>", RegexOptions.Singleline);
        // max_tokens에 잘려 닫히지 않은 <think>... (뒤는 전부 생각이므로 통째로 버린다)
        private static readonly Regex UnclosedThink = new(@"<think>.*$", RegexOptions.Singleline);


        public void SetSystemPrompt(string prompt, int maxTokens)
        {
            _systemPrompt = prompt;
            _maxTokens = maxTokens > 0 ? maxTokens : 40;
        }

        public void ClearHistory() { } // Groq는 매 요청이 독립적 — 히스토리 없음

        public async UniTask<string> ChatAsync(string userInput, IReadOnlyList<(string user, string npc)> history, 
                                            Action<string> onToken, CancellationToken ct)
        {
            Debug.Log($"[GroqLLM] ChatAsync 호출됨! 입력: '{userInput}', 모델: '{_rules.Model}'");
            string json = BuildJson(userInput, history);

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
                        Debug.LogWarning($"[GroqLLM] 빈 응답 요청 본문(임시 진단): {json}");
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

                    // 429 = HTTP "Too Many Requests". Groq는 분당 요청 수·토큰 수 제한이 있고, 넘으면 429로 거절한다.
                    // 잠깐 기다리면 풀리는 오류라 바로 포기하지 않고 1초 → 2초 → 4초(2^attempt) 간격으로 대기 후 재시도한다.
                    if (raw == "RATE_LIMIT")
                    {
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
            // timeout : 요청을 보낸 시점부터 응답을 다 받을 때까지의 전체 제한(연결 + 서버 처리 + 다운로드 합산).
            // 응답이 오는 도중이어도 '10초를 초과'하면 SendWebRequest가 실패로 끝나 즉시 null 오류가 반환된다.
            // 이 경우 재시도 없이 빈 답이 나가므로, 느린 모델에서는 이 값이 짧을 수 있다.
            req.timeout = 10;
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

        // 대화의 연속성을 위한 메서드. 이전 대화의 흐름을 기억하고 현재 대화에 반영한다.
        private string BuildJson(string userInput, IReadOnlyList<(string user, string npc)> history = null)
        {
            using var sb = ZString.CreateStringBuilder();
            // NPC 성격, 규칙, 기억 요약
            sb.Append("{\"model\":\""); sb.Append(_rules.Model);
            sb.Append("\",\"stream\":false,\"max_tokens\":"); sb.Append(_maxTokens);
            sb.Append(_rules.SamplingJson);
            sb.Append(",\"messages\":[{\"role\":\"system\",\"content\":\"");
            sb.Append(EscapeJson(_systemPrompt)); sb.Append("\"}");

            if (history != null)
                foreach (var (u, n) in history)
                {
                    // 빈 content는 API가 거절할 수 있음
                    if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(n)) continue;
                    // 과거에 플레이어가 한 말(u)
                    sb.Append(",{\"role\":\"user\",\"content\":\""); sb.Append(EscapeJson(u)); sb.Append("\"}");
                    // 과거에 NPC가 한 답(n)
                    sb.Append(",{\"role\":\"assistant\",\"content\":\""); sb.Append(EscapeJson(n)); sb.Append("\"}");
                }
            // 지금 방금 플레이어가 한 말(userInput)
            sb.Append(",{\"role\":\"user\",\"content\":\""); sb.Append(EscapeJson(userInput)); sb.Append("\"}]}");
            return sb.ToString();
        }

        // {"choices":[{"message":{"content":"..."}}]} 에서 content 추출
        private static string ParseContent(string json)
        {
            try
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                string text = obj["choices"][0]["message"]["content"]?.ToString() ?? string.Empty;
                string cleaned = UnclosedThink.Replace(ThinkBlock.Replace(text, ""), "");
                return cleaned.Trim().Trim('\0');   // 널 문자 및 공백 정리
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

            using var sb = ZString.CreateStringBuilder();
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