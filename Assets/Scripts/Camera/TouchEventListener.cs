using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UniRx;

public enum DeviceType
{
    PC,
    Mobile
}
public enum ControllerType
{
    Joystick,
    KeyBoard,
    Mouse,
    Touch
}

public class TouchEventListener : MonoBehaviour, IInputAxisOwner
{
    public DeviceType deviceType;
    public ControllerType controllerType;
    public BoolReactiveProperty isPriStartPositionOnUI = new BoolReactiveProperty(false);
    public BoolReactiveProperty isSecStartPositionOnUI = new BoolReactiveProperty(false);
    public BoolReactiveProperty isPriPositionOnUI = new BoolReactiveProperty(false);
    public BoolReactiveProperty isSecPositionOnUI = new BoolReactiveProperty(false);
    private static InputAxis DefaultPan => new()
    {
        Value = 0,
        Range = new Vector2(-180, 180),
        Wrap = true,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.NoRecentering
    };
    private static InputAxis DefaultTilt => new()
    {
        Value = 0,
        Range = new Vector2(-45, 45),
        Wrap = false,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.NoRecentering
    };
    private static InputAxis DefaultPositionX => new()
    {
        Value = 0,
        Range = new Vector2(0, Screen.width),
        Wrap = false,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.Momentary
    };
    private static InputAxis DefaultPositionY => new()
    {
        Value = 0,
        Range = new Vector2(0, Screen.height),
        Wrap = false,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.Momentary
    };

    public InputAxis PriContact = InputAxis.DefaultMomentary;
    public InputAxis PriDeltaX = DefaultPan;
    public InputAxis PriDeltaY = DefaultTilt;
    public InputAxis PriPositionX = DefaultPositionX;
    public InputAxis PriPositionY = DefaultPositionY;
    public InputAxis PriStartPositionX = DefaultPositionX;
    public InputAxis PriStartPositionY = DefaultPositionY;
    public InputAxis SecContact = InputAxis.DefaultMomentary;
    public InputAxis SecDeltaX = DefaultPan;
    public InputAxis SecDeltaY = DefaultTilt;
    public InputAxis SecPositionX = DefaultPositionX;
    public InputAxis SecPositionY = DefaultPositionY;
    public InputAxis SecStartPositionX = DefaultPositionX;
    public InputAxis SecStartPositionY = DefaultPositionY;

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref PriContact, Name = nameof(PriContact) });
        axes.Add(new() { DrivenAxis = () => ref PriDeltaX, Name = nameof(PriDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriDeltaY, Name = nameof(PriDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PriPositionX, Name = nameof(PriPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriPositionY, Name = nameof(PriPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PriStartPositionX, Name = nameof(PriStartPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriStartPositionY, Name = nameof(PriStartPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecContact, Name = nameof(SecContact) });
        axes.Add(new() { DrivenAxis = () => ref SecDeltaX, Name = nameof(SecDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecDeltaY, Name = nameof(SecDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecPositionX, Name = nameof(SecPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecPositionY, Name = nameof(SecPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecStartPositionX, Name = nameof(SecStartPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecStartPositionY, Name = nameof(SecStartPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    private void Start()
    {
        // 유니티의 Update() 콜백함수와 동일하게 매프레임 호출 되는 Observable 을 생성한다.
        // Observable.EveryUpdate() 는 static 메소드 이므로 MonoBehaviour 상속 클래스가 아닌 외부에서도 호출이 가능하다.
        // UniRx.Triggers 의 UpdateAsObservable 과 다르게 특정 GameObject 에 종속되어 있지 않기 때문에 Dispose 시점을 수동으로 지정해줘야 하므로 Subscribe 또는 AddTo 오퍼레이터를 사용해 관리할 필요가 있다
        // 일반적으로 특정 오브젝트에 종속되지 않는 상황, 예를 들면 FPS 출력이나 마우스 클릭 체크, 네트워크 체크 등의 경우 등에 유용하다.
        Observable.EveryUpdate()
            .Subscribe(_ => UpdateTouchPositions())
            .AddTo(this); // 사용이 끝나면 명시적으로 Dispose
    }

    private void UpdateTouchPositions()
    {
        var primaryPosition = new Vector2(PriPositionX.Value, PriPositionY.Value);
        isPriPositionOnUI.Value = OnUIChecker.IsPointerOverUIObject(primaryPosition);

        var secondaryPosition = new Vector2(SecPositionX.Value, SecPositionY.Value);
        isSecPositionOnUI.Value = OnUIChecker.IsPointerOverUIObject(secondaryPosition);

        var primaryStartPosition = new Vector2(PriStartPositionX.Value, PriStartPositionY.Value);
        isPriStartPositionOnUI.Value = OnUIChecker.IsPointerOverUIObject(primaryStartPosition);

        var secondaryStartPosition = new Vector2(SecStartPositionX.Value, SecStartPositionY.Value);
        isSecStartPositionOnUI.Value = OnUIChecker.IsPointerOverUIObject(secondaryStartPosition);

        if (PriContact.Value == 0)
        {
            ResetPrimaryInputAxes();
        }

        if (SecContact.Value == 0)
        {
            ResetSecondaryInputAxes();
        }
    }

    private void ResetPrimaryInputAxes()
    {
        PriContact = InputAxis.DefaultMomentary;
        PriDeltaX = DefaultPan;
        PriDeltaY = DefaultTilt;
        PriStartPositionX = DefaultPositionX;
        PriStartPositionY = DefaultPositionY;
        PriPositionX = DefaultPositionX;
        PriPositionY = DefaultPositionY;
        isPriPositionOnUI.Value = false;
        isPriStartPositionOnUI.Value = false;
    }

    private void ResetSecondaryInputAxes()
    {
        SecContact = InputAxis.DefaultMomentary;
        SecDeltaX = DefaultPan;
        SecDeltaY = DefaultTilt;
        SecStartPositionX = DefaultPositionX;
        SecStartPositionY = DefaultPositionY;
        SecPositionX = DefaultPositionX;
        SecPositionY = DefaultPositionY;
        isSecPositionOnUI.Value = false;
        isSecStartPositionOnUI.Value = false;
    }
}
