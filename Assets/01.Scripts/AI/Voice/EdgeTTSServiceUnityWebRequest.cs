using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI.Voice
{
    // Edge-TTS 로컬 브릿지 서버(tts_server.py)를 통해 MP3 오디오를 수신.
    // 직접 WSS 연결은 Unity Mono TLS 한계로 불가 → Python 서버가 대신 처리.
    public class EdgeTTSServiceUnityWebRequest : MonoBehaviour, ITTSService
    {
        private const string ServerUrl = "http://127.0.0.1:5050/tts";

        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private string _voiceName = "ko-KR-SunHiNeural";
        [SerializeField] private string _pitch = "+0Hz";  // ex) +5Hz, -10Hz
        [SerializeField] private string _rate = "+0%";    // ex) +10%, -20%

        public event Action OnSpeakStarted;
        public event Action OnSpeakFinished;

        public async UniTask SpeakAsync(string text, CancellationToken ct = default)
        {
            // 1. JSON 요청 빌드
            string pitch = NormalizePitch(_pitch);
            string rate = NormalizeRate(_rate);
            string json = $"{{\"text\":\"{EscapeJson(text)}\",\"voice\":\"{_voiceName}\",\"pitch\":\"{pitch}\",\"rate\":\"{rate}\"}}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            // 2. HTTP POST → MP3 bytes 수신
            using var req = new UnityWebRequest(ServerUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EdgeTTSService] HTTP 오류: {req.error}. tts_server.py 실행 중인지 확인하세요.");
                return;
            }

            byte[] audioBytes = req.downloadHandler.data;
            if (audioBytes == null || audioBytes.Length == 0)
                return;

            // 3. MP3 → AudioClip 변환 (temp 파일 경유)
            string tempPath = Path.Combine(Application.temporaryCachePath, $"npc_tts_{GetInstanceID()}.mp3");
            File.WriteAllBytes(tempPath, audioBytes);

            using var audioReq = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
            await audioReq.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (audioReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EdgeTTSService] 오디오 로드 실패: {audioReq.error}");
                return;
            }

            // 4. 재생 (clip + Play — GetOutputData() 사용을 위해 PlayOneShot 아님)
            _audioSource.clip = DownloadHandlerAudioClip.GetContent(audioReq);
            _audioSource.Play();
            OnSpeakStarted?.Invoke();

            // 5. 재생 완료 대기
            try
            {
                await UniTask.WaitWhile(() => _audioSource.isPlaying, cancellationToken: ct);
            }
            finally
            {
                OnSpeakFinished?.Invoke();
            }
        }

        public void Stop()
        {
            _audioSource.Stop();
            OnSpeakFinished?.Invoke();
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        // Inspector 구형 값 자동 변환: "1.0" → "+0%", "0%" → "+0%"
        private static string NormalizeRate(string v)
        {
            if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float f))
            {
                int pct = Mathf.RoundToInt((f - 1f) * 100f);
                return pct >= 0 ? $"+{pct}%" : $"{pct}%";
            }
            return v.StartsWith("+") || v.StartsWith("-") ? v : "+" + v;
        }

        // Inspector 구형 값 자동 변환: "0%" → "+0Hz", "+5%" → "+5Hz"
        private static string NormalizePitch(string v)
        {
            if (v.EndsWith("Hz")) return v.StartsWith("+") || v.StartsWith("-") ? v : "+" + v;
            // 숫자+% 형식이면 Hz로 교체
            string stripped = v.TrimStart('+', '-').Replace("%", "");
            if (float.TryParse(stripped, out _))
            {
                string sign = v.StartsWith("-") ? "-" : "+";
                return $"{sign}{stripped}Hz";
            }
            return v;
        }
    }
}
