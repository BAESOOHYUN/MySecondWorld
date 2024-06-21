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
    public InputAxis SecContact = InputAxis.DefaultMomentary;
    public InputAxis SecDeltaX = DefaultPan;
    public InputAxis SecDeltaY = DefaultTilt;
    public InputAxis SecPositionX = DefaultPositionX;
    public InputAxis SecPositionY = DefaultPositionY;

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref PriContact, Name = nameof(PriContact) });
        axes.Add(new() { DrivenAxis = () => ref PriDeltaX, Name = nameof(PriDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriDeltaY, Name = nameof(PriDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PriPositionX, Name = nameof(PriPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriPositionY, Name = nameof(PriPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecContact, Name = nameof(SecContact) });
        axes.Add(new() { DrivenAxis = () => ref SecDeltaX, Name = nameof(SecDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecDeltaY, Name = nameof(SecDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecPositionX, Name = nameof(SecPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecPositionY, Name = nameof(SecPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    private void Start()
    {
        PriDeltaX = DefaultPan;
        PriDeltaY = DefaultTilt;
        PriPositionX = DefaultPositionX;
        PriPositionY = DefaultPositionY;
        SecDeltaX = DefaultPan;
        SecDeltaY = DefaultTilt;
        SecPositionX = DefaultPositionX;
        SecPositionY = DefaultPositionY;
    }
    
    // TODO : RX로 바꾸기
    private void Update()
    {
        var primaryPosition = new Vector2(PriPositionX.Value, PriPositionY.Value);
        isPriPositionOnUI = OnUIChecker.IsPointerOverUIObject(primaryPosition);

        var secondaryPosition = new Vector2(SecPositionX.Value, SecPositionY.Value);
        isSecPositionOnUI = OnUIChecker.IsPointerOverUIObject(secondaryPosition);
    }
}
