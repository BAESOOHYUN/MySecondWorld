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

        [Server]
        private void HandleDied()
        {
            if (_movement != null) _movement.MovementLocked = true;
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

                if (_networkAnimator != null)
                    _networkAnimator.SetTrigger(DeathTrigger);

                await UniTask.WhenAny(
                    _deathAnimationTcs.Task,
                    UniTask.Delay(TimeSpan.FromSeconds(DeathAnimationTimeoutSeconds), cancellationToken: ct));

                TargetShowDeathScreen();
            }
            catch (OperationCanceledException)
            {
                // 대기 중 오브젝트가 파괴됨 — 정상 흐름
            }
        }

        [TargetRpc]
        private void TargetShowDeathScreen() => OnLocalDied?.Invoke();

        [Command]
        public void CmdRequestRespawn()
        {
            if (!_health.IsDead) return; // 살아있는데 리스폰 요청은 무시(조작 방지)

            _health.ReviveFull();
            _currentHP = _health.CurrentHP;

            _zoneService?.RespawnPlayer(connectionToClient);

            if (_movement != null) _movement.MovementLocked = false;
            TargetHideDeathScreen();
        }

        [TargetRpc]
        private void TargetHideDeathScreen() => OnLocalRespawned?.Invoke();
    }
}