using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using MySecondWorld.Network;
using MySecondWorld.Move;
using MySecondWorld.Inventory;

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

        private SkillExecutor _skillExecutor;

        private float _nextAttackTime;
        private float _lastAttackTime = float.NegativeInfinity;

        public bool IsInCombat => Time.time - _lastAttackTime < _stats.CombatStateDuration;

        #region Unity 생명주기

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
            _skillExecutor?.Dispose();
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

        // 화살은 NetworkWeaponEquip이 항상 들고 있는 장식용 인스턴스를 발사 시점에 그대로 넘겨받아 쓰므로
        // 별도로 "노킹" 단계에서 스폰할 게 없다. Archer@BowShot01 클립이 이 이벤트를 계속 쏘니
        // 리시버 없음 경고만 막는 빈 훅으로 남겨둔다.
        public void OnProjectileSpawn() { }

        // 실제 발사 — 서버 데미지 확정(CmdResolveShootHit) + 손에 들려있던 화살을 그대로 날림(신규 스폰 없음).
        // 투사체 발사 애니메이션(현재는 Bow만 존재 — Archer@BowShot01)의 "화살이 손을 떠나는" 프레임에 Animation Event로 연결.
        // TODO : 향후 Staff 등 다른 원거리 무기가 추가되면 아래 카테고리 체크(Bow)만 확장하면 된다.
        public void OnShootRelease()
        {
            Debug.Log("[Shot] OnShootRelease 호출됨"); // 임시 확인용
            if (!_weaponEquip.isLocalPlayer)
                return;

            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow)
                return;

            var aimPoint = FindAimPoint();

            var arrow = _weaponEquip.TakeCurrentArrowVisual();
            Debug.Log($"[Shot] arrow={arrow}, aimPoint={aimPoint}"); // 추가
            if (arrow == null)
                return; // 예외적으로 화살이 없는 상태(무기 해제 등) — 발사 취소

            var t = arrow.transform;
            var direction = (aimPoint - t.position).normalized; // 화살의 실제 현재 위치 기준으로 방향 계산

            Debug.Log($"[Shot] direction={direction}, arrowPos={t.position}"); // 추가
            // 실제 데미지 확정 — 서버 권위(PlayerAttackAuthority.CmdResolveShootHit).
            _attackAuthority.CmdResolveShootHit(
                t.position, direction, _stats.ShootRange, _stats.ProjectileColliderRadius, _stats.UnarmedAttack);

            var mover = arrow.AddComponent<ProjectileVisualMover>();
            t.SetParent(null); // 손에서 분리 — 이제부터 월드 기준 독립 비행
            t.rotation = Quaternion.LookRotation(direction);
            t.localScale = Vector3.one; // 장착 때(ResetSocketTransform)와 동일하게 "월드 스케일 1"로 복원

            float distance = Vector3.Distance(t.position, aimPoint);
            float flightTime = distance / Mathf.Max(_stats.ProjectileSpeed, 0.01f);
            Debug.Log($"[Shot] distance={distance}, flightTime={flightTime}"); // 추가
            mover.Setup(direction, _stats.ProjectileSpeed, flightTime);
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