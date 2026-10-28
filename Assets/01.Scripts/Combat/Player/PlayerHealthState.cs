using System;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;
using MySecondWorld.Foundation;
using MySecondWorld.Move;
using MySecondWorld.Network;
using MySecondWorld.Combat.Vfx;

namespace MySecondWorld.Combat.Player
{
    /// <summary>
    /// 플레이어 HP를 클라이언트로 동기화한다. PlayerBase 프리팹에 부착.
    /// Health(서버 권위 계산)와 분리하는 구조는 Monster/CompanionServerState와 같다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class PlayerHealthState : NetworkBehaviour, IHealthView, IStunnable
    {
        #region Constants & Statics

        private const float DeathAnimationTimeoutSeconds = 1.5f; // Animation Event 배선 누락 시 안전장치 (1.5초)
        private const float HitCooldown = 0.7f; // 피격 연타 쿨다운 (0.35초 경직 후 최소 0.35초 탈출 윈도우 보장)

        private static readonly int DeathTrigger = Animator.StringToHash(nameof(DeathTrigger));
        private static readonly int HitTrigger = Animator.StringToHash(nameof(HitTrigger));
        public static PlayerHealthState Local { get; private set; }
        public static event Action<PlayerHealthState> LocalReady;

        // Mirror를 참조하지 않는 어셈블리(AI/Camera)용 접근자.
        // Local.transform은 NetworkBehaviour 상속 멤버라 Mirror 참조가 없으면 CS0012가 난다.
        public static Transform LocalTransform => Local != null ? Local.transform : null;

        // Domain Reload OFF — static이 플레이 세션을 넘어 살아남으면 파괴된 객체를 가리킨다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalReady = null;
        }

        #endregion

        #region Inspector Fields

        [Header("사망 처리")]
        [SerializeField] private MovementController _movement;
        [SerializeField] private NetworkAnimator _networkAnimator;
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider _playerCollider;

        [Header("VFX")]
        [SerializeField] private CombatVfxDefaultsSO _vfxDefaults;

        #endregion

        #region SyncVars

        // 비율이 아니라 절대값 두 개를 나른다 — "78/100" 숫자 표시가 필요해서다.
        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _currentHP;

        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _maxHP = 1f;   // 0이면 UI의 나눗셈이 터진다

        #endregion

        #region Private Fields

        private Health _health;
        private ServerZoneService _zoneService;
        private UniTaskCompletionSource _deathAnimationTcs;
        private float _lastHitTime = -999f;
        private bool _isStunned;

        #endregion

        #region Properties & Events

        public Transform Anchor => transform;      // 화면 고정 바에서는 안 쓰이지만 계약이라 채운다
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsDead => (_health != null && _health.IsDead) || _currentHP <= 0f;
        public bool IsStunned => _isStunned;
        public event Action HPChanged;
        public event Action OnLocalDied;
        public event Action OnLocalRespawned;

        #endregion

        #region Unity Lifecycle & Network Callbacks

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
            if (_movement == null) _movement = GetComponent<MovementController>();
            if (_health == null) _health = GetComponent<Health>();
        }

        public override void OnStartServer()
        {
            _health = GetComponent<Health>();
            _maxHP = _health.MaxHP;
            _currentHP = _health.CurrentHP;

            // [Inject] 대신 여기서 꺼내는 이유 —
            // ServerZoneService는 서버에만 있는데(GameNetworkManager.OnStartServer에서 new),
            // PlayerScope는 서버·클라이언트 양쪽에서 만들어진다.
            // 클라에서 없는 걸 요구하면 스코프가 통째로 깨진다.
            // OnStartServer는 서버 전용 훅이라 이 줄에선 반드시 존재한다.
            _zoneService = (NetworkManager.singleton as GameNetworkManager)?.ZoneService;

            _health.OnDamaged += HandleDamaged;
            _health.OnDied += HandleDied;
        }

        public override void OnStopServer()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
                _health.OnDied -= HandleDied;
            }
        }

        public override void OnStartLocalPlayer()
        {
            Local = this;
            LocalReady?.Invoke(this);
        }

        public override void OnStopClient()
        {
            if (Local == this) Local = null;
        }

        #endregion

        #region 피격 및 데미지 처리

        private void OnHPValueChanged(float oldValue, float newValue) => HPChanged?.Invoke();
        
        [Server]
        private void HandleDamaged(float newCurrentHP)
        {
            _currentHP = newCurrentHP;
            if (_health.IsDead) return;

            // 히트 VFX는 전원이 봐야 해서 쿨다운과 무관하게 매 타격 브로드캐스트한다
            // (쿨다운은 피격 애니메이션/경직 스팸 방지용이지 데미지 표현용이 아니다).
            RpcPlayHitVfx(transform.position);

            // [쿨다운] 플레이어도 잡몹 여러 마리한테 맞을 때 무한 윽윽 방지
            if (Time.time - _lastHitTime < HitCooldown) return;
            _lastHitTime = Time.time;

            // 오너 클라이언트에게 피격 모션 트리거 지시
            TargetPlayHitAnimation(connectionToClient);
        }

        [ClientRpc]
        private void RpcPlayHitVfx(Vector3 position)
        {
            if (_vfxDefaults == null || _vfxDefaults.HitVfxPrefab == null) return;
            CombatVfxSpawner.Spawn(_vfxDefaults.HitVfxPrefab, position, Quaternion.identity);
        }

        [TargetRpc]
        private void TargetPlayHitAnimation(NetworkConnection target)
        {
            bool wasCharging = false;
            // 조준 중이었다면 피격으로 인해 조준 강제 취소 (슈퍼아머 취소)
            if (TryGetComponent(out PlayerAttackController attackController))
            {
                wasCharging = attackController.IsCharging;
                attackController.CancelAimCharge();
            }

            // [슈퍼아머] 조준 중이 아니었고, 실제 타격 공격(근접 공격 또는 화살 발사 프레임) 중일 때만 피격 캔슬 방지
            if (!wasCharging)
            {
                if (TryGetComponent(out ICombatant combatant) && combatant.IsAttacking)
                    return;
                if (_animator != null && (
                    _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
                    (_animator.layerCount > 1 && _animator.GetCurrentAnimatorStateInfo(1).IsTag("Attack"))
                ))
                    return;
            }

            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(HitTrigger);
            else if (_animator != null)
                _animator.SetTrigger(HitTrigger);

            // [피격 경직] 0.35초 동안 플레이어 조작/이동 잠금 (조작 탈출감 개선)
            ApplyHitStun().Forget();
        }

        /// <summary>
        /// IStunnable 구현 — 외부 CC기 및 피격 경직 공통 진입점
        /// </summary>
        public void ApplyStun(float duration = 0.35f) => ApplyHitStun(duration).Forget();

        private System.Threading.CancellationTokenSource _stunCts;

        private async UniTaskVoid ApplyHitStun(float duration = 0.35f)
        {
            if (_movement == null) _movement = GetComponent<MovementController>();
            if (_movement == null) return;

            _stunCts?.Cancel();
            _stunCts?.Dispose();
            _stunCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var ct = _stunCts.Token;

            _isStunned = true;
            _movement.MovementLocked = true;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
                _isStunned = false;
                // 사망 상태가 아닐 때만 이동 락 해제
                if (_movement != null && !IsDead)
                    _movement.MovementLocked = false;
            }
            catch (OperationCanceledException) { }
        }
        #endregion

        #region 사망 시퀀스

        // Death 클립 마지막 프레임 근처에 Animation Event로 연결
        public void OnDeathAnimationEnd()
        {
            if (isServer)
                _deathAnimationTcs?.TrySetResult();
            else if (isClient)
                CmdOnDeathAnimationEnd();
        }

        [Command]
        private void CmdOnDeathAnimationEnd() => _deathAnimationTcs?.TrySetResult();

        [Server]
        private void HandleDied()
        {
            if (_movement != null) _movement.MovementLocked = true;
            if (_playerCollider != null) _playerCollider.enabled = false;
            RpcPlayDeathVfx(transform.position);
            PlayDeathSequence().Forget();
        }

        [ClientRpc]
        private void RpcPlayDeathVfx(Vector3 position)
        {
            if (_vfxDefaults == null || _vfxDefaults.DeathVfxPrefab == null) return;
            CombatVfxSpawner.Spawn(_vfxDefaults.DeathVfxPrefab, position, Quaternion.identity);
        }

        private async UniTaskVoid PlayDeathSequence()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                _deathAnimationTcs = new UniTaskCompletionSource();

                // 1. 오너 클라이언트에게 사망 애니메이션 트리거 위임
                TargetPlayDeathAnimation(connectionToClient);
                // 2. 애니메이션 종료 또는 1.5초 타임아웃 대기
                await UniTask.WhenAny(
                    _deathAnimationTcs.Task,
                    UniTask.Delay(TimeSpan.FromSeconds(DeathAnimationTimeoutSeconds), cancellationToken: ct));
                // 3. 사망 화면 UI 팝업 요청
                TargetShowDeathScreen();
            }
            catch (OperationCanceledException)
            {
                // 대기 중 오브젝트가 파괴됨 — 정상 흐름
            }
        }

        // 오너 클라이언트가 NetworkAnimator를 구동하여 Mirror가 전체에 릴레이하도록 함
        [TargetRpc]
        private void TargetPlayDeathAnimation(NetworkConnection target)
        {
            if (_movement != null) _movement.MovementLocked = true; // 죽었을 때 내 이동 조작 락

            // 조준 또는 공격 진행 중이었다면 강제 취소 (사망 모션 덮어쓰기 및 벌떡 일어남 방지)
            if (TryGetComponent(out PlayerAttackController attackController))
            {
                attackController.CancelAimCharge();
                attackController.AbortAttack();
            }

            if (_networkAnimator != null)
            {
                _networkAnimator.ResetTrigger(HitTrigger);
                _networkAnimator.SetTrigger(DeathTrigger);
            }
            else if (_animator != null)
            {
                _animator.ResetTrigger(HitTrigger);
                _animator.SetTrigger(DeathTrigger);
            }
        }

        [TargetRpc]
        private void TargetShowDeathScreen() => OnLocalDied?.Invoke();

        #endregion

        #region 부활 시퀀스 (Respawn Sequence)

        [Command]
        public void CmdRequestRespawn(bool toTown)
        {
            if (!_health.IsDead) return; // 살아있는데 리스폰 요청은 무시(조작 방지)

            _health.ReviveFull();
            _currentHP = _health.CurrentHP;
            // 1. 스폰 구역으로 텔레포트 (마을 부활 여부 전달)
            if (_zoneService == null)
                _zoneService = (NetworkManager.singleton as GameNetworkManager)?.ZoneService;
            _zoneService?.RespawnPlayer(connectionToClient, toTown);
            // 2. 물리 엔진에 텔레포트 위치 즉시 반영 (1프레임 간극 방지)
            Physics.SyncTransforms();
            // 3. 콜라이더 및 이동 락 해제
            if (_playerCollider != null) _playerCollider.enabled = true;
            if (_movement != null) _movement.MovementLocked = false;
            TargetHideDeathScreen();
            RpcOnRespawn(); // (전체 클라이언트에 부활 애니메이션 브로드캐스트)
        }

        // 부활 시 눕는 모션에서 탈출하여 기본 이동 블렌드트리로 강제 복귀
        [ClientRpc]
        private void RpcOnRespawn()
        {
            if (_movement != null) _movement.MovementLocked = false; // 부활 시 이동 조작 락 해제
            if (_animator != null)
            {
                _animator.ResetTrigger(DeathTrigger);
                _animator.Play("MoveBlendTree", 0, 0f);
            }
        }

        [TargetRpc]
        private void TargetHideDeathScreen() => OnLocalRespawned?.Invoke();

        // NPC 앞으로 순간이동. 목적지 좌표는 서버(GameNetworkManager)가 들고 있다 —
        // 클라가 좌표를 실어 보내면 어디든 갈 수 있는 치트가 된다.
        [Command]
        public void CmdTeleportToNpc()
        {
            var manager = NetworkManager.singleton as GameNetworkManager;
            if (manager == null) return;

            if (_zoneService == null)
                _zoneService = manager.ZoneService;

            _zoneService?.TeleportToPoint(connectionToClient, manager.npcTeleportPoint, manager.defaultSubScene);
        }

        #endregion
    }
}