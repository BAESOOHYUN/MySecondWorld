using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using MySecondWorld.Network;
using MySecondWorld.Move;
using MySecondWorld.Inventory;

namespace MySecondWorld.Combat.Player
{
    // 입력 발생 → 쿨다운 체크 → 애니메이션 트리거 → (애니메이션의 타격/발사 프레임에서) 판정
    public class PlayerAttackController : MonoBehaviour
    {
        #region 인스펙터
        [Header("참조")]
        [SerializeField] private NetworkWeaponEquip _weaponEquip;
        [SerializeField] private PlayerAttackAuthority _attackAuthority;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private InputActionReference _attackAction;
        [SerializeField] private PlayerCombatStatsSO _stats;
        [SerializeField] private MovementController _movement;

        [Header("근접 판정")]
        [SerializeField] private Transform _attackPoint;

        [Header("조준")]
        [Tooltip("조준·회전 대상 탐색용 레이어. 근접·원거리 공통이다.\n" +
              "실제 데미지 판정 레이어는 서버가 따로 정한다(클라 값을 믿지 않음).")]
        [SerializeField] private LayerMask _targetLayerMask;

        [Header("원거리 판정")]
        // 조준/발사 기준점의 폴백. 정상 경로에서는 ArrowNockPoint.MuzzlePosition(손 소켓)을 쓰고,
        // 노킹 포인트가 없는 구성(활을 안 쓰는 캐릭터 등)에서만 이 값이 쓰인다. (_nockPoint가 없을 때의 폴백으로만 쓰임)
        [SerializeField] private Transform _projectileSpawnPoint;
        [SerializeField] private ArrowNockPoint _nockPoint;
        [SerializeField] private NetworkProjectileRelay _relay;

        #endregion

        #region 상태

        // 공격 회차. _releasedToken == _attackToken 이면 "직전 회차가 끝났다" = 다음 공격 가능.
        // 상태 두 개로 연사·캔슬·중복발사 방지가 전부 표현된다.
        private int _attackToken;
        private int _releasedToken;
        private float _attackStartTime;
        private float _nextAttackTime;
        private float _lastAttackTime = float.NegativeInfinity;

        // 입력 시점에 확정한 조준 방향. 발사까지 유지해서 모든 피어가 같은 방향을 쓰게 한다.
        // 발사 때 다시 계산하면 그 사이 대상이 움직여 원격 화면과 실제 궤적이 어긋난다.
        private Vector3 _pendingAimDirection;

        public bool IsInCombat => Time.time - _lastAttackTime < _stats.CombatStateDuration;

        // 공격이 진행 중인가 = 몸 회전을 잠글 것인가.
        // 발사(타격) 전까지만 true. 타임아웃도 함께 보므로 이벤트가 씹혀도 잠금이 영구히 남지 않는다.
        private bool IsAttackRoundOpen
        {
            get
            {
                // 안전장치 — 어떤 경우에도 여기서 무조건 풀린다
                if (Time.time - _attackStartTime >= _stats.AttackTimeout) return false;

                // 입력 ~ 발사: 아직 Attack 스테이트로 전이 중일 수 있어 토큰이 책임진다
                if (_releasedToken != _attackToken) return true;

                // 발사 ~ 클립 종료: 활을 내리는 동작까지 애니메이터가 책임진다
                return _animationController.IsAttacking;
            }
        }

        #endregion

        #region Unity 생명주기

        private void OnEnable()
        {        
            if (_weaponEquip == null || _animationController == null ||
                _attackAuthority == null || _attackAction == null || _attackPoint == null ||
                _projectileSpawnPoint == null || _stats == null || _relay == null || _movement == null)
            {
                Debug.LogError($"PlayerAttackController 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _attackAction.action.Enable();
            _attackAction.action.performed += OnAttackInput;
            _weaponEquip.OnMainHandVisualApplied += HandleMainHandChanged;
        }

        private void Update()
        {
            if (!_weaponEquip.isLocalPlayer) return;

            // 켜고 끄는 지점을 여러 곳에 두면(입력/CloseRound/AbortAttack/OnDisable)
            // 한 곳만 빠뜨려도 잠금이 남는다. 상태에서 유도하면 그럴 일이 없다.
            if (_movement != null)
                _movement.RotationLocked = IsAttackRoundOpen;
        }

        private void OnDisable()
        {
            if (_attackAction != null)
                _attackAction.action.performed -= OnAttackInput;
            _weaponEquip.OnMainHandVisualApplied -= HandleMainHandChanged;
        }

        #endregion

        #region 입력

        private void OnAttackInput(InputAction.CallbackContext ctx)
        {
            if (!_weaponEquip.isLocalPlayer) return; // 원격 플레이어 — 내 입력에 반응하면 안 됨
            if (!IsReadyToAttack()) return;

            bool isBow = _weaponEquip.GetMainHandCategory() == WeaponCategory.Bow;

            // 회전을 먼저 — 몸이 향한 뒤에 조준을 계산해야 둘이 같은 곳을 본다.
            // (대상이 없어 정면으로 쏘는 경우, 회전 없이 계산하면 엉뚱한 방향이 잡힌다)
            FaceAttackTarget(isBow ? _stats.ShootRange : _stats.MeleeFacingRange);

            if (isBow)
            {
                // 조준은 '입력 시점'에 확정한다. 발사 프레임까지 0.3~0.6초 여유가 생겨
                // 원격 피어가 자기 발사 프레임에 이 값을 읽어 쓸 수 있다.
                // 발사 프레임에 보내면 왕복 지연만큼 늦게 도착해 남의 화면에서 화살이 늦게 떠난다.
                _pendingAimDirection = ComputeAimDirection();
                _attackAuthority.CmdSetAimDirection(_pendingAimDirection);
            }

            _attackToken++;
            _attackStartTime = Time.time;
            _nextAttackTime = Time.time + _stats.AttackCooldown;
            _lastAttackTime = Time.time;

            _animationController.HandleAttack();
        }

        #endregion

        #region 공격 회차

        private bool IsReadyToAttack()
        {
            if (Time.time < _nextAttackTime) return false; // 연사 속도 상한

            if (_releasedToken != _attackToken) // 직전 회차가 아직 발사 전 — 연타는 무시된다
            {
                // 다만 이벤트가 씹혀 영영 안 올 수도 있다.
                // 타임아웃이 지났으면 회차를 강제로 닫아 공격이 영구히 잠기는 걸 막는다.
                if (Time.time - _attackStartTime < _stats.AttackTimeout) return false;
                AbortAttack();
            }
            return true;
        }

        // CloseRound()        ← 회차를 정상적으로 닫는다
        // 회차를 닫는다. 이미 닫힌 회차면 false(중복 이벤트).
        //
        // 원격 피어에는 입력이 없어 토큰이 늘 같은 값이다 —
        // 거기에도 이 게이트를 걸면 남의 화살이 영영 안 나가므로 로컬만 검사한다.
        private bool CloseRound()
        {
            if (!_weaponEquip.isLocalPlayer) return true;
            if (_releasedToken == _attackToken) return false;

            _releasedToken = _attackToken;
            return true;
        }

        // AbortAttack()       ← 회차를 강제로 닫는다
        // 진행 중인 공격 회차를 종료해 다음 공격을 받을 수 있게 한다.
        // 애니메이션을 멈추지 않고, 손 화살도 회수하지 않는다 —
        // 모션이 끝까지 가서 발사 이벤트가 와도 CloseRound()가 "이미 닫힌 회차"로 걸러낸다.
        // 여러 번 불러도 안전하다(피격/사망/무기교체가 서로 순서를 신경 쓸 필요 없음).
        public void AbortAttack() => _releasedToken = _attackToken;
        private void HandleMainHandChanged(WeaponCategory _) => AbortAttack();

        #endregion


        #region 애니메이션 이벤트

        // Draw(시위를 당기는) 프레임. 모든 피어에서 발생한다.
        public void OnAttackNock()
        {
            // 애니메이션을 끊고 다시 시작하면 이 이벤트가 연속으로 두 번 올 수 있다.
            // Nock()이 "이미 손에 있으면 아무것도 안 함"이라 두 번째 호출은 무시된다.
            _nockPoint?.Nock();
        }

        // 근접 타격 프레임. 기존 클립에 이미 박혀 있는 이벤트 이름이라 그대로 둔다.
        public void OnAttackHit()
        {
            if (!CloseRound()) return;
            if (!_weaponEquip.isLocalPlayer) return;

            _attackAuthority.CmdResolveAttackHit(
                _attackPoint.position, _stats.AttackRadius, _stats.UnarmedAttack);
        }

        // 활 발사(화살이 손을 떠나는) 프레임.
        public void OnAttackRelease()
        {
            if (!CloseRound()) return;
            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow) return;

            ReleaseArrow();
        }

        #endregion


        #region 발사

        private void ReleaseArrow()
        {
            // 로컬은 지금 조준을 다시 계산하고, 원격은 SyncVar로 받은 방향을 쓴다.
            var direction = _weaponEquip.isLocalPlayer
             ? _pendingAimDirection
             : _attackAuthority.AimDirection;

            if (direction.sqrMagnitude < 0.0001f) return;

            var origin = AimOrigin;

            if (_weaponEquip.isLocalPlayer)
                _attackAuthority.CmdRequestShoot(origin, direction);

            // ★ 서버 피어(호스트/전용 서버)는 여기서 쏘지 않는다.
            //   위 Cmd가 서버에서 권위 화살을 쏘면서 같은 손 화살을 소비하기 때문 —
            //   여기서 또 쏘면 호스트 화면에 화살이 두 개가 된다.
            if (NetworkServer.active) return;

            var request = new ProjectileRequest
            {
                Prefab = _relay.ProjectilePrefab,
                NockSource = _nockPoint, // ← 손에 든 그 화살이 날아간다
                Origin = origin,
                Direction = direction,
                Speed = _stats.ProjectileSpeed,
                MaxLifetime = _stats.ShootRange / Mathf.Max(_stats.ProjectileSpeed, 0.01f) + 0.3f,
                // 표현 화살은 판정하지 않으므로 반경·마스크는 쓰이지 않는다
            };

            _relay.TrackPresentation(ProjectileLauncher.Launch(request, gameObject.scene));
        }

        #endregion

        #region 조준 (※ 클릭 타겟팅 도입 시 이 리전을 교체한다)


        // 조준·발사의 공통 기준점. 손에 화살이 있으면 그 위치, 없으면 폴백 스폰 포인트.
        // 탐색과 발사가 서로 다른 점에서 재던 기존 불일치를 여기로 통일한다.
        // FindAimPoint()가 대상 탐색과 조준점 계산을 같이 하고 있는데, LookAt은 대상만 필요
        private Vector3 AimOrigin =>
            _nockPoint != null ? _nockPoint.MuzzlePosition : _projectileSpawnPoint.position;

        // 사거리 안에서 가장 가까운 대상. 없으면 null.
        private Transform FindNearestTarget(float range)
        {
            var nearest = Physics.OverlapSphere(AimOrigin, range, _targetLayerMask, QueryTriggerInteraction.Ignore)
                .OrderBy(c => Vector3.Distance(AimOrigin, c.transform.position))
                .FirstOrDefault();

            return nearest != null ? nearest.transform : null;
        }

        // 조준점 = 사거리 안에서 가장 가까운 대상의 몸통 높이.
        //
        // 호출 시점은 '발사 순간'이 아니라 '공격 입력 시점'이다(_pendingAimDirection).
        // 그래야 원격 피어가 자기 발사 프레임에 같은 방향을 쓸 수 있고,
        // 조준이 버튼을 누르는 순간에 확정돼 쏘는 도중 대상을 따라가지 않는다.
        //
        // 대상이 없으면 정면으로 쏜다 — 빗나감은 확정이지만 애니메이션은 자연스럽게 끝난다.
        // 발사 자체를 취소하면 입력이 씹힌 것처럼 느껴진다.
        //
        // ※ AimHeightOffset이 고정값이라 대상 크기를 반영하지 못한다(보스는 낮게, 작은 몹은 높게 겨눈다).
        private Vector3 FindAimPoint()
        {
            var target = FindNearestTarget(_stats.ShootRange);
            if (target == null)
                return AimOrigin + transform.forward * _stats.ShootRange; // 대상 없음 — 정면으로

            return target.position + Vector3.up * _stats.AimHeightOffset;
        }

        private Vector3 ComputeAimDirection() => (FindAimPoint() - AimOrigin).normalized;

        // 공격 입력 시점에 대상 쪽으로 즉시 회전한다.
        //
        // 수평 성분만 쓰는 건 MonsterAI.RotateTowardsTarget()과 같은 이유다 —
        // y까지 반영하면 위/아래 적을 볼 때 캐릭터가 앞뒤로 넘어진 것처럼 기운다.
        //
        // 대상이 없으면 아무것도 하지 않는다(지금 보는 방향 유지).
        // 억지로 정면을 향해 돌리면 허공을 쏠 때 캐릭터가 홱 도는 게 더 이상하다.
        private void FaceAttackTarget(float searchRange)
        {
            var target = FindNearestTarget(searchRange);
            if (target == null) return;

            Vector3 lookDirection = target.position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        #endregion
    }
}