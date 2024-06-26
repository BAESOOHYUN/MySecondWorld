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
    [SerializeField] private float _moveSpeed = 3.0f; // 속력
    [SerializeField] private float _jumpHeight = 2.0f; //점프 높이
    private Camera _mainCamera;
    private Transform _cameraTransform;
    private Vector3 _moveDir; // 플레이어 방향
    private Vector3 _targetPosition; // 목표 방향
    private Vector3 _playerVelocity; // 속도
    private float _verticalSpeed; // 수직 속력
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_targetPosition, 0.5f);
    }

    public void OnEnable()
    {
        _mainCamera = Camera.main;
        _cameraTransform = _mainCamera.transform;
    }

    private void Start()
    {
        // TODO : 마비노기처럼 PC에서 조이스틱과 마우스 이동 둘 다 되게 하려면?
        // TODO : 마우스로 드래그해서 지면을 클릭했을때 버벅거리는 현상
        Observable.EveryFixedUpdate()
            .Where(_ => _touchListener.deviceType == DeviceType.PC && _touchListener.controllerType == ControllerType.Mouse)
            .Subscribe(_ => CheckMouseStartPosition())
            .AddTo(this);

        Observable.EveryFixedUpdate()
            .Where(_ => (_touchListener.deviceType == DeviceType.PC && _touchListener.controllerType == ControllerType.KeyBoard) ||
                        (_touchListener.deviceType == DeviceType.Mobile && _touchListener.controllerType == ControllerType.Joystick))
            .Subscribe(_ => JoyStickMove())
            .AddTo(this);

        // Grounded 상태 체크
        Observable.EveryUpdate()
            .Select(_ => _characterController.isGrounded) //isGrounded 값으로 교체
            //.DistinctUntilChanged() //값에 변화가 있는 순간만 통과
            //.ThrottleFrame(5) // 곡면, 경사각을 이동하게 되면 isGrounded의 True/False가 격렬하게 변화하기 때문에 값이 5프레임간 오지 않으면 최후의 값을 보냄
            .Subscribe(isGrounded => //isGrounded가 6프레임 이상 안정된 때에 최후의 값을 통지한다.
            {
                // Update grounded status
                _isGrounded.Value = isGrounded;
                FallingAndLanding(isGrounded); 
            })
            .AddTo(this);

        // Jump 버튼 입력 처리
        Observable.EveryUpdate()
            .Where(_ => Jump.Value > 0 && _isGrounded.Value) //_isGrounded.Value 체크 안해주면 무한점프 가능
            .Subscribe(_ =>
            {
                // 점프 높이에 따라 필요한 초기 속도를 계산하여 플레이어가 지정된 높이까지 도달할 수 있게 함
                _verticalSpeed = Mathf.Sqrt(2 * -_gravity * _jumpHeight);
                _playerVelocity.y = _verticalSpeed;
            })
            .AddTo(this);

    }

    private void FallingAndLanding(bool isGrounded)
    {
        bool isFalling = _verticalSpeed <= 0.0f; //버튼 프레스 조건 or 조건으로 추가
        float falMultiplier = 2.0f; // 낙하 속도를 증가시키기 위한 배율

        if (isGrounded) //캐릭터가 지면에 있을 때 실행
        {
            _playerVelocity.y = _verticalSpeed += Physics.gravity.y * Time.deltaTime;
        }
        else if (isFalling) //캐릭터가 낙하 중일 때 실행
        {
            float previousYVelocity = _verticalSpeed;
            _verticalSpeed = _verticalSpeed + (_gravity * falMultiplier * Time.deltaTime);
            _playerVelocity.y = Mathf.Max((previousYVelocity + _verticalSpeed) * 0.5f, -20.0f);
        }
        else // Landing (캐릭터가 상승 중일 때 실행)
        {
            float previousYVelocity = _verticalSpeed;
            _verticalSpeed = _verticalSpeed + (_gravity * Time.deltaTime);
            _playerVelocity.y = (previousYVelocity + _verticalSpeed) * 0.5f;
        }

        // 캐릭터 수직 이동
        _characterController.Move(_playerVelocity * Time.deltaTime);
    }


    private void JoyStickMove()
    {
        _moveDir = new Vector3(MoveX.Value, 0, MoveY.Value);

        if (_moveDir != Vector3.zero) //조이스틱을 움직였음
        {
            _moveDir = _cameraTransform.TransformDirection(_moveDir);
            _moveDir.y = 0;
            _characterController.Move(_moveDir * _moveSpeed * Time.deltaTime);
            transform.forward = _moveDir; // 안해주면 조이스틱 놨을때 정방향 보면서 돌아감
        }
    }

    // 첫 입력이 UI를 클릭하는 게 아닐 때에만 이동
    private void CheckMouseStartPosition()
    {
        if (_touchListener.PriContact.Value > 0)
        {
            if (!_isMouseDown)
            {
                // 마우스 클릭이 시작되는 순간
                _isInitialClickOnUI = _touchListener.isPriPositionOnUI.Value;
                _isMouseDown = true;
            }

            // 마우스가 눌려있는 동안
            if (!_isInitialClickOnUI)
                MouseMove();
        }
        else
            // 마우스 클릭이 해제된 순간
            _isMouseDown = false;
    }

    private void MouseMove()
    {
        // TODO : 마우스 먼저 찍어서 이동 중에 점프 가능하도록 하기, 점프 중에 마우스 클릭으로 이동 금지
        // isGrounded조건이 빡빡한데....
        //if (!_isGrounded.Value)
        //    return;

        Vector3 mousePosition = new Vector3(_touchListener.PriPositionX.Value, _touchListener.PriPositionY.Value);
        Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray: ray, hitInfo: out RaycastHit hit, maxDistance: 1000.0f,
            layerMask: SetTargetableLayerMask("Ground")))
        {
            _targetPosition = hit.point;
        
            if (_mouseMoveCo != null)
                _mouseMoveCo.Dispose();

            _mouseMoveCo = Observable.FromCoroutine(() => PlayerMoveTowards(_targetPosition)).Subscribe();
        }

        if (_moveDir != Vector3.zero)
            transform.forward = _moveDir;
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
            //Character Controller
            var direction = targetPoint - transform.position;
            _moveDir = direction.normalized;
            _characterController.Move(_moveDir * _moveSpeed * Time.deltaTime);

            // 미세하게 캐릭터 컨트롤러가 떨리는 현상을 보정
            var isStop =
                (direction.x >= -0.1f && direction.x <= 0.1f) &&
                (direction.z >= -0.1f && direction.z <= 0.1f);

            if (isStop)
                yield break;

            yield return null;
        }
    }

    private int SetTargetableLayerMask(string cullingMaskName)
    {
        int layerMask = LayerMask.NameToLayer(cullingMaskName);
        layerMask = 1 << layerMask;
        return layerMask;
    }
}
