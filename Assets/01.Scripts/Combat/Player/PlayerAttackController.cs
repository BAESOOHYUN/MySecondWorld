using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using MySecondWorld.Network;
using MySecondWorld.Move;

namespace MySecondWorld.Combat.Player
{
    // 입력 발생 → 쿨다운 체크 → 애니메이션 트리거 → (애니메이션의 타격/발사 프레임에서) 판정
    public class PlayerAttackController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private NetworkWeaponEquip _weaponEquip;
        [SerializeField] private PlayerAttackAuthority _attackAuthority;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private InputActionReference _attackAction;
        [SerializeField] private PlayerCombatStatsSO _stats;

        [Header("근접 판정")]
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private LayerMask _targetLayerMask;

        [Header("원거리 판정")]
        [SerializeField] private Transform _projectileSpawnPoint; // 조준 대상 탐색 기준점(대략 손 근처) — 실제 화살은 NetworkWeaponEquip이 들고 있는 걸 그대로 씀
        
        private float _nextAttackTime;
        private float _lastAttackTime = float.NegativeInfinity;

        public bool IsInCombat => Time.time - _lastAttackTime < _stats.CombatStateDuration;

        #region Unity 생명주기

        private void Awake()
        {
        }

        private void OnEnable()
        {
            if (_weaponEquip == null || _animationController == null ||
                _attackAuthority == null || _attackAction == null || _attackPoint == null ||
                _projectileSpawnPoint == null || _stats == null)
            {
                Debug.LogError($"PlayerAttackController 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _attackAction.action.Enable();
            _attackAction.action.performed += OnAttackInput;
        }

        private void OnDisable()
        {
            if (_attackAction != null)
                _attackAction.action.performed -= OnAttackInput;
        }

        private void OnDestroy()
        {
        }

        #endregion

        #region 입력 처리

        private void OnAttackInput(InputAction.CallbackContext ctx)
        {
            if (!_weaponEquip.isLocalPlayer)
                return; // 원격 플레이어 — 내 입력에 반응하면 안 됨

            if (Time.time < _nextAttackTime) return;

            _nextAttackTime = Time.time + _stats.AttackCooldown;
            _lastAttackTime = Time.time;
            _animationController.HandleAttack();
        }

        #endregion

        #region 공격 판정

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
            if (!_weaponEquip.isLocalPlayer)
                return;

            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f)
                power = _stats.UnarmedAttack;

            _attackAuthority.CmdResolveAttackHit(_attackPoint.position, _stats.AttackRadius, _stats.UnarmedAttack);
        }

        // Phase 2(클릭 지속 타겟팅) 도입 전까지의 임시 조준 로직 —
        // 발사 순간 _shootRange 반경 내 가장 가까운 몬스터를 즉석으로 찾아 조준점으로 삼는다.
        // 대상이 없으면 캐릭터 정면으로 발사(빗나감 확정이지만 애니메이션은 자연스럽게 끝남).
        private Vector3 FindAimPoint()
        {
            var nearest = Physics.OverlapSphere(_projectileSpawnPoint.position, _stats.ShootRange, _targetLayerMask, QueryTriggerInteraction.Ignore)
                .OrderBy(c => Vector3.Distance(_projectileSpawnPoint.position, c.transform.position))
                .FirstOrDefault();

            if (nearest == null)
                return _projectileSpawnPoint.position + transform.forward * _stats.ShootRange;

            return nearest.transform.position + Vector3.up * _stats.AimHeightOffset;
        }
        #endregion
    }
}