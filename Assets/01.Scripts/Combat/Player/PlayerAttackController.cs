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

        [Header("근접 판정")]
        [SerializeField] private Transform _attackPoint;
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

        public bool IsInCombat => Time.time - _lastAttackTime < _stats.CombatStateDuration;

        #endregion

        #region Unity 생명주기

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
            _weaponEquip.OnMainHandVisualApplied += HandleMainHandChanged;
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

            // 조준 방향은 '입력 시점'에 보낸다. 발사 프레임까지 0.3~0.6초 여유가 생겨
            // 원격 피어가 자기 발사 프레임에 이 값을 읽어 쓸 수 있다.
            // 발사 프레임에 보내면 왕복 지연만큼 늦게 도착해 남의 화면에서 화살이 늦게 떠난다.
            if (_weaponEquip.GetMainHandCategory() == WeaponCategory.Bow)
                _attackAuthority.CmdSetAimDirection(ComputeAimDirection());

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
                ? ComputeAimDirection()
                : _attackAuthority.AimDirection;

            if (direction.sqrMagnitude < 0.0001f) return;

            var origin = _nockPoint != null ? _nockPoint.MuzzlePosition : _projectileSpawnPoint.position;

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

        #region 조준

        private Vector3 ComputeAimDirection()
        {
            var origin = _nockPoint != null ? _nockPoint.MuzzlePosition : _projectileSpawnPoint.position;
            return (FindAimPoint() - origin).normalized;
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