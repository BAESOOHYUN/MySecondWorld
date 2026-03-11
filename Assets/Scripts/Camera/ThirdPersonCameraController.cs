using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class SmoothCameraController : MonoBehaviour
{
    [Header("Cinemachine Components")]
    [SerializeField] private CinemachineOrbitalFollow _orbitFollow;
    [SerializeField] private CinemachineFollowZoom _followZoom;
    [SerializeField] private TouchEventListener _touchListener; // TouchEventListener 할당

    [Header("Rotation Settings")]
    [SerializeField] private float _rotSpeed = 0.2f;
    [SerializeField] private float _verticalMin = -10f, _verticalMax = 45f;

    [Header("Zoom Settings")] 
    [SerializeField] private InputAxis Zoom = InputAxis.DefaultMomentary;
    [SerializeField] private float _zoomSpeed = 1.5f;
    [SerializeField] private float _zoomSensitivity = 0.1f;
    [SerializeField] private float _zoomSmooth = 10f;
    [SerializeField] private float _fovMin = 0.5f, _fovMax = 4.7f;

    private float _targetZoom;
    private int _lastTouchCount;

    private void Start()
    {
        _targetZoom = _followZoom.Width;
    }

    private void LateUpdate()
    {
        if (_touchListener.deviceType == DeviceType.PC) 
            HandlePC();
        else 
            HandleMobile();

        // [공통] 부드러운 줌 보간 (CinemachineFollowZoom 직접 제어)
        _followZoom.Width = Mathf.Lerp(_followZoom.Width, _targetZoom, Time.deltaTime * _zoomSmooth);
    }

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref Zoom, Name = nameof(Zoom), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    #region PC Logic
    private void HandlePC()
    {
        // 회전 (우클릭 유지 시)
        if (_touchListener.SecContact.Value > 0 && !_touchListener.isPriPositionOnUI) // PC에서 보통 Pri는 마우스 포인터 위치
        {
            ApplyRotation(_touchListener.SecDeltaX, _touchListener.SecDeltaY);
        }

        // 줌 (InputAxis Zoom 사용)
        if (Mathf.Abs(Zoom.Value) > 0.001f && !_touchListener.isPriPositionOnUI)
        {
            // Zoom.Value가 양수면 줌인, 음수면 줌아웃 (인스펙터 설정에 따라 다를 수 있음)
            // 휠 입력 속도에 맞춰 적절히 가중치(예: 10f)를 곱해 타겟을 갱신.
            UpdateTargetZoom(Zoom.Value * 10f);
        }
    }
    #endregion


    #region Mobile Logic (요청하신 표 조건 100% 적용)
    private void HandleMobile()
    {
        int touchCount = (_touchListener.PriContact.Value > 0 ? 1 : 0) + (_touchListener.SecContact.Value > 0 ? 1 : 0);

        // [방어] 손가락 개수 변할 때 튀는 델타값 무시
        if (touchCount != _lastTouchCount)
        {
            _lastTouchCount = touchCount;
            return;
        }

        if (touchCount == 1)
        {
            // Pri가 UI 밖에서 시작했으면 회전
            if (_touchListener.PriContact.Value > 0 && !_touchListener.isPriStartPositionOnUI)
                ApplyRotation(_touchListener.PriDeltaX, _touchListener.PriDeltaY);
            // 혹은 Sec만 남았는데 UI 밖에서 시작했으면 회전
            else if (_touchListener.SecContact.Value > 0 && !_touchListener.isSecStartPositionOnUI)
                ApplyRotation(_touchListener.SecDeltaX, _touchListener.SecDeltaY);
        }
        else if (touchCount == 2)
        {
            bool priField = !_touchListener.isPriStartPositionOnUI;
            bool secField = !_touchListener.isSecStartPositionOnUI;

            if (priField && secField) // 둘 다 UI 밖 -> 줌
                ProcessPinchZoom();
            else if (priField && !secField) // Pri만 UI 밖 -> Pri로만 회전 (Sec 무시)
                ApplyRotation(_touchListener.PriDeltaX, _touchListener.PriDeltaY);
            else if (!priField && secField) // Sec만 UI 밖 -> Sec으로만 회전 (Pri 무시)
                ApplyRotation(_touchListener.SecDeltaX, _touchListener.SecDeltaY);
            // 둘 다 UI 안이면 아무것도 안 함
        }
    }
    #endregion

    #region core Methods
    private void ApplyRotation(InputAxis deltaXAxis, InputAxis deltaYAxis)
    {

        float dx = deltaXAxis.Value;
        float dy = deltaYAxis.Value;

        if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f) 
            return;

        _orbitFollow.HorizontalAxis.Value += dx * _rotSpeed;

        float nextV = _orbitFollow.VerticalAxis.Value - dy * _rotSpeed;
        _orbitFollow.VerticalAxis.Value = Mathf.Clamp(nextV, _verticalMin, _verticalMax);

        // 이번 프레임의 델타를 사용했으므로 0으로 초기화 (회전 폭주 방지)
        // 인자로 받은 Axis의 Value를 직접 0으로 밀어버림.
        deltaXAxis.Value = 0;
        deltaYAxis.Value = 0;
    }

    private void ProcessPinchZoom()
    {
        var t = _touchListener;

        // 현재 프레임의 거리 계산 (Current Distance)
        float currDX = t.PriPositionX.Value - t.SecPositionX.Value;
        float currDY = t.PriPositionY.Value - t.SecPositionY.Value;
        // x*x + y*y 방식 (Mathf.Pow보다 빠름)
        float currDist = Mathf.Sqrt(currDX * currDX + currDY * currDY);

        // 이전 프레임의 위치 역산 및 거리 계산 (Previous Distance)
        float prevPriX = t.PriPositionX.Value - t.PriDeltaX.Value;
        float prevPriY = t.PriPositionY.Value - t.PriDeltaY.Value;
        float prevSecX = t.SecPositionX.Value - t.SecDeltaX.Value;
        float prevSecY = t.SecPositionY.Value - t.SecDeltaY.Value;

        float prevDX = prevPriX - prevSecX;
        float prevDY = prevPriY - prevSecY;
        float prevDist = Mathf.Sqrt(prevDX * prevDX + prevDY * prevDY);

        // 거리 차이 계산 및 타겟 줌 업데이트
        float delta = prevDist - currDist;

        UpdateTargetZoom(delta);
    }


    private void UpdateTargetZoom(float delta)
    {
        float zoomAmount = delta * _zoomSensitivity * _zoomSpeed;
        _targetZoom = Mathf.Clamp(_targetZoom + zoomAmount, _fovMin, _fovMax);
    }
    #endregion
}