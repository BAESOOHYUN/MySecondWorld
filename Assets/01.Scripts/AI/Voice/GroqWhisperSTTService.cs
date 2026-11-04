using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using MySecondWorld.AI;

namespace MySecondWorld.AI.Voice
{
    /// <summary>
    /// Groq Whisper large-v3를 사용하는 서버 모드 STT 서비스.
    /// 로컬 WhisperSTTService와 동일한 ISTTService 인터페이스를 구현.
    /// 마이크 녹음 → WAV 인코딩 → Groq HTTP POST → 텍스트 반환.
    public class GroqWhisperSTTService : MonoBehaviour, ISTTService
    {
        private static string API_KEY { get; set; }
        private const string MODEL = "whisper-large-v3-turbo";
        [SerializeField] private int _sampleRate = 16000;
        [SerializeField] private int _maxDurationSec = 30;
        private bool _isListening;
        public bool IsListening => _isListening;

        private async void Start()
        {
            if (API_KEY == null)
                API_KEY = SecretKeyLoader.Load("groq_api_key");
            // 마이크 권한 요청 — WhisperSTTService와 동일 패턴
            await Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.LogError("[GroqSTT] 마이크 권한 거부됨 — STT 사용 불가");
                return;
            }

            foreach(var d in Microphone.devices)
                Debug.Log($"[GroqSTT] 마이크 목록: {d}");
        }

        public async UniTask<string> ListenAsync(CancellationToken ct = default)
        {
            Debug.Log("[GroqSTT] ListenAsync 진입");
            _isListening = true;
            // null = 기본 마이크, false = 루프 없음
            var clip = Microphone.Start(null, false, _maxDurationSec, _sampleRate);
            Debug.Log($"[GroqSTT] 녹음 시작. IsRecording={Microphone.IsRecording(null)}");

            try
            {
                await UniTask.WaitUntil(() => !_isListening, cancellationToken: ct);
                Debug.Log("[GroqSTT] 버튼 뗌 감지");
                int endSample = Microphone.GetPosition(null);
                Microphone.End(null);

                if (endSample == 0)
                {
                    Debug.LogWarning("[GroqSTT] endSample=0, 빈 결과 반환");
                    return string.Empty;
                }
                var trimmed = TrimClip(clip, endSample);
                Debug.Log($"[GroqSTT] TrimClip 완료 — samples={trimmed.samples}, freq={trimmed.frequency}");

                byte[] wavBytes = EncodeToWav(trimmed);
                Debug.Log($"[GroqSTT] WAV 인코딩 완료 — {wavBytes.Length} bytes");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await TranscribeAsync(wavBytes, ct);
                sw.Stop();
                Debug.Log($"[GroqSTT] 인식 결과: '{result}' ({sw.ElapsedMilliseconds}ms)");

                return result;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GroqSTT] 취소됨");
                return string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GroqSTT] 오류: {e}");  // e.Message → e (전체 스택 트레이스)
                return string.Empty;
            }
            finally
            {
                _isListening = false;
                if (Microphone.IsRecording(null))
                    Microphone.End(null);
            }
        }

        public void StopListening()
        {
            _isListening = false;
        }

        /// <summary>
        /// WAV bytes를 Groq에 전송하고 텍스트를 반환한다.
        /// UnityWebRequest(비동기)로 전송하므로 메인스레드를 막지 않는다.
        /// </summary>
        private async UniTask<string> TranscribeAsync(byte[] wavBytes, CancellationToken ct)
        {
            // OpenAI SDK의 MultipartFormDataContent가 Unity에서 boundary 오류 발생
            // → UnityWebRequest 직접 구현으로 폴백
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", wavBytes, "audio.wav", "audio/wav"),
                new MultipartFormDataSection("model", MODEL),
                new MultipartFormDataSection("language", "ko"),
                new MultipartFormDataSection("response_format", "text"),
            };

            using var request = UnityWebRequest.Post(
                "https://api.groq.com/openai/v1/audio/transcriptions", form);
            request.SetRequestHeader("Authorization", $"Bearer {API_KEY}");

            await request.SendWebRequest().WithCancellation(ct);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GroqSTT] HTTP 오류: {request.error} / {request.downloadHandler.text}");
                return string.Empty;
            }

            // response_format=text → 응답이 JSON 아닌 순수 텍스트
            return request.downloadHandler.text.Trim();
        }

        /// <summary>
        /// 녹음된 AudioClip에서 실제 음성 구간(endSample)만 잘라낸다.
        /// 잘라내지 않으면 30초 무음이 Groq로 전송되어 처리 시간 낭비.
        /// </summary>
        private AudioClip TrimClip(AudioClip original, int endSample)
        {
            var data = new float[endSample * original.channels];
            original.GetData(data, 0);
            var trimmed = AudioClip.Create("groq_stt_clip", endSample, original.channels, original.frequency, false);
            trimmed.SetData(data, 0);
            return trimmed;
        }

        /// <summary>
        /// AudioClip → WAV byte[] 변환.
        /// PCM float[-1,1] → Int16[-32768,32767] → 44바이트 RIFF 헤더 조립.
        /// </summary>
        private byte[] EncodeToWav(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // float → short 변환
            var pcm16 = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                // short.MaxValue = 32767
                // 비율이 그대로 유지되면서 float → short 범위로 압축
                // Mathf.Clamp(-1f, 1f) 간혹 오디오 데이터가 1.0을 살짝 넘는 경우가 있어서
                // 32767을 초과한 값이 short로 캐스팅되면 오버플로우 로 부호가 뒤집히는 걸 막기 위해서
                pcm16[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            }

            int dataSize = pcm16.Length * 2; // short = 2 bytes
            using var ms = new MemoryStream(44 + dataSize);
            using var writer = new BinaryWriter(ms);

            #region MemoryStream(44) 왜 44? 청크 설명
            // RIFF 청크(12 bytes)
            // ├─ "RIFF"    4 bytes  ← 식별자 (RIFF 포맷 파일)
            // ├─ 파일크기  4 bytes
            // └─ "WAVE"    4 bytes  ← 식별자 (오디오 파일)
            // fmt 청크(24 bytes)
            // ├─ "fmt "    4 bytes  ← 식별자
            // ├─ 청크크기  4 bytes(항상 16)
            // ├─ 포맷      2 bytes(PCM = 1) (AudioFormat: PCM)
            // ├─ 채널수    2 bytes(mono, stereo)
            // ├─ 샘플레이트 4 bytes //sampleRate(sampling frequency) 16000 "1초에 파형(sample)을 몇 번 측정하는가" → 오디오 품질 (Hz)
            // ├─ ByteRate  4 bytes //1초치 데이터가 몇 바이트인가? SampleRate × BlockAlign = 16000 × 2 = 32000 bytes/초  (모노 기준) 
            // ├─ BlockAlign 2 bytes //샘플 하나를 저장하는 데 필요한 바이트 수. channels*(BitsPerSample/8). 모노(1채널):1*2 = 2 bytes, 스테레오(2채널):2*2 = 4 bytes
            // └─ BitsPerSample  2 bytes //샘플 하나를 몇 비트로 저장하는가? short = 16비트
            // data 청크(8 bytes)
            // ├─ "data"    4 bytes  ← 식별자
            // └─ 데이터크기 4 bytes
            // 합계 = 12 + 24 + 8 = 44 bytes
            #endregion

            // RIFF 헤더 청크
            writer.Write(new char[] { 'R', 'I', 'F', 'F' }); // WAV 파서가 기대하는 그대로 딱 4바이트만
            writer.Write(36 + dataSize);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            // fmt 청크
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels); 
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);

            // data 청크
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
            foreach (var sample in pcm16)
                writer.Write(sample);

            return ms.ToArray();
        }
    }
}

//핵심 흐름 요약
//ListenAsync()
//  Microphone.Start() → 녹음
//  StopListening() 호출 대기(WaitUntil)
//  TrimClip() → 무음 30초 방지
//  EncodeToWav() → float → short → RIFF 헤더
//  TranscribeAsync()
//    UnityWebRequest.SendWebRequest() → HTTP POST to Groq (await, 스레드 전환 없음)
//결과 텍스트 반환