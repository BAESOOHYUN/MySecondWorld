using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using MySecondWorld.Network;
using MySecondWorld.Move;
using MySecondWorld.Inventory;
using MySecondWorld.Pool;

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

        [Header("원거리 판정 (투사체 — Bow/Staff 등 공용)")]
        [SerializeField] private Transform _projectileSpawnPoint; // 투사체가 실제로 발사되는 지점(무기별로 다름 — Bow는 오른손 소켓 하위 활 시위 위치)
        [SerializeField] private GameObject _projectilePrefab; // 순수 프리팹(스크립트 없음) — ProjectileVisualFactory가 스폰 시점에 ProjectileVisualMover를 붙임

        // 공격 반경/쿨다운/투사체 속도 등 나머지 튜닝 수치는 전부 _stats(PlayerCombatStatsSO)에서 읽는다.

        // 스킬 실행기 — 근접만. 원거리는 순수 비주얼이라 SkillExecutor 불필요(데미지는 서버 SphereCast가 확정).
        private SkillExecutor _skillExecutor;

        // 원거리 비주얼 전용 풀 — 물리 판정 없이 시간 기반으로만 이동(호스트가 아닌 클라에서도 항상 정확히 동작).
        private IGameObjectPool _visualPool;
        private GameObject _pendingVisualInstance;

        // 런타임 상태
        private float _nextAttackTime;
        private float _lastAttackTime = float.NegativeInfinity;
        private Vector3? _pendingAimPoint; // OnProjectileSpawn에서 계산한 조준점 — OnShootRelease에서 재사용(재계산 시 화살 각도 스냅 방지)

        public bool IsInCombat => Time.time - _lastAttackTime < _stats.CombatStateDuration;

        #region Unity 생명주기

        private void Awake()
        {
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() }
                );

            _visualPool = new GameObjectPool(
                () => ProjectileVisualFactory.CreatePooled(_projectilePrefab, _visualPool, gameObject.scene),
                defaultCapacity: 4, maxSize: 10);
        }

        private void OnEnable()
        {
            if (_weaponEquip == null || _animationController == null ||
                _attackAuthority == null || _attackAction == null || _attackPoint == null ||
                _projectileSpawnPoint == null || _projectilePrefab == null || _stats == null)
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
            _visualPool?.Dispose();
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

        // 투사체를 스폰 위치에 미리 배치만 함(비주얼 노킹 단계, 아직 안 날아감).
        // 투사체 발사 애니메이션의 "손에 화살이 나타나는(노킹)" 프레임에 Animation Event로 연결.
        public void OnProjectileSpawn()
        {
            if (!_weaponEquip.isLocalPlayer)
                return;

            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow)
                return;

            if (_visualPool == null)
                return;

            if (_pendingVisualInstance != null) // 직전 노킹이 발사 없이 재스폰된 경우 — 누수 방지로 먼저 반납
            {
                _pendingVisualInstance.transform.SetParent(null);
                _pendingVisualInstance.GetComponent<PooledObject>().ReturnToPool();
            }

            _pendingVisualInstance = _visualPool.Rent();
            var mover = _pendingVisualInstance.GetComponent<ProjectileVisualMover>();
            var t = _pendingVisualInstance.transform;
            t.SetParent(_projectileSpawnPoint, false); // 노킹 상태 — 손(스폰 포인트)의 애니메이션 움직임을 그대로 따라감
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            // 부모(스폰 포인트) 본의 스케일이 원본 프리팹 스케일을 왜곡시키지 않도록 보정
            var parentScale = _projectileSpawnPoint.lossyScale;
            var original = mover.OriginalLocalScale;
            t.localScale = new Vector3(original.x / parentScale.x, original.y / parentScale.y, original.z / parentScale.z);

            _pendingAimPoint = FindAimPoint();
        }

        // 실제 발사 — 서버 데미지 확정(CmdResolveShootHit) + 미리 배치해둔 화살을 조준점까지만 날아가다 사라지는 순수 비주얼로 전환.
        // 물리 충돌 판정에 의존하지 않음 — 호스트가 아닌 순수 클라이언트는 로컬에 진짜 몬스터 콜라이더가 없어
        // 물리 판정 자체가 불가능하기 때문(실제 데미지는 서버 SphereCast가 독립적으로 확정).
        // 투사체 발사 애니메이션(현재는 Bow만 존재 — Archer@BowShot01)의 "화살이 손을 떠나는" 프레임에 Animation Event로 연결.
        // TODO : 향후 Staff 등 다른 원거리 무기가 추가되면 아래 카테고리 체크(Bow)만 확장하면 된다.
        public void OnShootRelease()
        {
            if (!_weaponEquip.isLocalPlayer)
                return;

            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow)
                return;

            var aimPoint = _pendingAimPoint ?? FindAimPoint();
            _pendingAimPoint = null;
            var direction = (aimPoint - _projectileSpawnPoint.position).normalized;

            _attackAuthority.CmdResolveShootHit(
                _projectileSpawnPoint.position, direction, _stats.ShootRange, _stats.ProjectileColliderRadius, _stats.UnarmedAttack);

            GameObject instance = _pendingVisualInstance;
            _pendingVisualInstance = null;
            if (instance == null) // 드물게 OnProjectileSpawn 이벤트가 안 걸린 클립이면 지금 바로 대여
                instance = _visualPool.Rent();

            var mover = instance.GetComponent<ProjectileVisualMover>();
            var t = instance.transform;
            t.SetParent(null); // 발사 순간 손에서 분리 — 이제부터 월드 기준 독립 비행
            t.SetPositionAndRotation(_projectileSpawnPoint.position, Quaternion.LookRotation(direction));
            t.localScale = mover.OriginalLocalScale;

            float distance = Vector3.Distance(_projectileSpawnPoint.position, aimPoint);
            float flightTime = distance / Mathf.Max(_stats.ProjectileSpeed, 0.01f);
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