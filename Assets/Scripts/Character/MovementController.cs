using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour
{
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private CharacterController _characterController;
    //private AnimatorController _animatorController;
    private Camera _mainCamera;
    private Transform _cameraTransform;
    private Vector3 _moveVec; // 플레이어 방향
    [SerializeField] private float _moveSpeed = 3.0f; // 속력
    //[SerializeField] private float _rotationSpeed = 4.0f; //회전 속력
    [SerializeField] private float _jumpSpeed = 3.0f; //점프 속력
    [SerializeField] private float _jumpHeight = 1.0f; //점프 높이
    private Vector3 _playerVelocity; // 속도
    private float _gravity = -9.81f; // 중력
    private bool _isGround;
    private Vector3 _targetPosition;
    private Coroutine _mouseMoveCo;

    public bool IsMove
    {
        get
        {
            return (_inputManager != null) ? _inputManager.isMove : false;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_targetPosition, 0.5f);
    }

    private void Update()
    {
        _isGround = _characterController.isGrounded;
        if (_isGround && _playerVelocity.y < 0)
            _playerVelocity.y = 0f;

        // TODO : 컨트롤러가 너무 뻑뻑하게 움직여서 렉걸린거 같음. 스무스하게 움직이도록 하기.
        if (_inputManager?.controlType == ControlType.Mouse)
        {
            ControlTypeMouse();
        }
        else if (_inputManager?.controlType == ControlType.Joystick || _inputManager?.controlType == ControlType.Keyboard)
        {
            ControlTypeJoystick();
        }

        Jump();
        Gravity();
    }

    public void OnEnable()
    {
        _mainCamera = Camera.main;
        _cameraTransform = _mainCamera.transform;
    }

    // TODO : UI위에서 시작한 터치가 UI밖에서 끝났을때 이동 버벅거리는 거 처리
    // InputDebug Simulated TouchScreen이 아닐때에만 버벅거림
    private void ControlTypeJoystick()
    {
        var joystickValue = _inputManager.MoveValue;
        _moveVec = new Vector3(joystickValue.x, 0, joystickValue.y);
        _moveVec = _cameraTransform.TransformDirection(_moveVec);
        _moveVec.y = 0;
        _characterController.Move( _moveVec * _moveSpeed * Time.deltaTime);

        if (_moveVec != Vector3.zero) // 안해주면 조이스틱 놨을때 정방향 보면서 돌아감
            transform.forward = _moveVec;
    }

    private void ControlTypeMouse()
    {
        if (_inputManager.LeftMouseButtonPhase == TouchPhase.Moved
            && !_inputManager.isMouseCursorOnUI) // UI를 터치하는게 아닐때만 이동
        {
            Vector3 mousePosition = _inputManager.Position;
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

    private void Jump()
    {
        // TODO : 마우스 먼저 찍어서 이동 중에 점프 가능하도록 하기, 점프 중에 마우스 클릭으로 이동 금지
        if (_inputManager.isJumped && _isGround)
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
