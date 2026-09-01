using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using MySecondWorld.Network;
using MySecondWorld.Move;
using MySecondWorld.Inventory;
using MySecondWorld.Combat.Monster;

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

        // 활 홀드 조준 상태. 실제 판정에 쓰이는 게이지는 서버(PlayerAttackAuthority)가 직접
        // 시간을 재서 관리한다 — 여기 값들은 로컬 UI/미리보기 예측치일 뿐이다.
        private bool _isCharging;
        private Transform _pendingHomingTarget;   // 홀드 시작 시점에 고정한 유도 대상
        private float _pendingHomingTurnRate;      // 릴리즈 순간 굳힌 값(로컬 표현 화살용)

        // HUD가 구독하는 게이지 값(0~1). 활이 아니거나 충전 중이 아니면 0.
        public float AimGauge01 { get; private set; }

        public bool IsBowEquipped => _weaponEquip.GetMainHandCategory() == WeaponCategory.Bow;
        public bool IsLocalPlayer => _weaponEquip.isLocalPlayer;

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

        // 탭으로 고른 대상. PlayerTapAttack이 설정한다.
        // 비어 있으면 기존 자동 최근접 그대로 동작한다(회귀 안전장치).
        public Transform SelectedTarget { get; set; }

        // 화면 HUD는 로컬 플레이어보다 먼저 살아 있을 수 있다 — PlayerHealthState.Local과 같은 패턴.
        public static PlayerAttackController Local { get; private set; }
        public static event Action<PlayerAttackController> LocalReady;

        // Domain Reload OFF — static이 플레이 세션을 넘어 살아남으면 파괴된 객체를 가리킨다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalReady = null;
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
            // 근접=누르는 즉시, 활=홀드 충전 — 같은 입력이라 press/release로 직접 갈라 잡는다.
            _attackAction.action.started += OnAttackHoldStarted;
            _attackAction.action.canceled += OnAttackHoldReleased;
            _weaponEquip.OnMainHandVisualApplied += HandleMainHandChanged;

        }

        private void Update()
        {
            if (!_weaponEquip.isLocalPlayer) return;

            // OnEnable 시점엔 아직 isLocalPlayer가 안 정해져 있어(Instantiate 도중) 여기서 확정한다.
            // 이미 설정됐으면 매 프레임 헛수고하지 않도록 한 번만 통과시킨다.
            if (Local != this)
            {
                Local = this;
                LocalReady?.Invoke(this);
            }

            // 켜고 끄는 지점을 여러 곳에 두면(입력/CloseRound/AbortAttack/OnDisable)
            // 한 곳만 빠뜨려도 잠금이 남는다. 상태에서 유도하면 그럴 일이 없다.
            if (_movement != null)
                _movement.RotationLocked = IsAttackRoundOpen || _isCharging;

            if (_isCharging)
                TickAimGauge();
        }

        private void OnDisable()
        {
            if (_attackAction != null)
            {
                _attackAction.action.started -= OnAttackHoldStarted;
                _attackAction.action.canceled -= OnAttackHoldReleased;
            }
            _weaponEquip.OnMainHandVisualApplied -= HandleMainHandChanged;
            CancelAimCharge();

            if (Local == this) Local = null;
        }

        #endregion

        #region 입력

        // 근접: 누르는 즉시 공격
        // 활: 여기서는 충전만 시작한다 — 발사는 손을 뗄 때(OnAttackHoldReleased).
        private void OnAttackHoldStarted(InputAction.CallbackContext ctx)
        {
            if (!_weaponEquip.isLocalPlayer) return;

            if (!IsBowEquipped)
            {
                TryAttack();
                return;
            }

            if (!IsReadyToAttack()) return; // 쿨다운/직전 회차 중이면 홀드 자체를 안 받는다
            BeginAimCharge();
        }

        // 충전 중이 아니었으면(근접이었거나 쿨다운으로 거부됐던 경우) 아무것도 하지 않는다.
        private void OnAttackHoldReleased(InputAction.CallbackContext ctx)
        {
            if (!_weaponEquip.isLocalPlayer) return;
            if (!_isCharging) return;

            EndAimChargeAndShoot();
        }

        // 탭 공격(PlayerTapAttack)과 공격 입력이 반드시 같은 경로를 지나게 하려고 분리했다.
        // 쿨다운·회차 토큰을 우회하는 두 번째 경로가 생기면 연사 방지가 무너진다.
        // 활에서는 "대상이 없어 충전할 게 없을 때"의 즉시발사 폴백으로도 쓰인다(게이지 0%와 동일).
        public void TryAttack()
        {
            if (!_weaponEquip.isLocalPlayer) return; // 원격 플레이어 — 내 입력에 반응하면 안 됨
            if (!IsReadyToAttack()) return;

            ConfirmAttack(IsBowEquipped);
            _animationController.HandleAttack(); // 근접, 그리고 활의 "대상 없음" 즉시발사 폴백 — 기존 단일 클립
        }

        // 회전 확정 → (활이면) 조준 방향 확정 → 회차 열기. 애니메이션 트리거는 호출부가 각자 결정한다
        // (근접/즉시발사는 기존 Action, 홀드 발사는 Release) — 여기 섞으면 두 흐름이 뒤엉킨다.
        private void ConfirmAttack(bool isBow)
        {
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
        }

        #endregion

        #region 조준 게이지 (활 홀드 충전)

        // ② 몹을 선택해야만 게이지가 찬다.
        // 탭 선택 없으면 사거리 안 자동 최근접까지가 "선택"이다 (FindAttackTarget과 같은 우선순위).
        // 그마저도 없으면 게이지 없이 즉시 쏜다. "눌러도 반응이 없다"는 게 더 어색하다.
        private void BeginAimCharge()
        {
            var target = FindAttackTarget(_stats.ShootRange);
            if (target == null)
            {
                TryAttack();
                return;
            }

            _isCharging = true;
            AimGauge01 = 0f;
            _pendingHomingTarget = target;

            uint targetNetId = target.TryGetComponent(out NetworkIdentity identity) ? identity.netId : 0;
            _attackAuthority.CmdBeginAim(targetNetId);

            _animationController.HandleAimLoad();
        }

        // ④ 이동 중엔 서서히 감소, 멈춰 있으면 서서히 충전. 로컬 표시용 예측치일 뿐이고
        // 실제 유도 강도는 서버가 CmdRequestShoot 시점에 자기 게이지(_aimGaugeServer)로 다시 계산한다.
        private void TickAimGauge()
        {
            bool isMoving = _movement != null && _movement.InputMagnitude > 0.01f;

            AimGauge01 = isMoving
                ? Mathf.Clamp01(AimGauge01 - _stats.AimGaugeDrainRate * Time.deltaTime)
                : Mathf.Clamp01(AimGauge01 + Time.deltaTime / _stats.AimChargeDuration);
        }

        private void EndAimChargeAndShoot()
        {
            _isCharging = false;
            // 릴리즈 순간 값을 굳힌다 — ConfirmAttack 이후 애니메이션 발사 프레임까지 시간차가 있어서,
            // 그때 다시 읽으면 이미 0으로 리셋된 AimGauge01을 읽게 된다.
            _pendingHomingTurnRate = Mathf.Pow(AimGauge01, _stats.HomingCurveExponent) * _stats.HomingMaxTurnRate;
            AimGauge01 = 0f;

            _attackAuthority.CmdEndAim(); // 서버가 여기서 게이지를 얼리고 충전을 닫는다
            ConfirmAttack(isBow: true);
            _animationController.HandleAimRelease();
        }

        // 무기 교체·컴포넌트 비활성화 등으로 홀드를 강제로 접을 때. 발사로 이어지지 않는다.
        private void CancelAimCharge()
        {
            if (!_isCharging) return;

            _isCharging = false;
            AimGauge01 = 0f;
            _pendingHomingTarget = null;
            _attackAuthority?.CmdCancelAim();
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

        private void HandleMainHandChanged(WeaponCategory _)
        {
            AbortAttack();
            CancelAimCharge();
        }

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

            // "누굴 노렸는지"만 알려준다. 실제로 때릴 수 있는지는 서버의 구체 판정이 정한다.
            uint preferredNetId = 0;
            if (IsSelectionUsable() && SelectedTarget.TryGetComponent(out NetworkIdentity identity))
                preferredNetId = identity.netId;

            _attackAuthority.CmdResolveAttackHit(
                _attackPoint.position, _stats.AttackRadius, _stats.UnarmedAttack, preferredNetId);
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

                // 표현 화살은 판정하지 않으므로 반경·마스크는 쓰이지 않는다.
                // 유도는 내 화면 미리보기용으로만 켠다(데미지는 서버 사본이 결정) — 릴리즈 순간
                // 굳힌 로컬 예측치를 쓴다.
                HomingTarget = _pendingHomingTarget,
                HomingTurnRateDegPerSec = _pendingHomingTurnRate,
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

        // 조준점 = 사거리 안에서 가장 가까운 대상의 몸통 중심.
        //
        // 호출 시점은 '발사 순간'이 아니라 '공격 입력 시점'이다(_pendingAimDirection).
        // 그래야 원격 피어가 자기 발사 프레임에 같은 방향을 쓸 수 있고,
        // 조준이 버튼을 누르는 순간에 확정돼 쏘는 도중 대상을 따라가지 않는다.
        //
        // 대상이 없으면 정면으로 쏜다 — 빗나감은 확정이지만 애니메이션은 자연스럽게 끝난다.
        // 발사 자체를 취소하면 입력이 씹힌 것처럼 느껴진다.
        private Vector3 FindAimPoint()
        {
            var target = FindAttackTarget(_stats.ShootRange);
            if (target == null)
                return AimOrigin + transform.forward * _stats.ShootRange; // 대상 없음 — 정면으로

            if (target.TryGetComponent(out MonsterServerState state))
                return state.BodyCenterWorld;

            return target.position + Vector3.up * _stats.AimHeightOffset;   // 몬스터가 아니면 폴백
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
            var target = FindAttackTarget(searchRange);   // ← 탭으로 고른 놈을 향해 돌게 하는 지점
            if (target == null) return;

            Vector3 lookDirection = target.position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // 선택 대상이 사거리 안이면 그놈, 밖이면 관측 목록에서 가장 가까운 살아있는 몬스터.
        // 조준·회전·근접 우선순위가 전부 이 한 곳을 지나므로 여기만 바꾸면 전부 따라온다.
        //
        // 물리 질의를 안 쓰는 이유: 클라 몬스터 사본에는 실체 콜라이더가 없다(호스트 제외 —
        // CapsuleCollider는 MonsterAI가 서버에서만 붙이고, 탭 판정용 캡슐은 트리거라
        // QueryTriggerInteraction.Ignore에 걸러진다). MonsterServerState의 관측 목록을 쓰면
        // 호스트·원격이 똑같이 동작하고, 죽어가는 몬스터도 자동으로 제외된다
        // (콜라이더 기반은 시체 콜라이더가 남아 있어 시체를 겨눴다).
        //
        // TODO(재설계): 사거리 밖이면 '최근접으로 폴백'이 아니라 '선택 대상을 향해 이동'이어야 한다.
        //   지금은 멀리 있는 놈을 찍어도 눈앞의 다른 놈을 치게 된다.
        //   자동 접근을 넣을 때 같이 정해야 할 것:
        //     - 언제 멈추나 (IsInAttackRange 진입 시)
        //     - 수동 이동 입력이 들어오면 취소하나
        //     - 경로가 막히면 어떻게 하나 (NavMesh 실패 처리)
        //   플레이어 이동은 CharacterController 직접 제어라 NavMeshAgent가 없다 — 경로 탐색 수단부터 결정 필요.
        private Transform FindAttackTarget(float range)
        {
            if (IsSelectionUsable() &&
                (SelectedTarget.position - AimOrigin).sqrMagnitude <= range * range)
                return SelectedTarget;

            Transform nearest = null;
            float bestSqr = range * range;

            foreach (var monster in MonsterServerState.ActiveOnClient)
            {
                if (monster == null || !monster.IsAlive) continue;

                float sqr = (monster.Anchor.position - AimOrigin).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                nearest = monster.Anchor;
            }

            return nearest;
        }

        // 풀에 반납된 몬스터는 파괴되지 않고 비활성화만 되는 경우가 있다(호스트).
        // != null 만으로는 못 거르기 때문에 활성 여부까지 본다 —
        // 이걸 빼면 재사용된 다른 몬스터로 선택이 옮겨붙는다.
        private bool IsSelectionUsable() =>
            SelectedTarget != null && SelectedTarget.gameObject.activeInHierarchy;

        // 탭 공격이 헛스윙을 걸지 않게 하는 사거리 검사.
        //
        // ※ 이 검사는 FaceAttackTarget(몸 회전)보다 먼저 실행된다.
        //    _attackPoint는 플레이어 앞 자식이라 바라보는 방향에 따라 위치가 바뀌므로,
        //    그 좌표를 그대로 쓰면 옆·뒤에 있는 대상이 전부 사거리 밖으로 나온다.
        //    대신 "몸을 돌린 뒤의 도달 거리"를 미리 계산해서 방향과 무관하게 판단한다.
        public bool IsInAttackRange(Transform target)
        {
            if (target == null) return false;

            if (_weaponEquip.GetMainHandCategory() == WeaponCategory.Bow)
                return (target.position - AimOrigin).sqrMagnitude <= _stats.ShootRange * _stats.ShootRange;

            float reach = Vector3.Distance(transform.position, _attackPoint.position) + _stats.AttackRadius;
            return (target.position - transform.position).sqrMagnitude <= reach * reach;
        }

        #endregion
    }
}