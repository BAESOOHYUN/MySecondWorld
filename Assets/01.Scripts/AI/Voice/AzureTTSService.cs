using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI.Voice
{
    public class AzureTTSService : MonoBehaviour, ITTSService
    {
        private const string API_KEY = "0NEG81FsXzCUur9p8HIHlhJtzuejMgWaKNGTUGFjP2X7A6ftAqyvJQQJ99CEACNns7RXJ3w3AAAYACOGKogd";
        private const string REGION = "koreacentral";
        [SerializeField] private string _voiceName = "ko-KR-SunHiNeural";
        [SerializeField] private NPCSystemPromptSO _promptSO;

        public async UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default)
        {
            string style = _promptSO != null ? _promptSO.AzureTTSStyle : "cheerful";
            string ssml = BuildSsml(text, style);
            Debug.Log($"[AzureTTS] SSML: {ssml}");
            string url = $"https://{REGION}.tts.speech.microsoft.com/cognitiveservices/v1";

            byte[] mp3Bytes;
            try
            {
                mp3Bytes = await PostSsmlAsync(url, ssml, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[AzureTTS] 요청 실패: {e.Message}");
                return null;
            }

            if (mp3Bytes == null || mp3Bytes.Length == 0)
                return null;

            return await Mp3ToClipAsync(mp3Bytes, ct);
        }

        private string BuildSsml(string text, string style)
        {
            // SSML: "음성을 어떻게 읽을지" 지시하는 마크업 언어. speak, voice, prosody 같은 기본 태그는 W3C가 정의한 국제 표준. Edge-TTS의 SSML도 이 규격을 씀
            // mstts: 네임스페이스는 Microsoft가 Azure Speech Service에만 추가한 확장 태그. W3C 표준에는 없고 Azure 문서에 정의되어 있음
            // ※ mstts 네임스페이스 필수 — 없으면 express-as 태그 무시됨.
            return
                $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
                $"xmlns:mstts=\"http://www.w3.org/2001/mstts\" xml:lang=\"ko-KR\">" +
                $"<voice name=\"{_voiceName}\">" +
                $"<mstts:express-as style=\"{style}\">" + // 중요!
                $"{text}" +
                "</mstts:express-as></voice></speak>";
        }

        private async UniTask<byte[]> PostSsmlAsync(string url, string ssml, CancellationToken ct)
        {
            byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(ssml);
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer(); 
            req.SetRequestHeader("Ocp-Apim-Subscription-Key", API_KEY);
            req.SetRequestHeader("Content-Type", "application/ssml+xml");
            req.SetRequestHeader("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");

            await req.SendWebRequest().ToUniTask(cancellationToken: ct);
            Debug.Log($"[AzureTTS] 응답코드: {req.responseCode}, 바이트: {req.downloadHandler.data?.Length}");

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AzureTTS] HTTP {req.responseCode}: {req.error}");
                return null;
            }

            return req.downloadHandler.data;
        }

        private async UniTask<AudioClip> Mp3ToClipAsync(byte[] mp3Bytes, CancellationToken ct)
        { 
            // EdgeTTS와 동일 패턴: temp 파일 → UnityWebRequestMultimedia
            string tempPath = Path.Combine(
                Application.temporaryCachePath,
                $"npc_azure_tts_{GetInstanceID()}_{Guid.NewGuid():N}.mp3");

            File.WriteAllBytes(tempPath, mp3Bytes);

            using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            File.Delete(tempPath);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AzureTTS] 오디오 로드 실패: {req.error}");
                return null;
            }
            return DownloadHandlerAudioClip.GetContent(req);
        }
    }
}