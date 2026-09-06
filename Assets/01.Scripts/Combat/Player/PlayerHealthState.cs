using System;
using Mirror;
using UnityEngine;
using MySecondWorld.Foundation;
using MySecondWorld.Move;
using MySecondWorld.Network;
using Cysharp.Threading.Tasks;
using VContainer;

namespace MySecondWorld.Combat.Player
{
    /// <summary>
    /// 플레이어 HP를 클라이언트로 동기화한다. PlayerBase 프리팹에 부착.
    /// Health(서버 권위 계산)와 분리하는 구조는 Monster/CompanionServerState와 같다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class PlayerHealthState : NetworkBehaviour, IHealthView
    {
        [Header("사망 처리")]
        [SerializeField] private MovementController _movement;
        [SerializeField] private NetworkAnimator _networkAnimator;
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider _playerCollider;

        private static readonly int DeathTrigger = Animator.StringToHash(nameof(DeathTrigger));
        private const float DeathAnimationTimeoutSeconds = 3f; // Animation Event 배선 누락 시 안전장치

        // 비율이 아니라 절대값 두 개를 나른다 — "78/100" 숫자 표시가 필요해서다.
        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _currentHP;

        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _maxHP = 1f;   // 0이면 UI의 나눗셈이 터진다

        private Health _health;
        private ServerZoneService _zoneService;
        private UniTaskCompletionSource _deathAnimationTcs;

        public Transform Anchor => transform;      // 화면 고정 바에서는 안 쓰이지만 계약이라 채운다
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public event Action HPChanged;
        public event Action OnLocalDied;
        public event Action OnLocalRespawned;

        // 화면 HUD는 플레이어보다 먼저 살아 있으므로, UI가 "내 플레이어 나왔니"를 알 수단이 필요하다.
        public static PlayerHealthState Local { get; private set; }
        public static event Action<PlayerHealthState> LocalReady;

        // Domain Reload OFF — static이 플레이 세션을 넘어 살아남으면 파괴된 객체를 가리킨다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            LocalReady = null;
        }

        [Inject]
        public void Construct(ServerZoneService zoneService)
        {
            _zoneService = zoneService;
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
            if (_movement == null) _movement = GetComponent<MovementController>();
        }

        public override void OnStartServer()
        {
            _health = GetComponent<Health>();
            _maxHP = _health.MaxHP;
            _currentHP = _health.CurrentHP;

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

        [Server]
        private void HandleDamaged(float newCurrentHP) => _currentHP = newCurrentHP;

        private void OnHPValueChanged(float oldValue, float newValue) => HPChanged?.Invoke();

        #region 사망 시퀀스

        [Server]
        private void HandleDied()
        {
            if (_movement != null) _movement.MovementLocked = true;
            if (_playerCollider != null) _playerCollider.enabled = false;
            PlayDeathSequence().Forget();
        }

        // Death 클립 마지막 프레임 근처에 Animation Event로 연결
        public void OnDeathAnimationEnd() => _deathAnimationTcs?.TrySetResult();

        private async UniTaskVoid PlayDeathSequence()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                _deathAnimationTcs = new UniTaskCompletionSource();

                // 1. 오너 클라이언트에게 사망 애니메이션 트리거 위임
                TargetPlayDeathAnimation(connectionToClient);
                // 2. 애니메이션 종료 또는 3초 타임아웃 대기
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
            if (_movement != null) _movement.MovementLocked = true; //죽었을 때 내 이동 조작 락
            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(DeathTrigger);
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

        #endregion
    }
}