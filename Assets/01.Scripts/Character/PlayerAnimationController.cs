using UnityEngine;

namespace MySecondWorld.Move
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [SerializeField] private MovementController _movementController;
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _jumpStartClip; // JumpStart 클립 길이를 MovementController에 주입
        [SerializeField] private AnimationClip _landClip;      // Land 클립 길이를 MovementController에 주입

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


        private void Awake()
        {
            if (_animator == null)
            {
                if (!TryGetComponent(out _animator))
                    Debug.LogError($"Animator missing on {gameObject.name}", this);
            }

            if (_movementController == null)
            {
                _movementController = GetComponentInParent<MovementController>();
                if (_movementController == null)
                    Debug.LogError($"MovementController not found in parent hierarchy of {gameObject.name}", this);
            }
        }

        private void Start()
        {
            if (_movementController != null && _jumpStartClip != null)
                _movementController.JumpStartDuration = _jumpStartClip.length;
            if (_movementController != null && _landClip != null)
                _movementController.LandDuration = _landClip.length;
        }

        private void OnEnable()
        {
            if (_movementController == null)
                return;

            _movementController.OnJump += HandleJump;
            _movementController.OnFall += HandleFall;
            _movementController.OnLand += HandleLand;
            _movementController.OnSpeedChanged += HandleSpeedChanged;
        }

        private void OnDisable()
        {
            if (_movementController == null)
                return;

            _movementController.OnJump -= HandleJump;
            _movementController.OnFall -= HandleFall;
            _movementController.OnLand -= HandleLand;
            _movementController.OnSpeedChanged -= HandleSpeedChanged;
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
            Debug.Log("HandleLand 호출");
            _animator.ResetTrigger(FallTrigger);
            _animator.ResetTrigger(JumpTrigger);
            _animator.SetTrigger(LandTrigger);
        }
    }
}