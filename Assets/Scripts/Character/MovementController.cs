using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour, IInputAxisOwner
{
    [SerializeField] private TouchEventListener _touchListener;
    [SerializeField] private CharacterController _characterController;
    //private AnimatorController _animatorController;
    [SerializeField] private float _moveSpeed = 3.0f; // 속력
    [SerializeField] private float _jumpSpeed = 3.0f; //점프 속력
    [SerializeField] private float _jumpHeight = 1.0f; //점프 높이

    private Transform _cameraTransform;
    private Vector3 _moveVec; // 플레이어 방향
    private Vector3 _playerVelocity; // 속도
    private float _gravity = -9.81f; // 중력
    private bool _isGround;

    public InputAxis MoveX = InputAxis.DefaultMomentary;
    public InputAxis MoveY = InputAxis.DefaultMomentary;
    public InputAxis Jump = InputAxis.DefaultMomentary;
    public InputAxis Attack = InputAxis.DefaultMomentary;

    private void FixedUpdate()
    {
        _isGround = _characterController.isGrounded;
        if (_isGround && _playerVelocity.y < 0)
            _playerVelocity.y = 0f;

        if ((_touchListener.deviceType == DeviceType.PC || (_touchListener.deviceType == DeviceType.Mobile))
            JoyStickMove();

        Jumping();
        Gravity();
    }

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref MoveX, Name = nameof(MoveX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref MoveY, Name = nameof(MoveY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref Jump, Name = nameof(Jump) });
        axes.Add(new() { DrivenAxis = () => ref Attack, Name = nameof(Attack) });
    }

    private void JoyStickMove()
    {
        _moveVec = new Vector3(MoveX.Value, 0, MoveY.Value);

        if (_moveVec != Vector3.zero)
        {
            _moveVec = _cameraTransform.TransformDirection(_moveVec);
            _moveVec.y = 0;
            _characterController.Move(_moveVec * _moveSpeed * Time.deltaTime);
            transform.forward = _moveVec; // 안해주면 조이스틱 놨을때 정방향 보면서 돌아감
        }
    }

    private void Jumping()
    {
        // TODO : Falling & Landing
        if (Jump.Value > 0 && _isGround)
            _playerVelocity.y += Mathf.Sqrt(_jumpHeight * _jumpSpeed * -_gravity);
    }

    private void Gravity()
    {
        _playerVelocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_playerVelocity * Time.deltaTime);
    }
}
