using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.AI.Voice
{
    // Microsoft Edge 브라우저에서 사용하는 TTS 엔진 edge-tts.
    // 무료, API 키 없음, 한국어 고품질 (ko-KR-SunHiNeural)
    public class EdgeTTSServiceClientWebSocket : MonoBehaviour, ITTSService
    {
        private const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken=6A5AA1D4EAFF4E9FB37E23D68491D6F4&ConnectionId=";

        // 샘플링 레이트 24,000Hz — 1초에 소리를 24,000번 측정
        // 비트레이트 48kbps — 1초당 48,000비트로 압축
        // mono 단일 채널 (스테레오 아님)
        // MP3 압축 포맷
        private const string AudioFormat = "audio-24khz-48kbitrate-mono-mp3";

        [SerializeField] private AudioSource _audioSource;
        // ko-KR-SunHiNeural     — 여성, 밝고 친근함
        // ko-KR-InJoonNeural    — 남성, 차분함
        // ko-KR-BongJinNeural   — 남성, 중후함
        // ko-KR-GookMinNeural   — 남성, 젊음
        // ko-KR-YuJinNeural     — 여성, 활발함
        [SerializeField] private string _voiceName = "ko-KR-SunHiNeural";
        [SerializeField] private string _pitch = "0%";   // -50% ~ +50%
        [SerializeField] private string _rate = "1.0";  // 0.5 ~ 2.0

        public event Action OnSpeakStarted;
        public event Action OnSpeakFinished;

        public async UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default)
        {
            // Unity에서 WSS 연결 시 TLS 버전 불일치로 실패하는 경우 방지
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;

            // 1. 웹소켓 연결
            // Guid를 문자열로 바꿀 때 포맷 지정
            // "D" → 6ba7b810 - 9dad - 11d1 - 80b4 - 00c04fd430c8(하이픈 있음)
            // "N" → 6ba7b8109dad11d180b400c04fd430c8(하이픈 없음)
            // "B" → {6ba7b810 - 9dad - 11d1 - 80b4 - 00c04fd430c8}(중괄호)
            var uri = new Uri(WssUrl + Guid.NewGuid().ToString("N"));
            var ws = new ClientWebSocket();

            // * Edge-TTS는 공식 API가 아니라서 브라우저인 척 위장해야 작동한다.
            // Edge-TTS API는 원래 Edge 브라우저 확장 프로그램 전용. 서버가 이 Origin 헤더를 보고 "Edge 브라우저에서 온 요청이구나" 하고 허용
            // 랜덤처럼 보이는 문자열은 Microsoft의 Edge Read Aloud 확장 프로그램 ID로, 일종의 신분증 위장.
            ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
            ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44");
            ws.Options.SetRequestHeader("Pragma", "no-cache");
            ws.Options.SetRequestHeader("Cache-Control", "no-cache");

            await ws.ConnectAsync(uri, ct);

            // 2. 음성포맷 설정 전송
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"); // fff — milliseconds (1/1000초). f 개수가 정밀도, Z — UTC(협정 세계시) 표시
            string configMsg =
                $"X-Timestamp:{timestamp}\r\n" + //요청 시각
                $"Content-Type:" +
                    $"application/json; " +
                    $"charset=utf-8\r\n" +      //본문이 JSON, utf-8이다
                $"Path:speech.config\r\n\r\n" + //"이건 설정 메시지다" 라고 서버에 알림
                    $"{{\"context\":" +         //{{ 이 이중 중괄호는 C# 문자열 보간($"")에서 {를 문자 그대로 쓰려면 {{로 이스케이프해야 해서다.
                        $"{{\"synthesis\":" +
                            $"{{\"audio\":" + 
                                $"{{\"metadataoptions\":" +
                                    $"{{\"sentenceBoundaryEnabled\":\"false\"," +   //문장 경계 알림 끔
                                      $"\"wordBoundaryEnabled\":\"false\"}}," +     //wordBoundaryEnabled 단어 경계 알림 끔
                                $"\"outputFormat\":\"{AudioFormat}\"}}}}}}}}";      // 오디오 포맷
            await SendTextAsync(ws, configMsg, ct);

            // 3. 목소리 + 읽을 텍스트 전송 (SSML)
            // Speech Synthesis Markup Language — 음성 합성용 XML
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string ssml =
                $"<speak " +                                            //SSML 루트 태그
                    $"version='1.0' " +                                 //SSML 버전
                    $"xmlns='http://www.w3.org/2001/10/synthesis' " +   //W3C SSML 표준 따름 선언
                    $"xml:lang='ko-KR'>" +                              //기본 언어: 한국어
                $"<voice " +                                            //목소리·피치·속도를 다르게 세팅하면 완전히 다른 캐릭터처럼 들림
                    $"name='{_voiceName}'>" +
                $"<prosody " +
                    $"pitch='{_pitch}'" +    
                    $"rate='{_rate}'>" +     
                $"{text}" +
                $"</prosody></voice></speak>";
            string ssmlMsg =
                $"X-RequestId:{Guid.NewGuid():N}\r\n" +
                $"Content-Type:application/ssml+xml\r\n" +
                $"X-Timestamp:{timestamp}\r\n" +
                $"Path:ssml\r\n\r\n{ssml}";
            await SendTextAsync(ws, ssmlMsg, ct);

            // 4. 오디오 바이트 수신
            var audioBytes = new List<byte>();
            var buffer = new byte[8192]; // 8192(8KB)는 모바일 포함 일반적으로 쓰는 적당한 크기
            bool finished = false;

            while (!finished && ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msgBuffer = new List<byte>();
                WebSocketReceiveResult result;
                WebSocketMessageType msgType;

                // 메시지가 왔는지 안 왔는지는 받아봐야 알기 때문에 do-while
                // 한 메세지가 여러 청크로 올수도 있으므로 EndOfMessage까지 모음
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    msgBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
                    msgType = result.MessageType;
                }
                while (!result.EndOfMessage);

                var msg = msgBuffer.ToArray();
                if (msgType == WebSocketMessageType.Binary) 
                {
                    // 청크 수신 : 앞 2바이트 = 헤더길이(big-endian uint16), 그 뒤가 실제 MP3 데이터
                    if (msg.Length >= 2)
                    {
                        // Edge-TTS가 헤더 길이를 2바이트 big-endian으로 보냄
                        // ex) 헤더 길이가 300이면:
                        // msg[0] = 1(0000 0001)
                        // msg[1] = 44(0010 1100)
                        // msg[0] << 8 = 256(0000 0001 0000 0000)   비트시프트  << n = 2의 n승 곱하기랑 같다. >> n = 2의 n승 나누기랑 같다.
                        // | msg[1] 300(0000 0001 0010 1100)        비트OR 둘 중 하나라도 1이면 1. 두 바이트를 합치는 용도) 
                        int headerLen = (msg[0] << 8) | msg[1];
                        int audioStart = 2 + headerLen;
                        if (msg.Length > audioStart)
                            audioBytes.AddRange(new ArraySegment<byte>(msg, audioStart, msg.Length - audioStart));
                                                                    //[0][1][헤더...][오디오데이터...]
                                                                    //↑  ↑   ↑           ↑
                                                                    //2바이트 headerLen  audioStart부터 끝까지
                    }
                    //Binary일 때 finished = true 안 하는 이유
                    //서버가 turn.end 텍스트 메시지로 명시적으로 완료 신호를 보냄.
                }
                else if (msgType == WebSocketMessageType.Text)
                {
                    if (Encoding.UTF8.GetString(msg).Contains("Path:turn.end"))
                        finished = true;
                }
                else if (msgType == WebSocketMessageType.Close)
                {
                    break;
                }
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

            if (audioBytes.Count == 0) 
                return null;

            // 5. MP3 → AudioClip 변환
            // Application.temporaryCachePath : Unity가 제공하는 임시 폴더 경로
            // NPC가 여러 명이면 동시에 말할 수 있으므로 npc마다 고유 파일명 생성
            // 파일명을 고정이므로 매번 같은 자리에 덮어씀. 최대 NPC 수만큼만 파일이 생김.
            string tempPath = Path.Combine(Application.temporaryCachePath, $"npc_tts_{GetInstanceID()}_{Guid.NewGuid():N}.mp3");
            File.WriteAllBytes(tempPath, audioBytes.ToArray());

            using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG); // MP3 (MPEG-1 Audio Layer III)
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EdgeTTSService] 오디오 로드 실패: {req.error}");
                return null;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            File.Delete(tempPath);
            return clip;
        }

        public async UniTask PlayAsync(AudioClip clip, CancellationToken ct = default)
        {
            if (clip == null) return;
            _audioSource.clip = clip;
            _audioSource.Play();
            OnSpeakStarted?.Invoke();

            // 7. 재생 완료 대기
            try
            {
                await UniTask.WaitWhile(() => _audioSource.isPlaying, cancellationToken: ct);
            }
            finally
            {
                // 취소되거나 자연 종료되거나 항상 Finished 발행
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

        private static async UniTask SendTextAsync(ClientWebSocket ws, string message, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
    }
}
