using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
public class ThirdPersonCameraController : MonoBehaviour, IInputAxisOwner
{
    [SerializeField] private CinemachineOrbitalFollow _orbitFollow; // 3인칭 일 때
    //[SerializeField] private CinemachinePanTilt _pantilt; // 1인칭 일 때
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private float _postHorizontalAxis;
    [SerializeField] private float _postVerticalAxis;

    public InputAxis ClickRightMouseButton = InputAxis.DefaultMomentary;
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
        Range = new Vector2(-10, 45),
        Wrap = false,
        Center = 0,
        Restrictions = InputAxis.RestrictionFlags.NoRecentering
    };


    [Tooltip("Horizontal rotation.  Value is -1..1.")]
    public InputAxis Pan = DefaultPan;

    [Tooltip("Vertical rotation.  Value is -1..1.")]
    public InputAxis Tilt = DefaultTilt;


    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref ClickRightMouseButton, Name = nameof(ClickRightMouseButton) });
        axes.Add(new() { DrivenAxis = () => ref Pan, Name = nameof(Pan), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref Tilt, Name = nameof(Tilt), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    private void Reset()
    {
        Pan = DefaultPan;
        Tilt = DefaultTilt;
    }

    private void Update()
    {
        if (!_inputManager.isMouseCursorOnUI)
            RotateCamera();
    }


    private void RotateCamera()
    {
        if (ClickRightMouseButton.Value > 0)
        {
            _orbitFollow.HorizontalAxis.Value = Pan.Value + _postHorizontalAxis;
            _orbitFollow.VerticalAxis.Value = Mathf.Clamp(-Tilt.Value + _postVerticalAxis, -10, 45);
        }
        else
        {
            _postHorizontalAxis = _orbitFollow.HorizontalAxis.Value;
            _postVerticalAxis = _orbitFollow.VerticalAxis.Value;
            Pan.Value = 0;
            Tilt.Value = 0;
        }
    }
}
