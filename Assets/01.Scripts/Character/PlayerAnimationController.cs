using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using MessagePipe;
using MySecondWorld.Network.Player;

namespace MySecondWorld.Move
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        private Mirror.NetworkAnimator _networkAnimator;
        // 클립 길이를 MovementController에 주입하기 위해 [SerializeField]
        [SerializeField] private AnimationClip _jumpStartClip;
        [SerializeField] private AnimationClip _landClip;
        private const string AttackTag = "Attack";

        // 공격 스테이트 재생 중인가.
        // Override Controller가 클립만 바꾸고 스테이트/태그는 베이스 것을 쓰므로 무기 5종에 모두 통한다.
        // 상체 전용 레이어(Layer 1, UpperBody)가 활성화되어 있는 경우 해당 레이어의 태그도 함께 검사한다.
        public bool IsAttacking =>
            _animator != null && (
                _animator.GetCurrentAnimatorStateInfo(0).IsTag(AttackTag) ||
                (_animator.layerCount > 1 && _animator.GetCurrentAnimatorStateInfo(1).IsTag(AttackTag))
            );

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitStatics()
        {
            JumpTrigger = Animator.StringToHash(nameof(JumpTrigger));
            FallTrigger = Animator.StringToHash(nameof(FallTrigger));
            LandTrigger = Animator.StringToHash(nameof(LandTrigger));
            Speed = Animator.StringToHash(nameof(Speed));
            AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));
            AimLoadTrigger = Animator.StringToHash(nameof(AimLoadTrigger));
            AimReleaseTrigger = Animator.StringToHash(nameof(AimReleaseTrigger));
        }

        private static int JumpTrigger = Animator.StringToHash(nameof(JumpTrigger));
        private static int FallTrigger = Animator.StringToHash(nameof(FallTrigger));
        private static int LandTrigger = Animator.StringToHash(nameof(LandTrigger));
        private static int Speed = Animator.StringToHash(nameof(Speed));
        private static int AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));
        private static int AimLoadTrigger = Animator.StringToHash(nameof(AimLoadTrigger));
        private static int AimReleaseTrigger = Animator.StringToHash(nameof(AimReleaseTrigger));

        private ISubscriber<PlayerJumpedEvent> _jumpSubscriber;
        private ISubscriber<PlayerFalledEvent> _fallSubscriber;
        private ISubscriber<PlayerLandedEvent> _landSubscriber;
        private ISubscriber<PlayerSpeedChangedEvent> _speedSubscriber;

        [Inject]
        private void Construct(
            ISubscriber<PlayerJumpedEvent> jumpSubscriber,
            ISubscriber<PlayerFalledEvent> fallSubscriber,
            ISubscriber<PlayerLandedEvent> landSubscriber,
            ISubscriber<PlayerSpeedChangedEvent> speedSubscriber)
        {
            _jumpSubscriber = jumpSubscriber;
            _fallSubscriber = fallSubscriber;
            _landSubscriber = landSubscriber;
            _speedSubscriber = speedSubscriber;
        }

        private void Awake()
        {
            if (_animator == null)
            {
                if (!TryGetComponent(out _animator))
                    Debug.LogError($"Animator missing on {gameObject.name}", this);
            }

            _networkAnimator = GetComponent<Mirror.NetworkAnimator>();
        }

        private void Start()
        {
            if (_jumpSubscriber == null)
            {
                // 원격 플레이어 — 구독 불필요
                return;
            }

            var ct = this.GetCancellationTokenOnDestroy();

            // 클립 길이 주입 — MovementController를 같은 프리팹에서 찾아서 전달
            var movementController = GetComponentInParent<MovementController>();

            if (movementController != null)
            { 
                if (_jumpStartClip != null)
                    movementController.JumpStartDuration = _jumpStartClip.length;
                if (_landClip != null)
                    movementController.LandDuration = _landClip.length;
            }

            _jumpSubscriber.Subscribe(_ => HandleJump()).AddTo(ct);
            _fallSubscriber.Subscribe(_ => HandleFall()).AddTo(ct);
            _landSubscriber.Subscribe(_ => HandleLand()).AddTo(ct);
            _speedSubscriber.Subscribe(evt => HandleSpeedChanged(evt.Speed)).AddTo(ct);
        }

        private void HandleSpeedChanged(float speed)
        {
            if (_animator == null)
                return;
            _animator.SetFloat(Speed, speed);
        }

        private void HandleJump()
        {
            if (_animator == null)
                return; 
            
            if (_networkAnimator != null)
            {
                _networkAnimator.ResetTrigger(FallTrigger);
                _networkAnimator.ResetTrigger(LandTrigger);
                _networkAnimator.SetTrigger(JumpTrigger);
            }
            else
            {
                _animator.ResetTrigger(FallTrigger);
                _animator.ResetTrigger(LandTrigger);
                _animator.SetTrigger(JumpTrigger);
            }
        }

        private void HandleFall()
        {
            if (_animator == null)
                return;

            if (_networkAnimator != null)
            {
                _networkAnimator.ResetTrigger(LandTrigger);
                _networkAnimator.ResetTrigger(JumpTrigger);
                _networkAnimator.SetTrigger(FallTrigger);
            }
            else
            {
                _animator.ResetTrigger(LandTrigger);
                _animator.ResetTrigger(JumpTrigger);
                _animator.SetTrigger(FallTrigger);
            }
        }

        private void HandleLand()
        {
            if (_animator == null)
                return;

            if (_networkAnimator != null)
            {
                _networkAnimator.ResetTrigger(FallTrigger);
                _networkAnimator.ResetTrigger(JumpTrigger);
                _networkAnimator.SetTrigger(LandTrigger);
            }
            else
            {
                _animator.ResetTrigger(FallTrigger);
                _animator.ResetTrigger(JumpTrigger);
                _animator.SetTrigger(LandTrigger);
            }
        }

        // PlayerAttackController가 입력 발생 시 직접 호출 (MessagePipe 이벤트 구독 아님 — 이번 슬라이스는 DI/이벤트 생략)
        public void HandleAttack()
        {
            if (_animator == null)
                return;

            if (_networkAnimator != null) 
                _networkAnimator.SetTrigger(AttackTrigger);
            else 
                _animator.SetTrigger(AttackTrigger);
        }

        // 홀드 조준 — 누르는 순간. Load 재생 시작, 끝나면 애니메이터가 자동으로 Hold 루프로 넘어간다.
        public void HandleAimLoad()
        {
            if (_animator == null) return;

            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(AimLoadTrigger);
            else
                _animator.SetTrigger(AimLoadTrigger);
        }

        // 홀드 조준 — 손을 뗀 순간. Load/Hold 어느 단계에서 와도 Any State 전환으로 즉시 끊고 넘어간다.
        public void HandleAimRelease()
        {
            if (_animator == null) return;

            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(AimReleaseTrigger);
            else
                _animator.SetTrigger(AimReleaseTrigger);
        }

        // 조준 취소 (사망, 무기 교체, 캔슬 등) — 남아있는 조준 및 공격 트리거를 깨끗이 청소한다.
        public void HandleAimCancel()
        {
            if (_animator == null) return;

            if (_networkAnimator != null)
            {
                _networkAnimator.ResetTrigger(AimLoadTrigger);
                _networkAnimator.ResetTrigger(AimReleaseTrigger);
                _networkAnimator.ResetTrigger(AttackTrigger);
            }
            else
            {
                _animator.ResetTrigger(AimLoadTrigger);
                _animator.ResetTrigger(AimReleaseTrigger);
                _animator.ResetTrigger(AttackTrigger);
            }

            // 상체 레이어(Layer 1, UpperBody)가 활성화되어 있다면 즉시 Empty(기본) 상태로 복귀하여 굳음 방지
            if (_animator.layerCount > 1)
            {
                _animator.CrossFadeInFixedTime("Empty", 0.1f, 1);
            }
        }
    }
}