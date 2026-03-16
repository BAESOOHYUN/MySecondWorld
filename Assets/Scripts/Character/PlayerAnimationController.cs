using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private MovementController _movementController;
    [SerializeField] private Animator _animator;

    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int FallTriggerHash = Animator.StringToHash("FallTrigger");
    private static readonly int LandTriggerHash = Animator.StringToHash("LandTrigger");

    private void Awake()
    {
        if (_animator == null)
        {
            if (!TryGetComponent(out _animator))
                Debug.LogError($"Animator missing on {gameObject.name}", this);
        }

        if (_movementController == null)
        {
            if (!TryGetComponent(out _movementController))
                Debug.LogError($"MovementController not found on {gameObject.name}", this);
        }
    }

    private void OnEnable()
    {
        if (_movementController == null) return;

        _movementController.OnJump += HandleJump;
        _movementController.OnFall += HandleFall;
        _movementController.OnLand += HandleLand;
    }

    private void OnDisable()
    {
        if (_movementController == null) return;

        _movementController.OnJump -= HandleJump;
        _movementController.OnFall -= HandleFall;
        _movementController.OnLand -= HandleLand;
    }

    private void HandleJump()
    {
        _animator.SetTrigger(JumpTriggerHash);
    }

    private void HandleFall()
    {
        _animator.SetTrigger(FallTriggerHash);
    }

    private void HandleLand()
    {
        _animator.SetTrigger(LandTriggerHash);
    }
}
