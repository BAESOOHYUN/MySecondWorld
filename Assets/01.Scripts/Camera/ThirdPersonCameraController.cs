using UnityEngine;
using Unity.Cinemachine;
using VContainer;

namespace MySecondWorld.CameraSystem
{
    public class ThirdPersonCameraController : MonoBehaviour
    {
        // Brain 설정은 Update Method, Blend Update Method => LateUpdate로 바꿔주기 (이동시 화면이 덜덜 떨리는 현상 방지)

        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private CinemachineOrbitalFollow _orbitFollow; //World Space. Sphere, 10, Tracking Target
        [SerializeField] private CinemachineFollowZoom _followZoom; // With 4.74, Damping 1

        private TouchEventListener _touchEventListener;

        [Header("Rotation")]
        [SerializeField] private float _pcRotSpeed = 30f;
        [SerializeField] private float _mobileRotSpeed = 3f;

        [Header("Zoom")]
        [SerializeField] private float _pcZoomSpeed = 0.4f;
        [SerializeField] private float _mobileZoomSpeed = 4f;

        private const float _verticalMin = -10f;
        private const float _verticalMax = 45f;
        private const float _fovMin = 0.5f;
        private const float _fovMax = 4.7f;

        private const float _targetAspect = 16f / 9f;
        private const float _deltaCorrectionValue = 100f;

        private float _horizontalValue;
        private float _verticalValue;

        public float CameraYAngle => _cinemachineCamera.transform.eulerAngles.y;

        [Inject]
        private void Construct(IObjectResolver resolver)
        {
            // 헤드리스 서버엔 TouchEventListener가 등록되지 않는다 (ProjectLifetimeScope의 #if 가드).
            // VContainer는 기본값(= null)을 지원하지 않으므로, 있을 때만 TryResolve로 받는다.
            resolver.TryResolve(out _touchEventListener);
        }

        private void Start()
        {
            _horizontalValue = _orbitFollow.HorizontalAxis.Value;
            _verticalValue = _orbitFollow.VerticalAxis.Value;

            if (_touchEventListener == null) return;

            _touchEventListener.OnPriDrag += Rotation;
            _touchEventListener.OnSecDrag += Rotation;
            _touchEventListener.OnZoom += Zoom;
        }

        private void OnDisable()
        {
            if (_touchEventListener == null) return;

            _touchEventListener.OnPriDrag -= Rotation;
            _touchEventListener.OnSecDrag -= Rotation;
            _touchEventListener.OnZoom -= Zoom;
        }

        private void LateUpdate()
        {
            _orbitFollow.HorizontalAxis.Value = _horizontalValue;
            _orbitFollow.VerticalAxis.Value = _verticalValue;
        }

        public void ActiveCinemachine(bool isActitve)
        {
            _cinemachineCamera.enabled = isActitve;
            if (isActitve)
            {
                SnapToTarget(); // 켜지는 순간 블렌딩 끊고 즉시 스냅
            }
        }

        /// <summary>
        /// 씬 진입, 스폰, 순간이동 시 카메라 블렌딩(2초)과 감쇠(Damping)를 끊고
        /// 플레이어 등 뒤 원래 시점으로 즉시 스냅(Cut)시킵니다.
        /// </summary>
        public void SnapToTarget()
        {
            if (_cinemachineCamera != null)
            {
                // CinemachineVirtualCameraBase.cs 에 있는 ForceCameraPosition 함수 내부를 보면
                // PreviousStateIsValid를 true로 세팅해주고 있음
                // ForceCameraPosition로 다음 방으로 위치 옮기고
                // 바로 다음 줄에 PreviousStateIsValid = false 해주면 됨
                // false로 두면 이전 프레임 위치값이 유효하지 않은걸로 처리해서 카메라 위치를 옮겨도 blend 하지 않음
                _cinemachineCamera.PreviousStateIsValid = false;
                _cinemachineCamera.CancelDamping();
            }
            // CinemachineBrain이 2초 동안 날아오고 있다면 즉시 완료(Cut)
            // 2초 : 컴포넌트의 인스펙터(씬 데이터)에서 설정됨
            var brain = UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null && brain.IsBlending)
            {
                brain.ActiveBlend = null;
            }
        }

        // Rotation
        private void Rotation(float dx, float dy)
        {
            float speed = _touchEventListener.deviceType == DeviceType.PC ? _pcRotSpeed : _mobileRotSpeed;
            // 해상도에 무관하게 동일한 회전감: 화면 가로/세로 픽셀 수로 정규화
            _horizontalValue += (dx / Screen.width) * speed * _deltaCorrectionValue;
            _verticalValue = Mathf.Clamp(
                _verticalValue - (dy / Screen.height) * speed * _deltaCorrectionValue,
                _verticalMin, _verticalMax);
        }

        // Zoom
        private void Zoom(float delta)
        {
            float speed = _touchEventListener.deviceType == DeviceType.PC ? _pcZoomSpeed : _mobileZoomSpeed;
            // 모바일 핀치는 픽셀 거리이므로 Screen.height로 정규화, PC 스크롤은 그대로
            float normalized = _touchEventListener.deviceType == DeviceType.Mobile
                ? delta / Screen.height
                : delta;
            delta = normalized * speed * GetAspectCorrection();

            _followZoom.Width = Mathf.Clamp(_followZoom.Width + delta, _fovMin, _fovMax);
        }

        // 기준 화면비(16:9)보다 좁은 화면에서 줌 보정: 좁을수록 같은 동작이 더 크게 느껴지도록
        private float GetAspectCorrection()
        {
            float screenAspect = (float)Screen.width / (float)Screen.height;
            return screenAspect < _targetAspect ? _targetAspect / screenAspect : 1f;
        }
    }
}