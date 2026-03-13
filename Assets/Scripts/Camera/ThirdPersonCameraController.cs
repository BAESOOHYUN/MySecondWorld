using Unity.Cinemachine;
using UnityEngine;

public class ThirdPersonCameraController : MonoBehaviour
{
    [SerializeField] private CinemachineOrbitalFollow _orbitFollow;
    [SerializeField] private CinemachineFollowZoom _followZoom;
    [SerializeField] private TouchEventListener _touchListener;

    [Header("Rotation")]
    [SerializeField] private float _pcRotSpeed = 180f;
    [SerializeField] private float _mobileRotSpeed = 180f;
    [SerializeField] private float _rotSmoothTime = 0.05f;

    [Header("Zoom")]
    [SerializeField] private float _pcZoomSpeed = 1.5f;
    [SerializeField] private float _mobileZoomSpeed = 10f;
    [SerializeField] private float _zoomSmooth = 10f;

    private const float _verticalMin = -10f;
    private const float _verticalMax = 45f;
    private const float _fovMin = 0.5f;
    private const float _fovMax = 4.7f;
    private const float _targetAspect = 16f / 9f;

    // 회전 목표값 및 SmoothDamp용 velocity
    private float _targetH;
    private float _targetV;
    private float _hVelocity;
    private float _vVelocity;

    // 줌 목표값
    private float _targetZoom;

    private void Start()
    {
        _touchListener.OnPriDrag += Rotation;
        _touchListener.OnSecDrag += Rotation;
        _touchListener.OnZoom += delta =>
        {
            float speed = _touchListener.deviceType == DeviceType.PC ? _pcZoomSpeed : _mobileZoomSpeed;
            // 모바일 핀치는 픽셀 거리이므로 Screen.height로 정규화, PC 스크롤은 그대로
            float normalized = _touchListener.deviceType == DeviceType.Mobile
                ? delta / Screen.height
                : delta;
            Zoom(normalized * speed * GetAspectCorrection() * Time.deltaTime);
        };
    }

    private void Rotation(float dx, float dy)
    {
        float speed = _touchListener.deviceType == DeviceType.PC ? _pcRotSpeed : _mobileRotSpeed;
        // 해상도에 무관하게 동일한 회전감: 화면 가로/세로 픽셀 수로 정규화
        _orbitFollow.HorizontalAxis.Value += (dx / Screen.width) * speed * Time.deltaTime;
        _orbitFollow.VerticalAxis.Value = Mathf.Clamp(
            _orbitFollow.VerticalAxis.Value - (dy / Screen.height) * speed * Time.deltaTime,
            _verticalMin, _verticalMax);
    }

    // Rotation
    private void SetRotationTarget(float dx, float dy, float speed)
    {
        if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f)
            return;

        _targetH += dx * speed * Time.deltaTime;
        _targetV = Mathf.Clamp(_targetV - dy * speed * Time.deltaTime, _verticalMin, _verticalMax);
        TrackRotationTarget();
    }

    // SmoothDamp로 현재 회전값을 목표값에 부드럽게 근접
    private void TrackRotationTarget()
    {
        _orbitFollow.HorizontalAxis.Value = Mathf.SmoothDamp(
            _orbitFollow.HorizontalAxis.Value, _targetH, ref _hVelocity, _rotSmoothTime);
        _orbitFollow.VerticalAxis.Value = Mathf.SmoothDamp(
            _orbitFollow.VerticalAxis.Value, _targetV, ref _vVelocity, _rotSmoothTime);
    }


    // 기준 화면비(16:9)보다 좁은 화면에서 줌 보정: 좁을수록 같은 동작이 더 크게 느껴지도록
    private float GetAspectCorrection()
    {
        float screenAspect = (float)Screen.width / (float)Screen.height;
        return screenAspect < _targetAspect ? _targetAspect / screenAspect : 1f;
    }

    private void Zoom(float delta)
    {
        _followZoom.Width = Mathf.Clamp(_followZoom.Width + delta, _fovMin, _fovMax);
    }

    // 줌 목표값(_targetZoom)만 갱신. 실제 줌 적용은 TrackZoomTarget에서 처리
    private void SetZoomTarget(float delta)
    {
        Debug.Log($"SetZoomTarget   delta {delta}");
        _targetZoom = Mathf.Clamp(_targetZoom + delta, _fovMin, _fovMax);
        TrackZoomTarget();
    }

    // Lerp 방식: _zoomSmooth가 클수록 목표값에 빠르게 도달
    private void TrackZoomTarget()
    {
        _followZoom.Width = Mathf.Lerp(
            _followZoom.Width, _targetZoom, Time.deltaTime * _zoomSmooth);
    }
}