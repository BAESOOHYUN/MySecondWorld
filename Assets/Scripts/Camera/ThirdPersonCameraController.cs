using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
public class ThirdPersonCameraController : MonoBehaviour, IInputAxisOwner
{
    // TODO : TouchEventListener, MovementController를 DI에 담아놓고 쓰기
    [SerializeField] private CinemachineOrbitalFollow _orbitFollow; // 3인칭 일 때
    [SerializeField] private CinemachineFollowZoom _followZoom;
    [SerializeField] private TouchEventListener _touchListener;
    [SerializeField] private MovementController _movementController;
    [SerializeField] private InputAxis Zoom = InputAxis.DefaultMomentary;
    private float _postHorizontalAxis;
    private float _postVerticalAxis;
    private const float _minFOV = 0.5f;
    private const float _maxFOV = 4.7f;
    private bool _isInitialClickOnUI = false;
    private bool _isMouseDown = false;


    private void Update()
    {
        switch (_touchListener.deviceType)
        {
            case DeviceType.PC:
                {
                    RotateCamera_PC();
                    ScrollZoom(Zoom.Value);
                }
                break;
            case DeviceType.Mobile:
                {
                    RotateCamera_Mobile();
                    PinchZoom();
                }
                break;
        }
    }

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref Zoom, Name = nameof(Zoom), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    // 첫 입력이 UI를 클릭하는 게 아닐 때에만 회전
    private void RotateCamera_PC()
    {
        if (_touchListener.SecContact.Value > 0)
        {
            if (!_isMouseDown)
            {
                _isInitialClickOnUI = _touchListener.isPriPositionOnUI;
                _isMouseDown = true;
            }

            if (!_isInitialClickOnUI)
            {
                _orbitFollow.HorizontalAxis.Value = _touchListener.SecDeltaX.Value + _postHorizontalAxis;
                _orbitFollow.VerticalAxis.Value = Mathf.Clamp(-_touchListener.SecDeltaY.Value + _postVerticalAxis, -10, 45);
            }
        }
        else
        {
            _isMouseDown = false;
            _postHorizontalAxis = _orbitFollow.HorizontalAxis.Value;
            _postVerticalAxis = _orbitFollow.VerticalAxis.Value;
            _touchListener.SecDeltaX.Value = 0;
            _touchListener.SecDeltaY.Value = 0;
        }
    }


    // TODO : 조이스틱 이동 중, 두번째 터치로 회전하지 못하는 문제
    private void RotateCamera_Mobile()
    {
        if (_touchListener.PriContact.Value > 0)
        {
            _orbitFollow.HorizontalAxis.Value = _touchListener.PriDeltaX.Value + _postHorizontalAxis;
            _orbitFollow.VerticalAxis.Value = Mathf.Clamp(-_touchListener.PriDeltaY.Value + _postVerticalAxis, -10, 45);
        }
        else
        {
            _postHorizontalAxis = _orbitFollow.HorizontalAxis.Value;
            _postVerticalAxis = _orbitFollow.VerticalAxis.Value;
            _touchListener.PriDeltaX.Value = 0;
            _touchListener.PriDeltaY.Value = 0;
        }
    }

    private void ScrollZoom(float zoomValue)
    {
        if (!_touchListener.isPriPositionOnUI)
        {
            var preFOV = _followZoom.Width - zoomValue * 0.5f;
            _followZoom.Width = Mathf.Clamp(preFOV, _minFOV, _maxFOV);
        }
    }

    private void PinchZoom()
    {
        // 멀티 터치
        if (_touchListener.PriContact.Value > 0 && _touchListener.SecContact.Value > 0)
        {
            var priPosition = new Vector2(_touchListener.PriPositionX.Value, _touchListener.PriPositionY.Value);
            var secPosition = new Vector2(_touchListener.SecPositionX.Value, _touchListener.SecPositionY.Value);
            var priDelta = new Vector2(_touchListener.PriDeltaX.Value, _touchListener.PriDeltaY.Value);
            var secDelta = new Vector2(_touchListener.SecDeltaX.Value, _touchListener.SecDeltaY.Value);

            //직전 프레임의 터치 위치를 구하기 위해 "현재 위치 - 위치 변화량"을 계산
            Vector2 priPreviousPosition = priPosition - priDelta;
            Vector2 secPreviousPosition = secPosition - secDelta;

            //직전 프레임에서의 터치 위치 거리 값
            float previousPositionDistance = (priPreviousPosition - secPreviousPosition).magnitude;

            //현재 프레임에서의 터치 위치 거리 값
            float currentPositionDistance = (priPosition - secPosition).magnitude;

            if (_touchListener.isPriPositionOnUI || _touchListener.isSecPositionOnUI)
                return;

            var multiTouchDelta = previousPositionDistance - currentPositionDistance;
            ScrollZoom(multiTouchDelta);
        }
    }
}
