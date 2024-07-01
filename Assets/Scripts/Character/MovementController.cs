using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using System;

[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour, IInputAxisOwner
{
    [SerializeField] private TouchEventListener _touchListener;
    [SerializeField] private CharacterController _characterController;
    //private AnimatorController _animatorController;
    private Transform _cameraTransform;

    [SerializeField] private float _horizontalMoveSpeed = 3.0f; // 수평 속력
    [SerializeField] private float _jumpHeight = 2.0f; //점프 높이
    private float _currentYVelocity; // 현재 수직 속도
    private float _currentVerticalSpeed; // 현재 수직 속력
    private Vector3 _horizontalMovement; // 수평 방향 벡터
    private Vector3 _targetPosition; // 목표 지점 벡터
    private float _gravity = -9.81f; // 중력
    private bool _isInitialClickOnUI = false;
    private bool _isMouseDown = false;
    private IDisposable _mouseMoveCo;

    [SerializeField] private BoolReactiveProperty _isGrounded = new BoolReactiveProperty(true);

    private InputAxis MoveX = InputAxis.DefaultMomentary;
    private InputAxis MoveY = InputAxis.DefaultMomentary;
    private InputAxis Jump = InputAxis.DefaultMomentary;
    private InputAxis Attack = InputAxis.DefaultMomentary;
    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref MoveX, Name = nameof(MoveX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref MoveY, Name = nameof(MoveY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref Jump, Name = nameof(Jump) });
        axes.Add(new() { DrivenAxis = () => ref Attack, Name = nameof(Attack) });
    }

    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawSphere(_targetPosition, 0.2f);
    //}

    public void OnEnable()
    {
        _cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        // TODO : 마비노기처럼 PC에서 조이스틱과 마우스 이동 둘 다 되게 하려면?
        // TODO : 점프중 묘하게 카메라 느림.
        //if (_touchListener.deviceType == DeviceType.PC && _touchListener.controllerType == ControllerType.Mouse)
            MouseMove();
        //else if ((_touchListener.deviceType == DeviceType.PC && _touchListener.controllerType == ControllerType.KeyBoard) ||
                //(_touchListener.deviceType == DeviceType.Mobile && _touchListener.controllerType == ControllerType.Joystick))
            JoyStickMove();

        Observable.EveryUpdate()
            .Subscribe(_ =>
            {
                Vector3 move;
                bool isGrounded = _characterController.isGrounded;
                bool isJumping = Jump.Value > 0;

                // 점프와 착지 로직 처리
                FallingAndLanding(isGrounded);
                if (isJumping && isGrounded)
                {
                    _currentVerticalSpeed = Mathf.Sqrt(2 * -_gravity * _jumpHeight);
                    _currentYVelocity = _currentVerticalSpeed;
                }

                // 캐릭터 이동 처리
                if (_horizontalMovement != Vector3.zero)
                    transform.forward = _horizontalMovement;

                move = _horizontalMovement * _horizontalMoveSpeed * Time.deltaTime;
                move.y = _currentYVelocity * Time.deltaTime;
                _characterController.Move(move);
            })
            .AddTo(this);

    }

    private void FallingAndLanding(bool isGrounded)
    {
        bool isFalling = _currentVerticalSpeed <= 0.0f; //버튼 프레스 조건 or 조건으로 추가
        float falMultiplier = 2.0f; // 낙하 속도를 증가시키기 위한 배율

        if (isGrounded) //캐릭터가 지면에 있을 때 실행
        {
            _currentYVelocity = _currentVerticalSpeed += Physics.gravity.y * Time.deltaTime;
        }
        else if (isFalling) //캐릭터가 낙하 중일 때 실행
        {
            float previousVerticalSpeed = _currentVerticalSpeed;
            _currentVerticalSpeed = _currentVerticalSpeed + (_gravity * falMultiplier * Time.deltaTime);
            _currentYVelocity = Mathf.Max((previousVerticalSpeed + _currentVerticalSpeed) * 0.5f, -20.0f);
        }
        else // Landing (캐릭터가 상승 중일 때 실행)
        {
            float previousVerticalSpeed = _currentVerticalSpeed;
            _currentVerticalSpeed = _currentVerticalSpeed + (_gravity * Time.deltaTime);
            _currentYVelocity = (previousVerticalSpeed + _currentVerticalSpeed) * 0.5f;
        }
    }

 
    private void JoyStickMove()
    {
        // MoveX와 MoveY의 변화 감지 및 _horizontalMovement 업데이트
        Observable.EveryFixedUpdate()
            .Select(_=> new Vector3(MoveX.Value, 0, MoveY.Value))
            .DistinctUntilChanged() // 값이 변할 때만 통과
            .Subscribe(dir =>
            {
                _horizontalMovement = dir;

                // 입력이 없는 경우 _horizontalMovement 리셋
                if (_horizontalMovement == Vector3.zero)
                {
                    _horizontalMovement = Vector3.zero;
                }
                else if (_horizontalMovement != Vector3.zero)
                {
                    _horizontalMovement = _cameraTransform.TransformDirection(_horizontalMovement);
                    _horizontalMovement.y = 0;
                }

            })
            .AddTo(this);
    }
    
    private void MouseMove()
    { 
        // 마우스 클릭 이벤트를 관찰
        Observable.EveryUpdate()
            .Select(_ => new
            {
                ContactValue = _touchListener.PriContact.Value,
                MousePosition = new Vector3(_touchListener.PriPositionX.Value, _touchListener.PriPositionY.Value),
                IsPositionOnUI = _touchListener.isPriPositionOnUI.Value
            })
            .DistinctUntilChanged()
            .Subscribe(input =>
            {
                if (input.ContactValue > 0)
                {
                    if (!_isMouseDown)
                    {
                        // 마우스 클릭이 시작되는 순간
                        _isInitialClickOnUI = input.IsPositionOnUI;
                        _isMouseDown = true;
                    }

                    // 마우스가 눌려있는 동안 && 점프중이 아닐 때에만 (비행까지 구현시 isGrounded 조건 삭제)
                    if (!_isInitialClickOnUI && _characterController.isGrounded)
                    {
                        Ray ray = Camera.main.ScreenPointToRay(input.MousePosition);

                        if (Physics.Raycast(ray, out RaycastHit hit, 1000.0f, SetTargetableLayerMask("Ground")))
                        {
                            _targetPosition = hit.point;

                            //기존의 코루틴 구독 해제
                            _mouseMoveCo?.Dispose();
                            // 새로운 코루틴 구독
                            _mouseMoveCo = Observable.FromCoroutine(() => PlayerMoveTowards(_targetPosition)).Subscribe();
                        }
                    }
                }
                else
                {
                    // 마우스 클릭이 해제된 순간
                    _isMouseDown = false;
                }
            })
            .AddTo(this);
    }

    private IEnumerator PlayerMoveTowards(Vector3 targetPoint)
    {
        // 플레이어가 땅으로 처박히지 않게 방어
        float playerDistanceToFloor = transform.position.y - targetPoint.y;
        targetPoint.y += playerDistanceToFloor;

        // 내 현재 위치와 타겟포인트 거리 비교
        var distance = Vector3.Distance(transform.position, targetPoint);

        while (distance > 0.1f)
        {
            _horizontalMovement = (targetPoint - transform.position).normalized;
            _horizontalMovement.y = 0;
            yield return null;

            // 거리 업데이트
            distance = Vector3.Distance(transform.position, targetPoint);
        }

        // 목표 지점에 도달하면 이동 멈춤
        _horizontalMovement = Vector3.zero;
    }

    private int SetTargetableLayerMask(string cullingMaskName)
    {
        int layerMask = LayerMask.NameToLayer(cullingMaskName);
        layerMask = 1 << layerMask;
        return layerMask;
    }
}
