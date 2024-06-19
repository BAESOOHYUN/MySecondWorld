using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class ZoomCameraController : MonoBehaviour, IInputAxisOwner
{
    [SerializeField] private CinemachineFollowZoom _followZoom;
    [SerializeField] private InputManager _inputManager;

    private float _minFOV = 0.5f;
    private float _maxFOV = 4.7f;
    private bool _isMouseCursorOnUI = false;
    private bool _isPrimaryTouchOnUI = false;
    private bool _isSecondaryTouchOnUI = false;

    public InputAxis Zoom = InputAxis.DefaultMomentary;
    public InputAxis MousePositionX = InputAxis.DefaultMomentary;
    public InputAxis MousePositionY = InputAxis.DefaultMomentary;
    public InputAxis PrimaryTouchContact = InputAxis.DefaultMomentary; // TODO : Mouse Click 이나 Contect 같은 애들은 나중에 따로 떼어야 할지도?
    public InputAxis PrimaryTouchDeltaX = InputAxis.DefaultMomentary;
    public InputAxis PrimaryTouchDeltaY = InputAxis.DefaultMomentary;
    public InputAxis PrimaryTouchPositionX = InputAxis.DefaultMomentary;
    public InputAxis PrimaryTouchPositionY = InputAxis.DefaultMomentary;
    public InputAxis SecondaryTouchContact = InputAxis.DefaultMomentary;
    public InputAxis SecondaryTouchDeltaX = InputAxis.DefaultMomentary;
    public InputAxis SecondaryTouchDeltaY = InputAxis.DefaultMomentary;
    public InputAxis SecondaryTouchPositionX = InputAxis.DefaultMomentary;
    public InputAxis SecondaryTouchPositionY = InputAxis.DefaultMomentary;

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref Zoom, Name = nameof(Zoom), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PrimaryTouchContact, Name = nameof(PrimaryTouchContact) });
        axes.Add(new() { DrivenAxis = () => ref PrimaryTouchDeltaX, Name = nameof(PrimaryTouchDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PrimaryTouchDeltaY, Name = nameof(PrimaryTouchDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PrimaryTouchPositionX, Name = nameof(PrimaryTouchPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PrimaryTouchPositionY, Name = nameof(PrimaryTouchPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecondaryTouchContact, Name = nameof(SecondaryTouchContact) });
        axes.Add(new() { DrivenAxis = () => ref SecondaryTouchDeltaX, Name = nameof(SecondaryTouchDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecondaryTouchDeltaY, Name = nameof(SecondaryTouchDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecondaryTouchPositionX, Name = nameof(SecondaryTouchPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecondaryTouchPositionY, Name = nameof(SecondaryTouchPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    private void Update()
    {
        // TODO : 인풋 매니저 자체를 DI에 등록하고 끌어다 쓰기
        // TODO : Update대신 RX 이용.
        if (!_inputManager.isMouseCursorOnUI) 
        {
            // TODO : 스크롤 줌과 핀치줌 분기.
            // 컨트롤 타입이 아니라 디바이스 타입에 따라 나누는 게 맞는거 같긴 한데... 안꼬이려나?
            switch (_inputManager.controlType)
            {
                case ControlType.Joystick:
                    PinchZoom();
                    break;
                case ControlType.Keyboard:
                case ControlType.Mouse:
                    ScrollZoom(Zoom.Value);
                    break;
                default:
                    break;
            }
        }
    }

    private void ScrollZoom(float zoomValue)
    {
        var preFOV = _followZoom.Width - zoomValue * 0.5f;
        _followZoom.Width = Mathf.Clamp(preFOV, _minFOV, _maxFOV);
    }

    private void PinchZoom()
    {
        // 싱글 터치
        if (PrimaryTouchContact.Value > 0)
        {
            Debug.Log($"PrimaryTouchContact : {PrimaryTouchContact.Value}");
            var primaryTouchPosition = new Vector2(PrimaryTouchPositionX.Value, PrimaryTouchPositionY.Value);
            _isPrimaryTouchOnUI = OnUIChecker.IsPointerOverUIObject(primaryTouchPosition);


            Debug.Log($"primaryTouchPosition : {primaryTouchPosition}");
            Debug.Log($"_isPrimaryTouchOnUI : {_isPrimaryTouchOnUI}");

            // 멀티 터치
            if (SecondaryTouchContact.Value > 0)
            {
                Debug.Log($"SecondaryTouchContact : {SecondaryTouchContact.Value}");
                var secondaryTouchPosition = new Vector2(SecondaryTouchPositionX.Value, SecondaryTouchPositionY.Value);
                _isSecondaryTouchOnUI = OnUIChecker.IsPointerOverUIObject(secondaryTouchPosition);

                Debug.Log($"secondaryTouchPosition : {secondaryTouchPosition}");
                Debug.Log($"_isSecondaryTouchOnUI : {_isSecondaryTouchOnUI}");

                var primaryTouchDelta = new Vector2(PrimaryTouchDeltaX.Value, PrimaryTouchDeltaY.Value);
                var secondaryTouchDelta = new Vector2(SecondaryTouchDeltaX.Value, SecondaryTouchDeltaY.Value);


                Debug.Log($"primaryTouchDelta : {primaryTouchDelta}");
                Debug.Log($"secondaryTouchDelta : {secondaryTouchDelta}");

                //직전 프레임의 터치 위치를 구하기 위해 "현재 위치 - 위치  변화량"을 계산
                Vector2 primaryTouchPreviousPosition = primaryTouchPosition - primaryTouchDelta;
                Vector2 secondaryTouchPreviousPosition = secondaryTouchPosition - secondaryTouchDelta;


                Debug.Log($"primaryTouchPreviousPosition : {primaryTouchPreviousPosition}");
                Debug.Log($"secondaryTouchPreviousPosition : {secondaryTouchPreviousPosition}");

                //직전 프레임에서의 터치 위치 거리 값
                float previousPositionDistance = (primaryTouchPreviousPosition - secondaryTouchPreviousPosition).magnitude;
                //현재 프레임에서의 터치 위치 거리 값
                float currentPositionDistance = (primaryTouchPosition - secondaryTouchPosition).magnitude;


                Debug.Log($"previousPositionDistance : {previousPositionDistance}");
                Debug.Log($"currentPositionDistance : {currentPositionDistance}");

                if (_isPrimaryTouchOnUI || _isSecondaryTouchOnUI)
                {
                    return;
                }

                var multiTouchDelta = previousPositionDistance - currentPositionDistance;

                Debug.Log($"multiTouchDelta : {multiTouchDelta}");

                ScrollZoom(multiTouchDelta);
            }
        }
    }
}
