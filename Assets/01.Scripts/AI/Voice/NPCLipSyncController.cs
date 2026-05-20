using UnityEngine;
using VContainer;

namespace MySecondWorld.AI.Voice
{
    public class NPCLipSyncController : MonoBehaviour
    {
        [SerializeField] private MeshRenderer[] _facePlates;
        [SerializeField] private Material _neutral;
        [SerializeField] private Material _mouthSmall;
        [SerializeField] private Material _mouthMid;
        [SerializeField] private Material _mouthOpen;

        private AudioSource[] _sources;
        //오디오는 초당 44,100개의 샘플(44.1kHz)로 이루어진 연속 스트림
        //지금 막 재생된 256개 묶음 (5.8ms, 0.0058초)   → "지금 얼마나 큰 소리인가"
        private readonly float[] _samples = new float[256];
        private float _smoothedAmplitude;
        private const float SMOOTH_SPEED = 5f;  // 낮출수록 더 느리게 반응
        private float _holdTimer;
        private const float HOLD_TIME = 0.05f; // 80ms 유지

        [Inject]
        private void Construct(NPCVoicePlayer voicePlayer)
        {
            _sources = voicePlayer.Sources;
        }

        // Update() 호출마다
        // "지금 이 5.8ms 동안 얼마나 크게 울렸나?" → 평균 진폭 계산 → Material 결정
        private void Update()
        {
            if (_facePlates == null || _facePlates.Length == 0) 
                return;

            float raw = IsAnySourcePlaying() ? SampleAmplitude() : 0f;
            _smoothedAmplitude = Mathf.Lerp(_smoothedAmplitude, raw, Time.deltaTime * SMOOTH_SPEED);
            //Debug.Log($"smoothed={_smoothedAmplitude:F4}");

            _holdTimer -= Time.deltaTime;
            if (_holdTimer > 0) return; // 아직 유지 중이면 스킵

            Material next;
            if (_smoothedAmplitude < 0.02f) next = _neutral;      // _
            else if (_smoothedAmplitude < 0.05f) next = _mouthSmall;   // AEI
            else if (_smoothedAmplitude < 0.10f) next = _mouthMid;     // O
            else next = _mouthOpen;    // A

            SetMaterial(next);
            _holdTimer = HOLD_TIME;
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
            foreach (var facePlate in _facePlates)
            {
                if (facePlate != null)
                {
                    facePlate.material = mat;
                }
            }
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