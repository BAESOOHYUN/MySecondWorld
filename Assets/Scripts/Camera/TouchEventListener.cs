using System;
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

    public bool wasPriDown;
    public bool wasSecDown;

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

    private void Update()
    {
        // 첫 번째 터치가 시작된 순간에만 StartPosition UI 체크 (최적화)
        if (PriContact.Value > 0)
        {
            if (!wasPriDown)
            {
                var startPos = new Vector2(PriStartPositionX.Value, PriStartPositionY.Value);
                isPriStartPositionOnUI = OnUIChecker.IsPointerOverUIObject(startPos);
                wasPriDown = true;
            }

            var currPos = new Vector2(PriPositionX.Value, PriPositionY.Value);
            isPriPositionOnUI = OnUIChecker.IsPointerOverUIObject(currPos);
        }
        else //(PriContact.Value == 0)
        {
            ResetPri();
        }

        // 두 번째 터치가 시작된 순간에만 StartPosition UI 체크 (최적화)
        if (SecContact.Value > 0)
        {
            if (!wasSecDown)
            {
                var startPos = new Vector2(SecStartPositionX.Value, SecStartPositionY.Value);
                isSecStartPositionOnUI = OnUIChecker.IsPointerOverUIObject(startPos);
                wasSecDown = true;
            }

            var currPos = new Vector2(SecPositionX.Value, SecPositionY.Value);
            isSecPositionOnUI = OnUIChecker.IsPointerOverUIObject(currPos);
        }
        else //(SecContact.Value == 0)
        {
            ResetSec();
        }

    }

    private void ResetPri()
    {
        PriDeltaX.Value = 0;
        PriDeltaY.Value = 0;
        isPriPositionOnUI = false;
        isPriStartPositionOnUI = false;
        wasPriDown = false;
    }

    private void ResetSec()
    {
        SecDeltaX.Value = 0;
        SecDeltaY.Value = 0;
        isSecPositionOnUI = false;
        isSecStartPositionOnUI = false;
        wasSecDown = false;
    }

}
