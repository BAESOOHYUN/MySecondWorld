using Unity.Cinemachine;
using UnityEngine;

public class ThirdPersonCameraController : MonoBehaviour
{
    // Brain 설정은 Update Method, Blend Update Method => LateUpdate로 바꿔주기 (이동시 화면이 덜덜 떨리는 현상 방지)

    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private CinemachineOrbitalFollow _orbitFollow; //World Space. Sphere, 10, Tracking Target
    [SerializeField] private CinemachineFollowZoom _followZoom; // With 4.74, Damping 1

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

    public float CameraYAngle => _cinemachineCamera.transform.eulerAngles.y;

    private void OnEnable()
    {
        TouchEventListener.Instance.OnPriDrag += Rotation;
        TouchEventListener.Instance.OnSecDrag += Rotation;
        TouchEventListener.Instance.OnZoom += Zoom;
    }

    private void OnDisable()
    {
        TouchEventListener.Instance.OnPriDrag -= Rotation;
        TouchEventListener.Instance.OnSecDrag -= Rotation;
        TouchEventListener.Instance.OnZoom -= Zoom;
    }


    public void ActiveCinemachine(bool isActitve)
    {
        _cinemachineCamera.enabled = isActitve;
    }

    // Rotation
    private void Rotation(float dx, float dy)
    {
        float speed = TouchEventListener.Instance.deviceType == DeviceType.PC ? _pcRotSpeed : _mobileRotSpeed;
        // 해상도에 무관하게 동일한 회전감: 화면 가로/세로 픽셀 수로 정규화
        _orbitFollow.HorizontalAxis.Value += (dx / Screen.width) * speed * _deltaCorrectionValue;
        _orbitFollow.VerticalAxis.Value = Mathf.Clamp(
            _orbitFollow.VerticalAxis.Value - (dy / Screen.height) * speed * _deltaCorrectionValue,
            _verticalMin, _verticalMax);
    }

    // Zoom
    private void Zoom(float delta)
    {
        float speed = TouchEventListener.Instance.deviceType == DeviceType.PC ? _pcZoomSpeed : _mobileZoomSpeed;
        // 모바일 핀치는 픽셀 거리이므로 Screen.height로 정규화, PC 스크롤은 그대로
        float normalized = TouchEventListener.Instance.deviceType == DeviceType.Mobile
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