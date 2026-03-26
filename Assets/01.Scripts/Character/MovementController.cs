using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using MySecondWorld.Utils;

namespace MySecondWorld.Move
{
    [RequireComponent(typeof(CharacterController))]
    public class MovementController : MonoBehaviour, IInputAxisOwner
    {
        [SerializeField] private CharacterController _controller;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _jumpSpeed = 3f;
        [SerializeField] private float _jumpHeight = 1f;
        [SerializeField] private float _airControlMultiplier = 0.5f; // 공중 이동 감도

        public Func<float> GetCameraYAngle;  // 외부에서 주입 (NetworkPlayerController)
        public event Action OnJump;
        public event Action OnFall;
        public event Action OnLand;
        public event Action<float> OnSpeedChanged;
        // 향후 착지 취소(공중 재점프 등) 시나리오를 위해 예약된 이벤트. 현재 미사용.
        public event Action OnLand_Cancel;

        public float InputMagnitude { get; private set; }
        public bool IsGrounded { get; private set; } = true;

        // Cinemachine IInputAxisOwner 인터페이스 요구사항으로 public 사용
        public InputAxis MoveX = InputAxis.DefaultMomentary;
        public InputAxis MoveY = InputAxis.DefaultMomentary;
        public InputAxis Jump = InputAxis.DefaultMomentary;
        public InputAxis Attack = InputAxis.DefaultMomentary;

        private Vector3 _inputDirection;
        private Vector3 _moveDirection; // 수평 이동 벡터 (JoyStickMove에서 계산, Gravity에서 사용)
        private Vector3 _velocity; // 현재 속도
        private const float Gravity = -9.81f;
        private const float FallMultiplier = 2.5f; // 낙하 시 추가 가속도
        private const float DeadZone = 0.01f;
        private const float GroundedVelocity = -2f;
        private bool _isFalling;
        private bool _wasFalling;
        private float _airTime;
        private int _groundedFrames = 1;
        private bool IsJustLanded => _groundedFrames == 2 && _airTime >= 0.1f;

        private void Awake()
        {
            if (_controller == null)
                TryGetComponent(out _controller);
            if (_controller == null)
                Debug.LogError($"CharacterController를 찾을 수 없습니다. ({gameObject.name})", this);
        }

        private void Update()
        {
            JoyStickMove();
            Jumping();
        }

        private void OnDrawGizmos() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(1f, 0f, 0f), 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(1f, 0f, 0f), 0.35f);

        public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
        {
            axes.Add(new() { DrivenAxis = () => ref MoveX, Name = nameof(MoveX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
            axes.Add(new() { DrivenAxis = () => ref MoveY, Name = nameof(MoveY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
            axes.Add(new() { DrivenAxis = () => ref Jump, Name = nameof(Jump) });
            axes.Add(new() { DrivenAxis = () => ref Attack, Name = nameof(Attack) });
        }

        private void JoyStickMove()
        {
            if (GetCameraYAngle == null)
                return;

            Vector3 rawInput = new Vector3(MoveX.Value, 0f, MoveY.Value);

            if (rawInput.sqrMagnitude < DeadZone)
            {
                _moveDirection = Vector3.zero;
                _inputDirection = Vector3.zero;
                OnSpeedChanged?.Invoke(0f);
                return;
            }

            OnSpeedChanged?.Invoke(rawInput.magnitude);
            _inputDirection = rawInput.normalized;

            // 카메라 Y축 회전만 추출해서 이동 방향 계산 (pitch 영향 없음)
            //float cameraAngle = _cinemachineCamera.transform.eulerAngles.y;
            float cameraAngle = GetCameraYAngle();
            float targetAngle = Mathf.Atan2(_inputDirection.x, _inputDirection.z) * Mathf.Rad2Deg + cameraAngle;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // 공중에서는 이동 감도 줄이기
            float speed = IsGrounded ? _moveSpeed : _moveSpeed * _airControlMultiplier;
            _moveDirection = moveDir * speed;

            // 부드러운 캐릭터 회전
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
        }

        private void Jumping()
        {
            _wasFalling = _isFalling;

            // 현재 프레임 지면 상태 갱신
            IsGrounded = _controller.isGrounded;
            if (IsGrounded)
            {
                if (_groundedFrames < 2)
                    _groundedFrames++;
            }
            else
            {
                _groundedFrames = 0;
                _airTime += Time.deltaTime;
            }

            // 매 프레임 살짝 아래로 눌러서 지면 감지
            // 지면에 붙어있을 때 중력이 계속 누적되는 걸 막음
            if (IsGrounded && _velocity.y < 0)
                _velocity.y = GroundedVelocity;

            // 점프 입력
            if (Jump.Value > 0 && IsGrounded)
            {
                _velocity.y = Mathf.Sqrt(_jumpHeight * _jumpSpeed * -Gravity);
                OnJump?.Invoke();
            }

            // 낙하 상태 갱신 및 이벤트 (진입 순간 1회)
            _isFalling = _velocity.y < 0 && !IsGrounded;
            if (_isFalling && !_wasFalling)
                OnFall?.Invoke();

            // 착지 순간
            if (IsJustLanded)
            {
                OnLand?.Invoke();
                _airTime = 0f;
            }

            // 중력 적용 (낙하 시 추가 가속)
            float currentGravity = _velocity.y < 0 ? Gravity * FallMultiplier : Gravity;
            ApplyGravity(currentGravity);
        }

        private void ApplyGravity(float gravity)
        {
            _velocity.y += gravity * Time.deltaTime;
            // 수평 + 수직 합산 후 한 번만 Move 호출
            _controller.Move((_moveDirection + _velocity) * Time.deltaTime);
        }
    }
}