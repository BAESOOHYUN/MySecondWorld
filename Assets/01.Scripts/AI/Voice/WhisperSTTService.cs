using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Android.Gradle.Manifest;
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
            Debug.Log($"[STT] 사용 마이크: {Microphone.devices[0]}");
            foreach (var d in Microphone.devices)
                Debug.Log($"[STT] 마이크 목록: {d}");

            string fullPath = Path.Combine(Application.persistentDataPath, _modelFileName);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[WhisperSTTService] 모델 없음: {fullPath}");
                return;
            }

            _whisper.IsModelPathInStreamingAssets = false;
            _whisper.ModelPath = fullPath;
            await _whisper.InitModel();
            Debug.Log($"[WhisperSTTService] Whisper 준비 완료: {fullPath}");
        }

        public async UniTask<string> ListenAsync(CancellationToken ct = default)
        {
            _isListening = true;
            // null = 기본 마이크, false = 루프 없음, 16000 = Whisper 권장 샘플레이트
            var clip = Microphone.Start(null, false, _maxDurationSec, 16000);

            try
            {
                // StopListening()이 _isListening=false로 바꿀 때까지 여기서 대기
                // 버튼 떼면 → NPCDialogueUI가 StopListening() 호출 → _isListening=false → 통과
                await UniTask.WaitUntil(() => !_isListening, cancellationToken: ct);

                // 실제로 녹음이 몇 샘플까지 찍혔는지(ex 5초/_maxDurationSec) 위치 저장 (여기서 잘라야 무음 없음)
                int endSample = Microphone.GetPosition(null);
                // 마이크 녹음 중지
                Microphone.End(null);
                // 버튼을 너무 짧게 눌러서 아무것도 안 찍힌 경우 방어
                if (endSample == 0)
                    return string.Empty;
                // endSample 위치까지만 잘라낸 새 AudioClip 생성
                // (안 자르면 30초짜리 무음이 Whisper로 들어가서 추론이 느려짐)
                var trimmed = TrimClip(clip, endSample);
                var result = await _whisper.GetTextAsync(trimmed);
                return result?.Result?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
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