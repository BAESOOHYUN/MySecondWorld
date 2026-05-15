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

        public async UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default)
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
                return null;
            }

            byte[] audioBytes = req.downloadHandler.data;
            if (audioBytes == null || audioBytes.Length == 0)
                return null;

            // 3. MP3 → AudioClip 변환 (temp 폴더 경유)
            // Guid suffix: FetchAsync가 동시에 여러 개 실행될 때 파일 충돌 방지
            string tempPath = Path.Combine(Application.temporaryCachePath, $"npc_tts_{GetInstanceID()}_({Guid.NewGuid():N}.mp3"); //GetInstanceID = NPC 고유 ID, Guid.NewGuid():N = 한 NPC가 문장 여러개 
            File.WriteAllBytes(tempPath, audioBytes);

            using var audioReq = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
            await audioReq.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (audioReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EdgeTTSService] 오디오 로드 실패: {audioReq.error}");
                return null;
            }

            // 4. 재생 (clip + Play — GetOutputData() 사용을 위해 PlayOneShot 아님)
            var clip = DownloadHandlerAudioClip.GetContent(audioReq);
            File.Delete(tempPath); // AudioClip이 메모리에 올라왔으니 temp 파일 즉시 삭제 (안 지우면 temp 폴더에 파일이 계속 쌓임)
            return clip;
        }

        public async UniTask PlayAsync(AudioClip clip, CancellationToken ct = default)
        {
            if (clip == null) 
                return;
            _audioSource.clip = clip;
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

        public async UniTask SpeakAsync(string text, CancellationToken ct = default)
        {
            var clip = await FetchAsync(text, ct);
            await PlayAsync(clip, ct);
        }

        public void Stop()
        {
            _audioSource.Stop();
            OnSpeakFinished?.Invoke();
        }

        // JSON 문자열 안에 특수문자가 들어오면 JSON이 깨지는 걸 방지
        // 원본 변환   이유
        //  \	 \\	  역슬래시 자체가 이스케이프 문자라서
        //  "	 \"	  JSON 문자열 구분자
        //  \n	 \\n  줄바꿈
        //  \r	 \\r  캐리지 리턴
        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        // rate: 배속값(0.5~2.0) → 상대% (-50%~+100%)
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

        // pitch: Hz 없으면 붙임, 부호 없으면 + 붙임
        private static string NormalizePitch(string v)
        {
            if (v.EndsWith("Hz"))
                return v.StartsWith("+") || v.StartsWith("-") ? v : "+" + v;

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
