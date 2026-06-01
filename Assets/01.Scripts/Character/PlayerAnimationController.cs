using UnityEngine;
using VContainer;
using MessagePipe;
using MySecondWorld.Network.Player;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Move
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        // 클립 길이를 MovementController에 주입하기 위해 [SerializeField]
        [SerializeField] private AnimationClip _jumpStartClip;
        [SerializeField] private AnimationClip _landClip;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitStatics()
        {
            JumpTrigger = Animator.StringToHash(nameof(JumpTrigger));
            FallTrigger = Animator.StringToHash(nameof(FallTrigger));
            LandTrigger = Animator.StringToHash(nameof(LandTrigger));
            Speed = Animator.StringToHash(nameof(Speed));
        }

        private static int JumpTrigger = Animator.StringToHash(nameof(JumpTrigger));
        private static int FallTrigger = Animator.StringToHash(nameof(FallTrigger));
        private static int LandTrigger = Animator.StringToHash(nameof(LandTrigger));
        private static int Speed = Animator.StringToHash(nameof(Speed));

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
            
            _animator.ResetTrigger(FallTrigger);
            _animator.ResetTrigger(LandTrigger);
            _animator.SetTrigger(JumpTrigger);
        }

        private void HandleFall()
        {
            if (_animator == null)
                return;

            _animator.ResetTrigger(LandTrigger);
            _animator.ResetTrigger(JumpTrigger);
            _animator.SetTrigger(FallTrigger);
        }

        private void HandleLand()
        {
            if (_animator == null)
                return;

            _animator.ResetTrigger(FallTrigger);
            _animator.ResetTrigger(JumpTrigger);
            _animator.SetTrigger(LandTrigger);
        }
    }
}