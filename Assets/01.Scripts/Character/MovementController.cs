using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using MessagePipe;
using VContainer;
using MySecondWorld.Utils;
using MySecondWorld.Network.Player;

namespace MySecondWorld.Move
{
    // TODO: OnLand_Cancel — 착지 취소(공중 재점프) 시나리오 구현 시 IPublisher<PlayerLandCancelledEvent> 추가
    
    [RequireComponent(typeof(CharacterController))]
    public class MovementController : MonoBehaviour, IInputAxisOwner
    {
        // Inspector
        [SerializeField] private CharacterController _controller;
        
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _airControlMultiplier = 0.5f; // 공중 이동 감도

        [Header("Jump")]
        [SerializeField] private float _jumpSpeed = 3f;
        [SerializeField] private float _jumpHeight = 1f;

        [Header("Ground")]
        [SerializeField] private float _groundCheckDistance = 0.1f; // 지면 감지 추가 거리
        [SerializeField] private LayerMask _groundLayer;             // 지면 레이어

        // Public API
        public Func<float> GetCameraYAngle;  // NetworkPlayerController에서 Authority(권위) 시점에 주입
        public float InputMagnitude { get; private set; }
        public bool IsGrounded { get; private set; } = true;
        public float JumpStartDuration { get; set; } // PlayerAnimationController에서 클립 길이 주입
        public float LandDuration { get; set; }       // PlayerAnimationController에서 클립 길이 주입
        
        // 공격 회차 중 회전 잠금. Combat 쪽에서 켜고 끈다.
        // Character는 Combat을 참조하지 않으므로(순환 방지) 플래그를 밖에서 밀어넣는 구조다.
        public bool RotationLocked { get; set; }

        // Private State
        private Vector3 _inputDirection;
        private Vector3 _moveDirection; // 수평 이동 벡터 (JoyStickMove에서 계산, Gravity에서 사용)
        private Vector3 _velocity; // 현재 속도

        private const float Gravity = -9.81f;
        private const float FallMultiplier = 2.5f; // 낙하 시 추가 가속도
        private const float DeadZone = 0.04f;  // magnitude 기준 0.2 이하 무시
        private const float GroundedVelocity = -2f;

        private float _jumpStartTimer; // JumpStart 클립 길이만큼 OnFall 발화 지연
        private float _landingTimer;   // Land 클립 길이만큼 이동 차단
        private bool _isFalling;
        private bool _wasFalling;
        private float _airTime;
        private int _groundedFrames = 1;

        private bool IsJustLanded => _groundedFrames >= 1 && _airTime >= 0.1f;

        // Cinemachine IInputAxisOwner (인터페이스 요구사항으로 public 사용)
        public InputAxis MoveX = InputAxis.DefaultMomentary;
        public InputAxis MoveY = InputAxis.DefaultMomentary;
        public InputAxis Jump = InputAxis.DefaultMomentary;

        // DI
        private IPublisher<PlayerJumpedEvent> _jumpPublisher;
        private IPublisher<PlayerFalledEvent> _fallPublisher;
        private IPublisher<PlayerLandedEvent> _landPublisher;
        private IPublisher<PlayerSpeedChangedEvent> _speedPublisher;
        [Inject]
        private void Construct(
            IPublisher<PlayerJumpedEvent> jumpPublisher,
            IPublisher<PlayerFalledEvent> fallPublisher,
            IPublisher<PlayerLandedEvent> landPublisher,
            IPublisher<PlayerSpeedChangedEvent> speedPublisher)
        {
            _jumpPublisher = jumpPublisher;
            _fallPublisher = fallPublisher;
            _landPublisher = landPublisher;
            _speedPublisher = speedPublisher;
        }

        private void Awake()
        {
            if (_controller == null)
                TryGetComponent(out _controller);
            if (_controller == null)
                Debug.LogError($"CharacterController를 찾을 수 없습니다. ({gameObject.name})", this);
        }

        private void Update()
        {
            //지면 상태(IsGrounded)를 먼저 확정한 뒤
            //이동 속도(_moveSpeed vs _moveSpeed * _airControlMultiplier)를 결정
            Jumping();
            JoyStickMove();
        }

        private void OnDrawGizmos() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(1f, 0f, 0f), 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(1f, 0f, 0f), 0.35f);

        public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
        {
            axes.Add(new() { DrivenAxis = () => ref MoveX, Name = nameof(MoveX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
            axes.Add(new() { DrivenAxis = () => ref MoveY, Name = nameof(MoveY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
            axes.Add(new() { DrivenAxis = () => ref Jump, Name = nameof(Jump) });
        }

        private void JoyStickMove()
        {
            if (OnUIChecker.IsInputFieldFocused()) return;
            if (GetCameraYAngle == null)
            {
                // 카메라 없어도 Speed 0 발행해서 모션 고착 방지
                _moveDirection = Vector3.zero;
                _speedPublisher?.Publish(new PlayerSpeedChangedEvent { Speed = 0 });
                return;
            }

            Vector3 rawInput = new Vector3(MoveX.Value, 0f, MoveY.Value);

            // "조이스틱 입력이 거의 없거나" OR "방금 착지해서 경직 중"
            if (rawInput.sqrMagnitude < DeadZone || _landingTimer > 0)
            {
                // 이동 방향·속도를 0으로 초기화하고 함수 종료
                _moveDirection = Vector3.zero;
                _inputDirection = Vector3.zero;
                _speedPublisher.Publish(new PlayerSpeedChangedEvent { Speed = 0 });
                return;
            }

            // 지상일 때만 Speed 발행, 공중은 0으로 강제 (점프/낙하 모션 우선)
            float animSpeed = IsGrounded ? rawInput.magnitude : 0f;
            _speedPublisher.Publish(new PlayerSpeedChangedEvent { Speed = animSpeed });
            _inputDirection = rawInput.normalized;

            // 카메라 Y축 회전만 추출해서 이동 방향 계산 (pitch 영향 없음)
            float cameraAngle = GetCameraYAngle();
            float targetAngle = Mathf.Atan2(_inputDirection.x, _inputDirection.z) * Mathf.Rad2Deg + cameraAngle;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // 공중에서는 이동 감도 줄이기
            float speed = IsGrounded ? _moveSpeed : _moveSpeed * _airControlMultiplier;
            _moveDirection = moveDir * speed;

            // 부드러운 캐릭터 회전
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            // Combat 쪽에서 유도. 궁수 전투중 이동시 회전 방향 안틀어지게.
            if (!RotationLocked)
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
        }

        //CharacterController.isgrounded로 지면 탐지가 불안정. SphereCast, _groundLayer로 지면 탐지.
        private bool CheckGrounded()
        {
            Vector3 origin = transform.position + _controller.center;
            float radius = _controller.radius - _controller.skinWidth;
            float distance = (_controller.height / 2f) - radius + _groundCheckDistance;
            return Physics.SphereCast(origin, radius, Vector3.down, out _, distance, _groundLayer);
        }

        private void Jumping()
        {
            _wasFalling = _isFalling;

            // 현재 프레임 지면 상태 갱신
            IsGrounded = CheckGrounded();
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
                _jumpStartTimer = JumpStartDuration;
                _jumpPublisher.Publish(new PlayerJumpedEvent());
            }

            // JumpStart 클립이 끝난 후에만 FallTrigger 발화 (조기 발화 방지)
            if (_jumpStartTimer > 0)
                _jumpStartTimer -= Time.deltaTime;

            // 낙하 상태 갱신 및 이벤트 (진입 순간 1회)
            _isFalling = _velocity.y < 0 && !IsGrounded && _jumpStartTimer <= 0;
            if (_isFalling && !_wasFalling)
                _fallPublisher.Publish(new PlayerFalledEvent());

            // 착지 순간
            if (IsJustLanded)
            {
                _landingTimer = LandDuration;
                _landPublisher?.Publish(new PlayerLandedEvent());
                _airTime = 0f;
            }

            if (_landingTimer > 0)
                _landingTimer -= Time.deltaTime;

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