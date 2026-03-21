using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private MovementController _movementController;
    [SerializeField] private Animator _animator;

    private static readonly int JumpTrigger = Animator.StringToHash(nameof(JumpTrigger));
    private static readonly int FallTrigger = Animator.StringToHash(nameof(FallTrigger));
    private static readonly int LandTrigger = Animator.StringToHash(nameof(LandTrigger));
    private static readonly int Speed = Animator.StringToHash(nameof(Speed));

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
        _animator.SetTrigger(JumpTrigger);
    }

    private void HandleFall()
    {
        if (_animator == null)
            return;
        _animator.SetTrigger(FallTrigger);
    }

    private void HandleLand()
    {
        if (_animator == null)
            return;
        _animator.SetTrigger(LandTrigger);
    }
}
