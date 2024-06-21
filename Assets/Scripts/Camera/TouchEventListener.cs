using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
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
    public bool isPriStartPositionOnUI;
    public bool isSecStartPositionOnUI;
    public bool isPriPositionOnUI;
    public bool isSecPositionOnUI;
    static InputAxis DefaultPan => new()
    {
        Value = 0,
        Range = new Vector2(-180, 180),
        Wrap = true,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.NoRecentering
    };
    static InputAxis DefaultTilt => new()
    {
        Value = 0,
        Range = new Vector2(-45, 45),
        Wrap = false,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.NoRecentering
    };
    static InputAxis DefaultPositionX => new()
    {
        Value = 0,
        Range = new Vector2(0, Screen.width),
        Wrap = false,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.Momentary
    };
    static InputAxis DefaultPositionY => new()
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
        PriDeltaX = DefaultPan;
        PriDeltaY = DefaultTilt;
        PriPositionX = DefaultPositionX;
        PriPositionY = DefaultPositionY;
        PriStartPositionX = DefaultPositionX;
        PriStartPositionY = DefaultPositionY;
        SecDeltaX = DefaultPan;
        SecDeltaY = DefaultTilt;
        SecPositionX = DefaultPositionX;
        SecPositionY = DefaultPositionY;
        SecStartPositionX = DefaultPositionX;
        SecStartPositionY = DefaultPositionY;
    }

    // TODO : RX로 바꾸기
    private void Update()
    {
        // PriPosition는 PriContact보다 우선적으로 값이 들어오는 것으로 보인다.
        // 왜냐면 PriContact == true/fase 하고 PriPosition 값을 체크하면 값이 변경되지 않기 때문임.
        //if (PriContact.Value > 0)
        //    Debug.Log($"{PriPositionX.Value}, {PriPositionY.Value}");
        // PriDeltaX는 PriContact보다 나중에 값이 들어오는 것으로 보인다. 조건 걸었을때 값이 계속 변함.
        //if (PriContact.Value > 0)
        //    Debug.Log($"{PriDeltaX.Value}, {PriDeltaY.Value}");
        // PriPosition는 PriContact보다 우선적으로 값이 들어오는 것으로 보인다.
        //if (PriContact.Value > 0)
        //    Debug.Log($"{PriStartPositionX.Value}, {PriStartPositionX.Value}");

        // *순서 주의!
        // 간헐적으로 Position 값이 제대로 들어오지 않는데 원인을 모르겠음.
        // 등록순서나 계산 순서가 문제인가 싶었는데 그것도 아니고...
        var primaryPosition = new Vector2(PriPositionX.Value, PriPositionY.Value);
        isPriPositionOnUI = OnUIChecker.IsPointerOverUIObject(primaryPosition);

        var secondaryPosition = new Vector2(SecPositionX.Value, SecPositionY.Value);
        isSecPositionOnUI = OnUIChecker.IsPointerOverUIObject(secondaryPosition);

        var primaryStartPosition = new Vector2(PriStartPositionX.Value, PriStartPositionY.Value);
        isPriStartPositionOnUI = OnUIChecker.IsPointerOverUIObject(primaryStartPosition);

        var secondaryStartPosition = new Vector2(SecStartPositionX.Value, SecStartPositionY.Value);
        isSecStartPositionOnUI = OnUIChecker.IsPointerOverUIObject(secondaryStartPosition);

        if (PriContact.Value == 0)
        {
            PriContact = InputAxis.DefaultMomentary;
            PriDeltaX = DefaultPan;
            PriDeltaY = DefaultTilt;
            PriStartPositionX = DefaultPositionX;
            PriStartPositionY = DefaultPositionY;
            PriPositionX = DefaultPositionX;
            PriPositionY = DefaultPositionY;
            isPriPositionOnUI = false;
            isPriStartPositionOnUI = false;
        }

        if (SecContact.Value == 0)
        {
            SecContact = InputAxis.DefaultMomentary;
            SecDeltaX = DefaultPan;
            SecDeltaY = DefaultTilt;
            SecStartPositionX = DefaultPositionX;
            SecStartPositionY = DefaultPositionY;
            SecPositionX = DefaultPositionX;
            SecPositionY = DefaultPositionY;
            isSecPositionOnUI = false;
            isSecStartPositionOnUI = false;
        }
    }
}
