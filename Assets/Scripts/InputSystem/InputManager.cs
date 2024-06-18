using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public enum TouchState
{
    None,
    SinglePressed,
    MultiplePressed,
}

public enum ControlType
{
    Joystick,
    Keyboard,
    Mouse,
}

public class InputManager : MonoBehaviour, ActionMaps.IPlayerActions, ActionMaps.ICameraActions, ActionMaps.ITouchActions
{
    [SerializeField] private PlayerInput _playerInput;
    //[SerializeField] private ActionMaps _actionMaps;
    public ControlType controlType;

    #region Property
    public float MultiTouchDelta { get; private set; } = 0;
    public Vector2 MoveValue { get; private set; } = Vector2.zero;
    public Vector2 ScrollWheel { get; private set; } = Vector2.zero;
    public Vector2 Delta { get; private set; } = Vector2.zero;
    public Vector2 PrimaryTouchDelta { get; private set; } = Vector2.zero;
    public Vector2 SecondaryTouchDelta { get; private set; } = Vector2.zero;
    public Vector2 Position { get; private set; } = Vector2.zero;
    public Vector2 PrimaryTouchPosition { get; private set; } = Vector2.zero;
    public Vector2 SecondaryTouchPosition { get; private set; } = Vector2.zero;
    public TouchPhase LeftMouseButtonPhase { get; private set; } = TouchPhase.None;
    public TouchPhase RightMouseButtonPhase { get; private set; } = TouchPhase.None;
    public TouchPhase PrimaryTouchPhase { get; private set; } = TouchPhase.None;
    public TouchPhase SecondaryTouchPhase { get; private set; } = TouchPhase.None;
    #endregion
    #region Event
    public static event Action<Vector2> OnMoveListener;
    public static event Action<Vector2> OnDeltaListener;
    public static event Action<Vector2> OnPrimaryDeltaListener;
    public static event Action<Vector2> OnSecondaryDeltaListener;
    public static event Action<Vector2> OnPositionListener;
    public static event Action<Vector2> OnPrimaryPositionListener;
    public static event Action<Vector2> OnSecondaryPositionListener;
    public static event Action<TouchPhase> OnLeftClickEventListener;
    public static event Action<TouchPhase> OnRightClickEventListener;
    public static event Action<TouchPhase> OnPrimaryTouchEventListener;
    public static event Action<TouchPhase> OnSecondaryTouchEventListener;
    public static event Action<float> OnMultipleDeltaListener;
    public static event Action OnBackKeyEventListener;
    public static event Action<Vector2> OnWheelEventListener;
    public static event Action<float> OnJumpListener;
    public static event Action<float> OnAttackListener;
    #endregion

    private float _isJumped = 0.0f;
    private float _isAttack = 0.0f;
    public bool isJumped => _isJumped > 0;
    public bool isAttack => _isAttack > 0;

    //MoveValue값이 0에 근접하면 true를 반환하므로, isMove가 false값을 가지기 위해 false값으로 바꿔준다.
    public bool isMove => Mathf.Approximately(MoveValue.sqrMagnitude, 0.0f) == false;

    public bool isMouseCursorOnUI;
    private bool _isPrimaryTouchOnUI;
    private bool _isSecondaryTouchOnUI;

    private void Awake()
    {
        //_actionMaps = new ActionMaps();
        //_actionMaps?.Enable();
        _playerInput?.actions.Enable();
    }

    private void OnDisable()
    {
        //_actionMaps.Disable();
        _playerInput.actions.Disable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveValue = context.ReadValue<Vector2>();
        OnMoveListener?.Invoke(MoveValue);
    }

    public void OnPosition(InputAction.CallbackContext context)
    {
        Position = context.ReadValue<Vector2>();
        OnPositionListener?.Invoke(Position);

        isMouseCursorOnUI = OnUIChecker.IsPointerOverUIObject(Position);
    }

    public void OnClickLeftMouseButton(InputAction.CallbackContext context)
    {
        if (context.started) //터치 이벤트 시작시
        {
            LeftMouseButtonPhase = TouchPhase.Began;
        }
        else if (context.performed && context.control.IsPressed())
        {
            LeftMouseButtonPhase = TouchPhase.Moved;
        }
        else if (context.canceled)
        {
            LeftMouseButtonPhase = TouchPhase.Ended;
        }

        Debug.Log($"Left Mouse ButtonPhase: {LeftMouseButtonPhase}");
        OnLeftClickEventListener?.Invoke(LeftMouseButtonPhase);
    }

    public void OnClickRightMouseButton(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            RightMouseButtonPhase = TouchPhase.Began;
        }
        else if (context.performed && context.control.IsPressed())
        {
            RightMouseButtonPhase = TouchPhase.Moved;
        }
        else if (context.canceled)
        {
            RightMouseButtonPhase = TouchPhase.Ended;
        }

        Debug.Log($"Right Mouse ButtonPhase: {RightMouseButtonPhase}");
        OnRightClickEventListener?.Invoke(RightMouseButtonPhase);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        _isJumped = context.ReadValue<float>();

        OnJumpListener?.Invoke(_isJumped);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        _isAttack = context.ReadValue<float>();

        OnAttackListener?.Invoke(_isAttack);
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        ScrollWheel = context.ReadValue<Vector2>();

        OnWheelEventListener?.Invoke(ScrollWheel);
    }

    public void OnRotate(InputAction.CallbackContext context)
    {
        Delta = context.ReadValue<Vector2>();
        OnDeltaListener?.Invoke(Delta);
    }

    public void OnPrimaryTouchContact(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            PrimaryTouchPhase = TouchPhase.Began;
        }
        else if (context.performed && context.control.IsPressed())
        {
            PrimaryTouchPhase = TouchPhase.Moved;
        }
        else if (context.canceled)
        {
            PrimaryTouchPhase = TouchPhase.Ended;
        }

        Debug.Log($"Primary Touch Phase: {PrimaryTouchPhase}");
        OnPrimaryTouchEventListener?.Invoke(PrimaryTouchPhase);
    }

    public void OnSecondaryTouchContact(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            SecondaryTouchPhase = TouchPhase.Began;
        }
        else if (context.performed && context.control.IsPressed())
        {
            SecondaryTouchPhase = TouchPhase.Moved;
        }
        else if (context.canceled)
        {
            SecondaryTouchPhase = TouchPhase.Ended;
        }

        Debug.Log($"Secondary Touch Phase: {SecondaryTouchPhase}");
        OnSecondaryTouchEventListener?.Invoke(SecondaryTouchPhase);
    }

    public void OnPrimaryTouchPosition(InputAction.CallbackContext context)
    {
        PrimaryTouchPosition = context.ReadValue<Vector2>();
        OnPrimaryPositionListener?.Invoke(PrimaryTouchPosition);

        _isPrimaryTouchOnUI = OnUIChecker.IsPointerOverUIObject(PrimaryTouchPosition);
    }

    public void OnSecondaryTouchPosition(InputAction.CallbackContext context)
    {
        SecondaryTouchPosition = context.ReadValue<Vector2>();
        OnSecondaryPositionListener?.Invoke(SecondaryTouchPosition);

        _isSecondaryTouchOnUI = OnUIChecker.IsPointerOverUIObject(SecondaryTouchPosition);
    }

    public void OnPrimaryTouchDelta(InputAction.CallbackContext context)
    {
        PrimaryTouchDelta = context.ReadValue<Vector2>();
        OnPrimaryDeltaListener?.Invoke(PrimaryTouchDelta);
    }

    public void OnSecondaryTouchDelta(InputAction.CallbackContext context)
    {
        SecondaryTouchDelta = context.ReadValue<Vector2>();
        OnSecondaryDeltaListener?.Invoke(SecondaryTouchDelta);
        CalcMultiTouchDelta();
    }
    private void CalcMultiTouchDelta()
    {
        if ((PrimaryTouchPhase == TouchPhase.Moved || PrimaryTouchPhase == TouchPhase.Stationary) ||
            (SecondaryTouchPhase == TouchPhase.Moved || SecondaryTouchPhase == TouchPhase.Stationary))
        {
            //직전 프레임의 터치 위치를 구하기 위해 "현재 위치 - 위치  변화량"을 계산
            Vector2 primaryTouchPreviousPosition = PrimaryTouchPosition - PrimaryTouchDelta;
            Vector2 secondaryTouchPreviousPosition = SecondaryTouchPosition - SecondaryTouchDelta;

            //직전 프레임에서의 터치 위치 거리 값
            float previousPositionDistance = (primaryTouchPreviousPosition - secondaryTouchPreviousPosition).magnitude;
            //현재 프레임에서의 터치 위치 거리 값
            float currentPositionDistance = (PrimaryTouchPosition - SecondaryTouchPosition).magnitude;

            // 터치한 손가락 중 하나라도 UI위에 있으면 터치 무시
            if (_isPrimaryTouchOnUI || _isSecondaryTouchOnUI)
            {
                return;
            }

            MultiTouchDelta = previousPositionDistance - currentPositionDistance;
            OnMultipleDeltaListener?.Invoke(MultiTouchDelta);
        }
    }
}
