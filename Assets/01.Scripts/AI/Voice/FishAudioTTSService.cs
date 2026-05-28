using Cysharp.Threading.Tasks;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI.Voice
{
    public class FishAudioTTSService : MonoBehaviour, ITTSService
    {
        private const string API_URL = "https://api.fish.audio/v1/tts";
        private const string API_KEY = "04499204a44d489db1fc6604e72c6736";       // fish.audio API 키
        [SerializeField] private NPCSystemPromptSO _promptSO;  // 음성 reference_id (비우면 기본 음성)
        [SerializeField, Range(0.5f, 2f)] private float _speed = 1.0f;

        public async UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default)
        {
            string refPart = string.IsNullOrEmpty(_promptSO.FishAudioReferenceId) ? "" : $",\"reference_id\":\"{_promptSO.FishAudioReferenceId}\"";
            string json = $"{{\"text\":\"{EscapeJson(text)}\",\"model\":\"speech-1.6\",\"format\":\"wav\",\"sample_rate\":44100{refPart},\"prosody\":{{\"speed\":{_speed:F2}}}}}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(API_URL, "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            req.SetRequestHeader("Content-Type", "application/json");
            //req.SetRequestHeader("model", "s2-pro");

            float t0 = Time.realtimeSinceStartup;

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[FishAudio] 연결 실패: {e.Message}");
                return null;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FishAudio] HTTP {req.responseCode}: {req.downloadHandler.text}");
                return null;
            }

            Debug.Log($"[FishAudio] TTS 완료: {Time.realtimeSinceStartup - t0:F2}s");

            byte[] wavBytes = req.downloadHandler.data;
            if (wavBytes == null || wavBytes.Length == 0) 
                return null;
            return ParseWav(req.downloadHandler.data);
        }

        // WAV bytes → AudioClip (16비트 PCM 기준)
        private static AudioClip ParseWav(byte[] wav)
        {
            // 'data' 청크 위치 탐색 (표준 44바이트 헤더 외에 fmt 확장이 있을 수 있음)
            int dataOffset = 44;
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (wav[i] == 'd' &&
                    wav[i + 1] == 'a' &&
                    wav[i + 2] == 't' &&
                    wav[i + 3] == 'a')
                { 
                    dataOffset = i + 8; 
                    break;
                }
            }

            int channels = wav[22] | (wav[23] << 8);
            int sampleRate = wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24);
            int pcmLen = (wav.Length - dataOffset) / 2; // Int16 샘플 수

            float[] samples = new float[pcmLen];

            for (int i = 0; i < pcmLen; i++)
            {
                short s = (short)(wav[dataOffset + i * 2] | (wav[dataOffset + i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }

            var clip = AudioClip.Create("FishAudio", pcmLen / channels, channels, sampleRate, false);

            clip.SetData(samples, 0);
            return clip;
        }

        // JSON 특수문자 이스케이프
        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"")
             .Replace("\n", "\\n").Replace("\r", "\\r");
    }
}