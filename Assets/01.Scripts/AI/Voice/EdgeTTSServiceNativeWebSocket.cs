using Cysharp.Threading.Tasks;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;

namespace MySecondWorld.AI.Voice
{
    // Edge-TTS: Microsoft Edge 브라우저 TTS 엔진. 무료, API 키 없음.
    public class EdgeTTSServiceNativeWebSocket : MonoBehaviour, ITTSService
    {
        private const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken=6A5AA1D4EAFF4E9FB37E23D68491D6F4&ConnectionId=";
        private const string AudioFormat = "audio-24khz-48kbitrate-mono-mp3";
        [SerializeField] private AudioSource _audioSource;
        // ko-KR-SunHiNeural  — 여성, 밝고 친근함
        // ko-KR-InJoonNeural — 남성, 차분함
        // ko-KR-BongJinNeural— 남성, 중후함
        // ko-KR-GookMinNeural— 남성, 젊음
        // ko-KR-YuJinNeural  — 여성, 활발함
        [SerializeField] private string _voiceName = "ko-KR-SunHiNeural";
        [SerializeField] private string _pitch = "+0Hz";   // ex) +5Hz, -10Hz
        [SerializeField] private string _rate = "+0%";     // ex) +10%, -20% 

        public event Action OnSpeakStarted;
        public event Action OnSpeakFinished;

        private readonly List<WebSocket> _activeConnections = new();

        private void Update()
        {
            // 백그라운드 수신 메시지를 메인 스레드로 전달
            // WebGL 예외. WebGL은 브라우저의 JavaScript WebSocket을 쓰기 때문에 처음부터 메인 스레드에서 동작해서 Dispatch가 필요 없다.
#if !UNITY_WEBGL || UNITY_EDITOR
            foreach (var ws in _activeConnections)
                ws.DispatchMessageQueue();
#endif
        }

        public async UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default)
        {
            // Unity Editor(Windows)에서 ClientWebSocket 내부 사용 시 TLS 인증서 검증 우회
            // System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            //System.Net.ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;

            // 1. 웹소켓 연결 설정
            var secMsGec = GenerateSecMsGec();
            var url = WssUrl + Guid.NewGuid().ToString("N") +
                "&Sec-MS-GEC=" + secMsGec +
                "&Sec-MS-GEC-Version=1-143.0.3650.75";
            // Edge-TTS는 공식 API가 아니라 브라우저인 척 위장해야 작동
            var ws = new WebSocket(url.ToString(), new Dictionary<string, string>
            {
                ["Origin"] = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold",
                ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0",
                ["Pragma"] = "no-cache",
                ["Cache-Control"] = "no-cache",
                ["Accept-Language"] = "en-US,en;q=0.9",
                ["Cookie"] = $"muid={GenerateMuid()};",
            });

            //DispatchMessageQueue()를 Update에서 호출하기 위해 목록에 등록해 둠
            _activeConnections.Add(ws);

            // 2. 수신 핸들러 등록
            var audioBytes = new List<byte>();
            var tcs = new UniTaskCompletionSource<byte[]>();
            bool isDone = false;

            // 2-1. 음성포맷 설정
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string configMsg =
                $"X-Timestamp:{timestamp}\r\n" +
                $"Content-Type:" +
                    $"application/json; " +
                    $"charset=utf-8\r\n" +
                $"Path:speech.config\r\n\r\n" +
                    $"{{\"context\":" +
                        $"{{\"synthesis\":" +
                            $"{{\"audio\":" +
                                $"{{\"metadataoptions\":" +
                                    $"{{\"sentenceBoundaryEnabled\":\"false\"," +
                                      $"\"wordBoundaryEnabled\":\"false\"}}," +
                                $"\"outputFormat\":\"{AudioFormat}\"}}}}}}}}" +
                                "\r\n"; // Python도 JSON 뒤에 \r\n 있음
            Debug.Log($"[TTS] configMsg:\n{configMsg}");
            // 2-2. 목소리 설정
            string ssml =
                $"<speak " +                                         
                    $"version='1.0' " +                              
                    $"xmlns='http://www.w3.org/2001/10/synthesis' " +
                    $"xml:lang='ko-KR'>" +                           
                $"<voice " +                                         
                    $"name='{_voiceName}'>" +
                $"<prosody " +
                    $"pitch='{_pitch}' " +
                    $"rate='{_rate}'>" +
                $"{text}" +
                $"</prosody></voice></speak>";
            // 2-3. 읽을 텍스트
            string ssmlMsg =
                $"X-RequestId:{Guid.NewGuid():N}\r\n" +
                $"Content-Type:application/ssml+xml\r\n" +
                $"X-Timestamp:{timestamp}\r\n" +
                $"Path:ssml\r\n\r\n{ssml}";
            Debug.Log($"[TTS] ssmlMsg:\n{ssmlMsg}");
            // 이벤트와 await를 연결하는 다리가 UniTaskCompletionSource = tcs
            // NativeWebSocket 버전은 OnOpen 이벤트 안에서 순차 전송:
            ws.OnOpen += async () =>
            {
                await ws.SendText(configMsg); // 연결되면 그때 전송
                await ws.SendText(ssmlMsg); // 그 다음
            };
            ws.OnMessage += (byte[] msg) =>
            {
                string decoded = Encoding.UTF8.GetString(msg);
                // Path:turn.end 메시지가 왔을 때 호출하도록 설정해뒀으니,
                // Edge-TTS가 오디오 전송을 끝냈다는 신호가 오면 그때 await가 완료된다.
                if (decoded.Contains("Path:turn.end"))
                {
                    Debug.Log($"[TTS] turn.end 수신. 누적 오디오: {audioBytes.Count} bytes");
                    isDone = true;
                    tcs.TrySetResult(audioBytes.ToArray()); //tcs.TrySetResult()가 호출되는 순간 await가 풀리고 다음 코드로 넘어감.
                    _ = ws.Close();
                    return;
                }

                // 청크 수신 : 앞 2바이트 = 헤더길이(big-endian uint16), 그 뒤가 실제 MP3 데이터
                if (msg.Length >= 2)
                {
                    int headerLen = (msg[0] << 8) | msg[1];
                    int audioStart = 2 + headerLen;
                    if (msg.Length > audioStart)
                    {
                        int added = msg.Length - audioStart;
                        audioBytes.AddRange(new ArraySegment<byte>(msg, audioStart, added));
                        Debug.Log($"[TTS] 바이너리 청크 수신. 추가: {added} bytes, 누적: {audioBytes.Count} bytes");
                    }
                    else
                    {
                        Debug.Log($"[TTS] 텍스트: {decoded}"); // 전체 출력
                    }
                }
            };
            ws.OnError += (string err) => tcs.TrySetException(new Exception(err));
            ws.OnClose += (_) =>
            {
                Debug.Log($"[TTS] OnClose. isDone={isDone}, audioBytes={audioBytes.Count}");
                if (!isDone)
                    tcs.TrySetResult(audioBytes.ToArray());
            };

            // NPC 파괴 시 진행 중인 연결 정리
            ct.Register(() =>
            {
                tcs.TrySetCanceled();   // await tcs.Task를 취소 예외로 풀어버림
                _ = ws.Close();         // 열려있는 WebSocket 연결 닫음. _는 반환값(Task)을 무시하겠다는 표현 fire-and-forget
            });

            // 3. 연결 시작 + 오디오 수신 대기
            await ws.Connect(); // ← 연결 시작. 완료되면 OnOpen이 자동으로 불림

            byte[] mp3Bytes;
            try
            {
                mp3Bytes = await tcs.Task; // ← 여기서 실제로 기다림
            }
            finally
            {
                _activeConnections.Remove(ws);
                if(ws.State== WebSocketState.Open)
                    _ = ws.Close();
            }

            // 4. MP3 → AudioClip 변환 (temp 폴더 경유)
            // Guid suffix: 동시 fetch 시 temp 파일 충돌 방지
            if (mp3Bytes == null || mp3Bytes.Length == 0)
                return null;

            string tempPath = Path.Combine(Application.temporaryCachePath, $"npc_tts_{GetInstanceID()}_{Guid.NewGuid():N}.mp3");
            File.WriteAllBytes(tempPath, mp3Bytes);

            // UnityWebRequestMultimedia.GetAudioClip : temp 폴더에 저장한 MP3 파일을 읽어서 AudioClip으로 디코딩해주는 역할
            using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EdgeTTSServiceNative] 오디오 로드 실패 : {req.error}");
                return null;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            File.Delete(tempPath);
            return clip;
        }

        public async UniTask PlayAsync(AudioClip clip, CancellationToken ct = default)
        {
            if (clip == null) return;

            // clip + Play() 고정 — PlayOneShot은 GetOutputData()로 진폭을 못 읽음
            _audioSource.clip = clip;
            _audioSource.Play();
            OnSpeakStarted?.Invoke();
            try
            {
                await UniTask.WaitWhile(()=> _audioSource.isPlaying, cancellationToken:ct);
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

        private static string GenerateSecMsGec() // Generate Security Microsoft Gec(Microsoft 내부 약어, 정확한 풀네임은 공개 안 됨)
        {
            // 5분마다 바뀌는 입장권
            // TrustedClientToken은 고정값이라 한번 유출되면 누구나 쓸 수 있음
            // Microsoft가 시각 기반으로 매번 달라지는 추가 검증 값을 넣어서 재사용을 막음
            // Sec-MS-GEC 계산 방식: 현재 시각(5분 단위로 내림) +TrustedClientToken → SHA256 해시

            // Windows FILETIME = 100나노초 단위, 1601-01-01 기준
            long fileTime = DateTime.UtcNow.ToFileTimeUtc();
            // 5분(300초 = 3,000,000,000 틱) 단위로 내림
            long rounded = fileTime - (fileTime % 3_000_000_000L);
            // TrustedClientToken Edge. 브라우저 소스코드에 하드코딩된 값. (WssUrl 상수에도 이미 들어가 있음)
            string strToHash = $"{rounded}6A5AA1D4EAFF4E9FB37E23D68491D6F4"; 
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(strToHash));
            return BitConverter.ToString(hash).Replace("-", "").ToUpper();
        }

        private static string GenerateMuid() // Generate Machine Unique Id
        {
            // Cookie 헤더에 muid={값} 형태로 실린다.
            // Edge 브라우저가 기기마다 고유 ID를 쿠키로 보내는 걸 흉내 낸 것.
            // 값 자체는 랜덤 16바이트라 실제로 추적용은 아니고, 그냥 "브라우저처럼 보이게" 하는 용도
            byte[] bytes = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }

        // Python의 date_to_string()과 동일한 형식
        private static string DateToString()
        {
            return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture)
                + " GMT+0000 (Coordinated Universal Time)";
        }
    }
}