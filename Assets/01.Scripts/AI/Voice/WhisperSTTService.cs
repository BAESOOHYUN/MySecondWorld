using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using Whisper;
using Application = UnityEngine.Application;

namespace MySecondWorld.AI.Voice
{
    public class WhisperSTTService : MonoBehaviour, ISTTService
    {
        [SerializeField] private WhisperManager _whisper;
        [SerializeField] private string _modelFileName = "ggml-tiny.bin";
        [SerializeField] private int _maxDurationSec = 30; //최대 녹음 시간
        private bool _isListening;
        public bool IsListening => _isListening;

        private async void Start()
        {
            // 마이크 권한 요청 — Android에서 다이얼로그 표시, 허용 후 진행
            await Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.LogError("[STT] 마이크 권한 거부됨 — STT 사용 불가");
                return;
            }

            foreach (var d in Microphone.devices)
                Debug.Log($"[STT] 마이크 목록: {d}");

            string fullPath = Path.Combine(Application.persistentDataPath, _modelFileName);
            bool exists = File.Exists(fullPath);
            long fileSize = exists ? new FileInfo(fullPath).Length : 0;
            Debug.Log($"[STT] 모델 경로: {fullPath}, 존재={exists}, 크기={fileSize / 1024 / 1024}MB ({fileSize}bytes)");
            if (!exists)
            {
                Debug.LogError($"[STT] 모델 없음: {fullPath}");
                return;
            }

            _whisper.IsModelPathInStreamingAssets = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            _whisper.ModelPath = "file://" + fullPath;
#else
            _whisper.ModelPath = fullPath;
#endif
            await _whisper.InitModel();
            Debug.Log($"[STT] Whisper 준비 완료. IsLoaded={_whisper.IsLoaded}");
        }

        public async UniTask<string> ListenAsync(CancellationToken ct = default)
        {
            Debug.Log("[STT] ListenAsync 진입");
            _isListening = true;
            // null = 기본 마이크, false = 루프 없음, 16000 = Whisper 권장 샘플레이트
            var clip = Microphone.Start(null, false, _maxDurationSec, 16000);
            Debug.Log($"[STT] 녹음 시작. IsRecording={Microphone.IsRecording(null)}, clip={clip}");

            try
            {
                // StopListening()이 _isListening=false로 바꿀 때까지 여기서 대기
                // 버튼 떼면 → NPCDialogueUI가 StopListening() 호출 → _isListening=false → 통과
                await UniTask.WaitUntil(() => !_isListening, cancellationToken: ct);
                Debug.Log("[STT] WaitUntil 통과 — 버튼 뗌 감지");

                // 실제로 녹음이 몇 샘플까지 찍혔는지(ex 5초/_maxDurationSec) 위치 저장 (여기서 잘라야 무음 없음)
                int endSample = Microphone.GetPosition(null);
                Debug.Log($"[STT] endSample={endSample}");
                // 마이크 녹음 중지
                Microphone.End(null);
                // 버튼을 너무 짧게 눌러서 아무것도 안 찍힌 경우 방어
                if (endSample == 0)
                {
                    Debug.LogWarning("[STT] endSample=0, 빈 결과 반환");
                    return string.Empty;
                }
                // endSample 위치까지만 잘라낸 새 AudioClip 생성
                // (안 자르면 30초짜리 무음이 Whisper로 들어가서 추론이 느려짐)
                var trimmed = TrimClip(clip, endSample);
                Debug.Log($"[STT] TrimClip 완료 — samples={trimmed.samples}, freq={trimmed.frequency}, ch={trimmed.channels}");

                // float[] 앞부분 진폭 샘플링 (무음 여부 확인용)
                var probe = new float[Mathf.Min(1024, trimmed.samples * trimmed.channels)];
                trimmed.GetData(probe, 0);
                float maxAmp = 0f;
                foreach (var s in probe) maxAmp = Mathf.Max(maxAmp, Mathf.Abs(s));
                Debug.Log($"[STT] 오디오 최대 진폭(앞 1024샘플)={maxAmp:F5} — 0이면 무음");

                Debug.Log($"[STT] GetTextAsync 호출 — lang='{_whisper.language}', IsLoaded={_whisper.IsLoaded}");
                var result = await _whisper.GetTextAsync(trimmed);
                if (result == null)
                    Debug.LogWarning("[STT] result == null (Whisper 추론 실패 또는 모델 미로드)");
                else
                    Debug.Log($"[STT] result.Result='{result.Result}', segments={result.Segments?.Count}");
                return result?.Result?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[STT] 취소됨 (OperationCanceledException)");
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

        private AudioClip TrimClip(AudioClip original, int endSample)
        {
            // channels 오디오의 좌/우 채널 수
            // ex) 5초치 샘플이 1000개라면?
            // channels=1 (모노, 소리 하나): float 배열 크기 = 1000
            // channels=2 (스테레오, 왼쪽(L) + 오른쪽(R): float 배열 크기 = 2000 (L+R이 한 쌍)
            var data = new float[endSample * original.channels];
            // 0번 샘플부터 data 배열 크기만큼 읽어옴
            // → 실제 음성 구간(5초)의 float 값들만 추출됨
            original.GetData(data, 0);
            // 새 AudioClip 생성
            // endSample    = 샘플 수 (5초치)
            // channels     = 원본과 동일
            // frequency    = 원본과 동일 (16000Hz)
            // false        = stream 아님 (메모리에 통째로 올림)
            var trimmed = AudioClip.Create("stt_clip", endSample, original.channels, original.frequency, false);
            // 추출한 float 배열을 새 clip에 0번 위치부터 씀
            trimmed.SetData(data, 0);
            return trimmed;
        }
    }
}