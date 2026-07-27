using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using MySecondWorld.Network;
using MySecondWorld.Move;

namespace MySecondWorld.Combat
{
    // 입력 발생 → 쿨다운 체크 → 애니메이션 트리거 → (애니메이션의 타격 프레임에서) 범위 안 후보 탐지 → SkillContext 구성 → SkillExecutor.Execute()
    public class PlayerAttackController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private NetworkWeaponEquip _weaponEquip;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private InputActionReference _attackAction;

        [Header("판정")]
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private LayerMask _monsterLayer;

        [Header("쿨다운")]
        [SerializeField] private float _attackCooldown = 0.8f;
        private float _cooldownTimer;


        private SkillExecutor _skillExecutor;

        private void Awake()
        {
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() }
                );
        }

        private void OnEnable()
        {
            if (_weaponEquip == null)
            {
                Debug.LogError($"NetworkWeaponEquip missing on {this.gameObject.name}", this);
                return;
            }

            if (!_weaponEquip.isLocalPlayer)
                return; // 원격 플레이어 — 내 입력에 반응하면 안 됨

            _attackAction.action.Enable();
            _attackAction.action.performed += OnAttackInput;
        }

        private void OnDisable()
        {
            _attackAction.action.performed -= OnAttackInput;
        }

        private void OnAttackInput(InputAction.CallbackContext ctx)
        {
            if (_cooldownTimer > 0f) return;
            _cooldownTimer = _attackCooldown;
            _animationController.HandleAttack();
        }

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        private void OnAttackHit()
        {
            var candidates = Physics.OverlapSphere(_attackPoint.position, _attackRadius, _monsterLayer) // 공격 지점 반경 안의 몬스터 레이어 콜라이더 전부 탐지
                .OrderBy(c => Vector3.Distance(_attackPoint.position, c.transform.position)) // 가까운 순 정렬 (SingleTargetSelector가 첫 번째를 "가장 가까운 대상"으로 씀)
                .Select(c => c.GetComponentInParent<IDamageable>()) // 콜라이더 → 자신+조상 중 IDamageable 붙은 가장 가까운 오브젝트로 변환
                .Where(d => d != null)                              // IDamageable이 없는 대상(오탐) 제거
                .ToList();                                          // 지연 평가를 여기서 즉시 실행해 리스트로 확정 (Candidates가 인덱스 접근 필요)

            var ctx = new SkillContext(this.gameObject, candidates, _weaponEquip.GetMainHandAttack());
            _skillExecutor.Execute(ctx).Forget();
        }
    }
}