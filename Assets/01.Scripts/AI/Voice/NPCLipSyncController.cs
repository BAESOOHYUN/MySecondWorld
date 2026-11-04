using Cysharp.Threading.Tasks;
using MySecondWorld.Customization;
using System.Threading;
using UnityEngine;
using VContainer;

namespace MySecondWorld.AI.Voice
{
    public class NPCLipSyncController : MonoBehaviour
    {
        // --- 인스펙터 ---
        [SerializeField] private MeshRenderer[] _facePlates; // [0]=Female, [1]=Male 순서에 의존
        [SerializeField] private Material _neutral;
        [SerializeField] private Material _mouthSmall;
        [SerializeField] private Material _mouthMid;
        [SerializeField] private Material _mouthOpen;

        // --- 튜닝 상수 ---
        private const int FemaleIdx = 0;
        private const int MaleIdx = 1;
        private const float SMOOTH_SPEED = 5f; // 낮출수록 더 느리게 반응
        private const float HOLD_TIME = 0.05f; // 입 모양 최소 유지 시간 (50ms)

        // --- 주입받은 참조 ---
        private AudioSource[] _sources;
        private NPCCharacterCustomizer _customizer;

        // --- 진폭 샘플링 상태 ---
        //오디오는 초당 44,100개의 샘플(44.1kHz)로 이루어진 연속 스트림
        //지금 막 재생된 256개 묶음 (5.8ms, 0.0058초)   → "지금 얼마나 큰 소리인가"
        private readonly float[] _samples = new float[256];
        private float _smoothedAmplitude;
        private float _holdTimer;

        // --- 립싱크 상태 ---
        private MeshRenderer _activeFacePlate; // 지금 성별에 맞게 켜 둔 판때기
        private CancellationTokenSource _lipSyncCts;

        [Inject]
        private void Construct(NPCVoicePlayer voicePlayer, NPCCharacterCustomizer customizer)
        {
            _customizer = customizer;
            _customizer.AppearanceApplied += ApplyFacePlateByGender;
            _sources = voicePlayer.Sources;
            voicePlayer.OnSpeakStarted += StartLipSync;
            voicePlayer.OnSpeakFinished += StopLipSync;
            ShowFacePlate(-1); // 외형이 적용되기 전에는 얼굴 판때기를 숨긴다 (성별이 정해지면 하나만 켠다)
        }

        private void OnDestroy()
        {
            _customizer.AppearanceApplied -= ApplyFacePlateByGender;
        }

        // active 인덱스의 판때기만 켠다. -1이면 전부 끈다.
        private void ShowFacePlate(int active)
        {
            for (int i = 0; i < _facePlates.Length; i++)
                _facePlates[i].gameObject.SetActive(i == active); // 인덱스가 active인 판때기만 켜고 나머지는 끔

            _activeFacePlate = active >= 0 ? _facePlates[active] : null;  // int로 active 된 플레이트를 대입, -1이면 전부 끔
        }

        private void ApplyFacePlateByGender()
        {
            bool female = _customizer.GetCurrentAppearance().Gender == Gender.Female;
            ShowFacePlate(female ? FemaleIdx : MaleIdx);
            SetMaterial(_neutral);
        }

        private void StartLipSync()
        {
            _lipSyncCts?.Cancel();
            _lipSyncCts?.Dispose();
            _lipSyncCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            LipSyncLoop(_lipSyncCts.Token).Forget();
        }

        private void StopLipSync()
        {
            _lipSyncCts?.Cancel();
            _lipSyncCts?.Dispose();
            _lipSyncCts = null;
            SetMaterial(_neutral);
        }

        // LipSyncLoop 호출마다
        // "지금 이 5.8ms 동안 얼마나 크게 울렸나?" → 평균 진폭 계산 → Material 결정
        private async UniTaskVoid LipSyncLoop(CancellationToken ct)
        {
            if (_facePlates == null || _facePlates.Length == 0) 
                return;

            while (!ct.IsCancellationRequested)
            {
                float raw = IsAnySourcePlaying() ? SampleAmplitude() : 0f;
                _smoothedAmplitude = Mathf.Lerp(_smoothedAmplitude, raw, Time.deltaTime * SMOOTH_SPEED);
                //Debug.Log($"smoothed={_smoothedAmplitude:F4}");

                _holdTimer -= Time.deltaTime;
                if (_holdTimer <= 0)
                {
                    Material next;
                    if (_smoothedAmplitude < 0.02f) next = _neutral;      // _
                    else if (_smoothedAmplitude < 0.05f) next = _mouthSmall;   // AEI
                    else if (_smoothedAmplitude < 0.10f) next = _mouthMid;     // O
                    else next = _mouthOpen;    // A
                    SetMaterial(next);
                    _holdTimer = HOLD_TIME;
                }

                await UniTask.Yield(cancellationToken: ct);
            }
        }

        private bool IsAnySourcePlaying()
        {
            foreach (var source in _sources)
            {
                if (source.isPlaying)
                    return true;
            }
            return false;
        }

        private void SetMaterial(Material mat)
        {
            if (_activeFacePlate != null)
                _activeFacePlate.material = mat;
        }

        private float SampleAmplitude()
        {
            float max = 0f;
            foreach (var source in _sources)
            {
                if (!source.isPlaying)
                    continue;

                // 현재 AudioSource가 출력 중인 오디오 파형을 _samples 배열(256개 float)에 담는다.
                // 각 값은 -1.0 ~ +1.0 범위의 파형 진폭. 두 번째 인자 0은 채널 번호(0 = 왼쪽).
                source.GetOutputData(_samples, 0);
                float sum = 0f;
                for (int i = 0; i < _samples.Length; i++)
                {
                    // 파형 : 공기 압력의 변화량, 진동판 앞으로 밀고(+) 뒤로 당기는(-) 정도
                    // 파형 예시:  0.0, 0.3, 0.7, 0.5, -0.3, -0.8, 0.1 ...
                    // 파형은 양수/음수를 오가므로 그냥 더하면 상쇄된다.
                    // 절댓값을 취해서 "얼마나 흔들렸는가"만 합산해야 한다.
                    // 이게 RMS(Root Mean Square) 대신 쓰는 간이 진폭 계산이다.
                    sum += Mathf.Abs(_samples[i]);
                }

                max = Mathf.Max(max, sum/_samples.Length);
            }
            return max;
        }
    }
}