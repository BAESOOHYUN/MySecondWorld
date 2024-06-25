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

    private Camera _mainCamera;
    private Transform _cameraTransform;
    private Vector3 _moveVec; // 플레이어 방향
    private Vector3 _playerVelocity; // 속도
    private float _gravity = -9.81f; // 중력
    private bool _isGround;
    private Vector3 _targetPosition;
    private Coroutine _mouseMoveCo;
    private bool _isInitialClickOnUI = false;
    private bool _isMouseDown = false;

    public InputAxis MoveX = InputAxis.DefaultMomentary;
    public InputAxis MoveY = InputAxis.DefaultMomentary;
    public InputAxis Jump = InputAxis.DefaultMomentary;
    public InputAxis Attack = InputAxis.DefaultMomentary;


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

    private void Update()
    {
        _isGround = _characterController.isGrounded;
        if (_isGround && _playerVelocity.y < 0)
            _playerVelocity.y = 0f;

        // TODO : 컨트롤러가 너무 뻑뻑하게 움직여서 렉걸린거 같음. 스무스하게 움직이도록 하기.
        // TODO : 마비노기처럼 PC에서 조이스틱과 마우스 이동 둘 다 되게 하려면?
        if (_touchListener.deviceType == DeviceType.PC && _touchListener.controllerType == ControllerType.Mouse)
            CheckMouseStartPosition();
        else if ((_touchListener.deviceType == DeviceType.PC && _touchListener.controllerType == ControllerType.KeyBoard)
               || (_touchListener.deviceType == DeviceType.Mobile && _touchListener.controllerType == ControllerType.Joystick))
            JoyStickMove();

        Jumpping();
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

        if (_moveVec != Vector3.zero) //조이스틱을 움직였음
        {
            _moveVec = _cameraTransform.TransformDirection(_moveVec);
            _moveVec.y = 0;
            _characterController.Move(_moveVec * _moveSpeed * Time.deltaTime);
            transform.forward = _moveVec; // 안해주면 조이스틱 놨을때 정방향 보면서 돌아감
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
                _isInitialClickOnUI = _touchListener.isPriPositionOnUI;
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
        Vector3 mousePosition = new Vector3(_touchListener.PriPositionX.Value, _touchListener.PriPositionY.Value);
        Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray: ray, hitInfo: out RaycastHit hit, maxDistance: 1000.0f,
            layerMask: SetTargetableLayerMask("Ground")))
        {
            _targetPosition = hit.point;

            if (_mouseMoveCo != null)
                StopCoroutine(_mouseMoveCo);
            _mouseMoveCo = StartCoroutine(PlayerMoveTowards(_targetPosition));
        }

        if (_moveVec != Vector3.zero)
            transform.forward = _moveVec;
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
            _moveVec = direction.normalized;
            _characterController.Move(_moveVec * _moveSpeed * Time.deltaTime);

            // 미세하게 캐릭터 컨트롤러가 떨리는 현상을 보정
            var isStop =
                (direction.x >= -0.1f && direction.x <= 0.1f) &&
                (direction.z >= -0.1f && direction.z <= 0.1f);

            if (isStop)
                yield break;

            yield return null;
        }
    }

    private void Jumpping()
    {
        // TODO : 마우스 먼저 찍어서 이동 중에 점프 가능하도록 하기, 점프 중에 마우스 클릭으로 이동 금지
        if (Jump.Value > 0 && _isGround)
            _playerVelocity.y += Mathf.Sqrt(_jumpHeight * _jumpSpeed * -_gravity);
    }

    private void Gravity()
    {
        _playerVelocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_playerVelocity * Time.deltaTime);
    }

    private int SetTargetableLayerMask(string cullingMaskName)
    {
        int layerMask = LayerMask.NameToLayer(cullingMaskName);
        layerMask = 1 << layerMask;
        return layerMask;
    }
}
