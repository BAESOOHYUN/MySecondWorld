using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using MySecondWorld.Network;
using MySecondWorld.Move;
using MySecondWorld.Inventory;

namespace MySecondWorld.Combat.Player
{
    // 입력 발생 → 쿨다운 체크 → 애니메이션 트리거 → (애니메이션의 타격 프레임에서) 범위 안 후보 탐지 → SkillContext 구성 → SkillExecutor.Execute()
    public class PlayerAttackController : MonoBehaviour
    {
        #region Fields

        [Header("참조")]
        [SerializeField] private NetworkWeaponEquip _weaponEquip;
        [SerializeField] private PlayerAttackAuthority _attackAuthority;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private InputActionReference _attackAction;

        [Header("판정")]
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private float _unarmedAttack = 10f; // 무기 미장착(맨주먹) 시 기본 공격력
        [SerializeField] private LayerMask _targetLayerMask;

        [Header("원거리 판정 (투사체 — Bow/Staff 등 공용)")]
        [SerializeField] private Transform _projectileSpawnPoint; // 투사체가 실제로 발사되는 지점(무기별로 다름 — Bow는 오른손 소켓 하위 활 시위 위치)
        [SerializeField] private GameObject _projectilePrefab; // 순수 프리팹(스크립트 없음) — ProjectileFactory가 스폰 시점에 Projectile을 붙임
        [SerializeField] private float _projectileSpeed = 20f;
        [SerializeField] private Vector3 _projectileColliderCenter;
        [SerializeField] private float _projectileColliderRadius = 0.15f;
        [SerializeField] private float _shootRange = 15f; // 조준 대상을 즉석 탐색하는 반경(Phase 2 이전 임시 로직)
        [SerializeField] private float _aimHeightOffset = 1.4f; // RangedMonsterHitbox._aimHeightOffset과 동일한 보정

        [Header("전투 상태")]
        [SerializeField] private float _combatStateDuration = 3f; // 마지막 공격 후 이 시간 동안 "전투 중"으로 간주
        private float _lastAttackTime = float.NegativeInfinity;
        public bool IsInCombat => Time.time - _lastAttackTime < _combatStateDuration;

        [Header("쿨다운")]
        [SerializeField] private float _attackCooldown = 0.8f;

        private SkillExecutor _skillExecutor;
        private SkillExecutor _rangedSkillExecutor;
        private float _nextAttackTime;

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() }
                );

            // 데미지 없는 비주얼(예측/연출) 전용 — 실제 데미지 확정은 서버 Command(CmdResolveShootHit)가 담당한다.
            // effects를 빈 리스트로 둬서, 클라이언트가 로컬에서 뭔가를 맞히더라도 TakeDamage가 절대 호출되지 않는다.
            _rangedSkillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new ProjectileCast(_projectilePrefab, _projectileSpawnPoint, _projectileSpeed, 
                                _projectileColliderCenter, _projectileColliderRadius, LayerMask.GetMask("Monster")),
                new List<ISkillEffect>()
                );
        }

        private void OnEnable()
        {
            if (_weaponEquip == null || _animationController == null ||
                _attackAuthority == null || _attackAction == null || _attackPoint == null ||
                _projectileSpawnPoint == null || _projectilePrefab == null)
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
            _skillExecutor?.Dispose();
            _rangedSkillExecutor?.Dispose();
        }

        #endregion

        #region 입력 처리

        private void OnAttackInput(InputAction.CallbackContext ctx)
        {
            if (!_weaponEquip.isLocalPlayer)
                return; // 원격 플레이어 — 내 입력에 반응하면 안 됨

            if (Time.time < _nextAttackTime) return;

            _nextAttackTime = Time.time + _attackCooldown;
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

            // 무기 미장착 시 맨주먹 공격력 사용
            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f)
                power = _unarmedAttack;

            _attackAuthority.CmdResolveAttackHit(_attackPoint.position, _attackRadius, _unarmedAttack);
        }

        // 투사체 발사 애니메이션(현재는 Bow만 존재 — Archer@BowShot01)의 발사 프레임에 Animation Event로 연결.
        // RangedMonsterHitbox.OnShootRelease()와 대칭 구조. 향후 Staff 등 다른 원거리 무기가 추가되면
        // 아래 카테고리 체크만 확장하면 된다(WeaponCategory.cs 기준 현재 MainHand 허용 목록엔 Bow만 원거리).
        public void OnShootRelease()
        {
            if (!_weaponEquip.isLocalPlayer)
                return;

            // 무기 카테고리가 바뀐 뒤 재생 중이던 다른 클립의 잔재 이벤트가 잘못 걸려 호출되는 경우 방어
            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow)
                return;

            if (_rangedSkillExecutor == null)
                return;

            var aimPoint = FindAimPoint();
            var direction = (aimPoint - _projectileSpawnPoint.position).normalized;

            // 실제 데미지 확정 — 서버 권위(PlayerAttackAuthority.CmdResolveShootHit).
            // 서버가 같은 방향/반경으로 재검증하므로, 여기서 보내는 값은 어디까지나 "참고 입력"일 뿐이다.
            _attackAuthority.CmdResolveShootHit(_projectileSpawnPoint.position, direction, _shootRange, _projectileColliderRadius, _unarmedAttack);

            // 클라 로컬 투사체 비주얼(예측/연출 전용) — _rangedSkillExecutor의 effects가 비어있어 데미지는 절대 적용되지 않는다.
            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f)
                power = _unarmedAttack;

            var ctx = new SkillContext(gameObject, Array.Empty<IDamageable>(), power, aimPoint);
            _rangedSkillExecutor.Execute(ctx).Forget();
        }

        // Phase 2(클릭 지속 타겟팅) 도입 전까지의 임시 조준 로직 —
        // 발사 순간 _shootRange 반경 내 가장 가까운 몬스터를 즉석으로 찾아 조준점으로 삼는다.
        // 대상이 없으면 캐릭터 정면으로 발사(빗나감 확정이지만 애니메이션은 자연스럽게 끝남).
        private Vector3 FindAimPoint()
        {
            var nearest = Physics.OverlapSphere(_projectileSpawnPoint.position, _shootRange, _targetLayerMask, QueryTriggerInteraction.Ignore)
                .OrderBy(c => Vector3.Distance(_projectileSpawnPoint.position, c.transform.position))
                .FirstOrDefault();

            if (nearest == null)
                return _projectileSpawnPoint.position + transform.forward * _shootRange;

            return nearest.transform.position + Vector3.up * _aimHeightOffset;
        }
        #endregion
    }
}