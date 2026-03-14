using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour, IInputAxisOwner
{
    public event Action<bool> OnGrounded;
    public event Action OnJump;
    public event Action OnFall;
    public event Action OnLand;
    public event Action OnLand_Cancel;

    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _jumpSpeed = 3f;
    [SerializeField] private float _jumpHeight = 1f;
    [SerializeField] private float _airControlMultiplier = 0.5f; // 공중 이동 감도

    private Vector3 _inputDirection; 
    private Vector3 _moveDirection; // 수평 이동 벡터 (JoyStickMove에서 계산, Gravity에서 사용)
    private Vector3 _velocity; // 현재 속도
    private float _gravity = -9.81f;
    private float _fallMultiplier = 2.5f; // 낙하 시 추가 가속도
    private bool _isGrounded;
    private bool _wasGrounded; 
    private bool _isFalling;
    private bool _wasFalling;

    public InputAxis MoveX = InputAxis.DefaultMomentary;
    public InputAxis MoveY = InputAxis.DefaultMomentary;
    public InputAxis Jump = InputAxis.DefaultMomentary;
    public InputAxis Attack = InputAxis.DefaultMomentary;
   
    private void Update()
    {
        JoyStickMove();
        Jumping();
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
        Vector3 rawInput = new Vector3(MoveX.Value, 0f, MoveY.Value);

        if (rawInput.sqrMagnitude < 0.01f)
        {
            _moveDirection = Vector3.zero;
            _inputDirection = Vector3.zero;
            return;
        }

        _inputDirection = rawInput.normalized;

        // 카메라 Y축 회전만 추출해서 이동 방향 계산 (pitch 영향 없음)
        float cameraAngle = _cinemachineCamera.transform.eulerAngles.y;
        float targetAngle = Mathf.Atan2(_inputDirection.x, _inputDirection.z) * Mathf.Rad2Deg + cameraAngle;
        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        
        // 공중에서는 이동 감도 줄이기
        float speed = _isGrounded ? _moveSpeed : _moveSpeed * _airControlMultiplier;
        _moveDirection = moveDir * speed;

        // 부드러운 캐릭터 회전
        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
    }

    private void Jumping()
    {
        // 이전 프레임 상태 기록
        _wasGrounded = _isGrounded;
        _wasFalling = _isFalling;

        // 현재 프레임 지면 상태 갱신
        _isGrounded = _controller.isGrounded;
        if (_isGrounded && _velocity.y < 0)
            _velocity.y = -2f;

        // 매 프레임 지면 상태 전달
        OnGrounded?.Invoke(_isGrounded);

        // 점프 입력
        if (Jump.Value > 0 && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(_jumpHeight * _jumpSpeed * -_gravity);
            OnJump?.Invoke();
        }

        // 낙하 상태 갱신 및 이벤트 (진입 순간 1회)
        _isFalling = _velocity.y < 0 && !_isGrounded;
        if (_isFalling && !_wasFalling)
            OnFall?.Invoke();

        // 착지 이벤트
        if (!_wasGrounded && _isGrounded)
            OnLand?.Invoke();

        // 중력 적용 (낙하 시 추가 가속)
        float currentGravity = _velocity.y < 0 ? _gravity * _fallMultiplier : _gravity;
        Gravity(currentGravity);
    }

    private void Gravity(float gravity)
    {
        _velocity.y += gravity * Time.deltaTime;
        // 수평 + 수직 합산 후 한 번만 Move 호출
        _controller.Move((_moveDirection + _velocity) * Time.deltaTime);
    }
}
