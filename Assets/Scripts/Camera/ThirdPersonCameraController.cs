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
    public bool _isInitialClickOnUI = false;
    public bool _isSingleTouchDown = false;


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
        var t = _touchListener;
        var o = _orbitFollow;

        if (t.SecContact.Value > 0) //우클릭을 의미
        {
            if (!_isSingleTouchDown)
            {
                _isInitialClickOnUI = t.isPriPositionOnUI;
                _isSingleTouchDown = true;
            }

            if (!_isInitialClickOnUI)
            {
                o.HorizontalAxis.Value = t.SecDeltaX.Value + _postHorizontalAxis;
                o.VerticalAxis.Value = Mathf.Clamp(-t.SecDeltaY.Value + _postVerticalAxis, -10, 45);
            }
        }
        else
        {
            _isSingleTouchDown = false;
            _postHorizontalAxis = o.HorizontalAxis.Value;
            _postVerticalAxis = o.VerticalAxis.Value;
            t.SecDeltaX.Value = 0;
            t.SecDeltaY.Value = 0;
        }
    }


    // TODO : 조이스틱 이동 중, 두번째 터치로 회전하지 못하는 문제
    private void RotateCamera_Mobile()
    {
        var t = _touchListener;
        var o = _orbitFollow;

        if ((t.PriContact.Value > 0 && !t.isPriPositionOnUI && (t.SecContact.Value == 0 || t.isSecPositionOnUI)) ||
            (t.SecContact.Value > 0 && t.isSecPositionOnUI && t.PriContact.Value > 0 && !t.isPriPositionOnUI))
        {
            Debug.Log($"[0]번째 손가락으로 회전");

            if (!_isSingleTouchDown)
            {
                _isInitialClickOnUI = t.isPriPositionOnUI;
                _isSingleTouchDown = true;
            }

            if (!_isInitialClickOnUI)
            {
                o.HorizontalAxis.Value = t.PriDeltaX.Value + _postHorizontalAxis;
                o.VerticalAxis.Value = Mathf.Clamp(-t.PriDeltaY.Value + _postVerticalAxis, -10, 45);
            }

        }
        else if (t.PriContact.Value == 0)
        {
            Debug.Log($"[1]번째 손가락으로 회전 중지");
            _isSingleTouchDown = false;
            _postHorizontalAxis = o.HorizontalAxis.Value;
            _postVerticalAxis = o.VerticalAxis.Value;
            t.PriDeltaX.Value = 0;
            t.PriDeltaY.Value = 0;
        }
        else if ((t.SecContact.Value > 0 && !t.isSecPositionOnUI && (t.PriContact.Value == 0 || t.isPriPositionOnUI)) ||
                (t.PriContact.Value > 0 && t.isPriPositionOnUI && t.SecContact.Value > 0 && !t.isSecPositionOnUI))
        {
            Debug.Log($"[1]번째 손가락으로 회전");

            if (!_isSingleTouchDown)
            {
                _isInitialClickOnUI = t.isSecPositionOnUI;
                _isSingleTouchDown = true;
            }

            if (!_isInitialClickOnUI)
            {
                o.HorizontalAxis.Value = t.SecDeltaX.Value + _postHorizontalAxis;
                o.VerticalAxis.Value = Mathf.Clamp(-t.SecDeltaY.Value + _postVerticalAxis, -10, 45);
            }
        }
        else if (t.SecContact.Value == 0)
        {
            Debug.Log($"[1]번째 손가락으로 회전 중지");
            _isSingleTouchDown = false;
            _postHorizontalAxis = o.HorizontalAxis.Value;
            _postVerticalAxis = o.VerticalAxis.Value;
            t.SecDeltaX.Value = 0;
            t.SecDeltaY.Value = 0;
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
        var t = _touchListener;

        // 멀티 터치 && UI을 건드리지 않고 있을 때만 줌~
        if (t.PriContact.Value > 0 && t.SecContact.Value > 0
            && !t.isPriPositionOnUI && !t.isSecPositionOnUI)
        {
            var priPosition = new Vector2(t.PriPositionX.Value, t.PriPositionY.Value);
            var secPosition = new Vector2(t.SecPositionX.Value, t.SecPositionY.Value);
            var priDelta = new Vector2(t.PriDeltaX.Value, t.PriDeltaY.Value);
            var secDelta = new Vector2(t.SecDeltaX.Value, t.SecDeltaY.Value);

            //직전 프레임의 터치 위치를 구하기 위해 "현재 위치 - 위치 변화량"을 계산
            Vector2 priPreviousPosition = priPosition - priDelta;
            Vector2 secPreviousPosition = secPosition - secDelta;

            //직전 프레임에서의 터치 위치 거리 값
            float previousPositionDistance = (priPreviousPosition - secPreviousPosition).magnitude;

            //현재 프레임에서의 터치 위치 거리 값
            float currentPositionDistance = (priPosition - secPosition).magnitude;

            if (t.isPriPositionOnUI || t.isSecPositionOnUI)
                return;

            var multiTouchDelta = previousPositionDistance - currentPositionDistance;
            ScrollZoom(multiTouchDelta);
        }
    }
}
