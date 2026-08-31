using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using MySecondWorld.Foundation;

namespace MySecondWorld.Combat.Companion
{
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkMatch))]
    public class CompanionServerState : NetworkBehaviour, ICompanionServerState, IHealthView
    {
        // 비율이 아니라 절대값 두 개를 나른다 — "78/100" 숫자 표시가 필요해서다.
        [SyncVar(hook = nameof(OnHPChanged))]
        private float _currentHP;

        [SyncVar(hook = nameof(OnHPChanged))]
        private float _maxHP = 1f;   // 0이면 UI의 나눗셈이 터진다

        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool _isAlive = true;

        // 컴패니언은 소유권 없이 스폰되므로(NetworkServer.Spawn에 conn 미전달),
        // 클라이언트가 "내 컴패니언"을 가릴 단서가 이것뿐이다.
        [SyncVar(hook = nameof(OnOwnerChanged))]
        private uint _ownerNetId;

        private Health _health;

        public Transform Anchor => transform;
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsAlive => _isAlive;

        public event Action HPChanged;   // 값 없이 통보만 — 현재/최대를 UI가 다시 읽는다

        // 사망·소환해제는 디스폰이 아니라 SetActive(false)라서 OnStopClient가 안 불린다.
        // UI가 바를 숨기려면 이 신호가 필요하다.
        public event Action<bool> AliveChanged;

        public static CompanionServerState LocalCompanion { get; private set; }
        public static event Action<CompanionServerState> LocalCompanionReady;
        public static event Action LocalCompanionLost;

        // Domain Reload OFF — static이 플레이 세션을 넘어 살아남으면 파괴된 객체를 가리키고,
        // 구독도 그대로 남아 지난 세션의 UI가 계속 불린다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LocalCompanion = null;
            LocalCompanionReady = null;
            LocalCompanionLost = null;
        }

        public override void OnStartServer()
        {
            _health = GetComponent<Health>();
            _maxHP = _health.MaxHP;
            _currentHP = _health.CurrentHP;

            _health.OnDamaged += HandleDamaged;
        }

        public override void OnStopServer()
        {
            if (_health != null)
                _health.OnDamaged -= HandleDamaged;
        }

        public override void OnStartClient() => WaitForLocalPlayerThenMark().Forget();

        public override void OnStopClient()
        {
            if (LocalCompanion != this) return;

            LocalCompanion = null;
            LocalCompanionLost?.Invoke();
        }

        #region ICompanionServerState — 서버 전용 조작

        // CompanionFactory가 서버에서만 호출 — CompanionAI 쪽으로 위임(같은 Combat 어셈블리라 직접 참조 가능)
        [Server]
        public void Initialize(Transform ownerTransform)
        {
            if (ownerTransform != null && ownerTransform.TryGetComponent(out NetworkIdentity owner))
                _ownerNetId = owner.netId;

            GetComponent<CompanionAI>().Initialize(ownerTransform);
        }

        // CompanionAI.PlayDeathSequence()가 죽음 애니메이션 재생 완료 후 호출
        [Server]
        public void MarkDead() => _isAlive = false;

        [Server]
        public void Revive(Vector3 position)
        {
            if (_isAlive) return;

            // Warp 전에 먼저 켜야 NavMeshAgent가 동작함(Health.OnEnable()도 여기서 같이 실행돼 CurrentHP가 MaxHP로 리셋됨)
            this.gameObject.SetActive(true);
            var ai = GetComponent<CompanionAI>();
            ai.Warp(position);
            ai.ResetForRevive();
            _currentHP = _health.MaxHP;
            _isAlive = true;
        }

        [Server]
        public void ToggleFollowPaused()
        {
            var ai = GetComponent<CompanionAI>();
            if (ai.IsFollowPaused) ai.ResumeFollow();
            else ai.PauseFollow();
        }

        // 테스트용 즉사기
        [Server]
        public void DebugKill() => GetComponent<Health>().TakeDamage(99999f);

        [Server]
        private void HandleDamaged(float newCurrentHP) => _currentHP = newCurrentHP;

        #endregion

        #region SyncVar 훅

        private void OnHPChanged(float oldValue, float newValue) => HPChanged?.Invoke();

        private void OnAliveChanged(bool oldValue, bool newValue)
        {
            // SetActive(false)보다 먼저 알린다 — 나중에 자식 UI가 구독해도 안전하도록.
            AliveChanged?.Invoke(newValue);

            this.gameObject.SetActive(newValue);

            if (!isServer) return; // AI 판단은 서버 전용 — 클라이언트 로컬 사본은 절대 켜면 안 됨

            var ai = GetComponent<CompanionAI>();
            var hitbox = GetComponent<CompanionHitbox>();
            if (ai != null) ai.enabled = newValue;
            if (hitbox != null) hitbox.enabled = newValue;
        }

        // Initialize가 Spawn 뒤에 불리므로 _ownerNetId는 스폰 메시지가 아니라 나중에 도착한다.
        // OnStartClient 한 번만으로는 놓치기 때문에 훅에서도 다시 본다.
        private void OnOwnerChanged(uint oldValue, uint newValue) => TryMarkLocal();

        #endregion

        #region "내 컴패니언" 판정

        // 로컬 플레이어가 컴패니언보다 늦게 스폰될 수 있다. 그 경우 OnStartClient 시점엔
        // NetworkClient.localPlayer가 null이라 판정이 조용히 실패하고, 훅도 이미 지나가서
        // 다시 볼 기회가 없어진다 — HP바가 영원히 안 붙는다. 준비될 때까지 기다렸다 판정한다.
        private async UniTaskVoid WaitForLocalPlayerThenMark()
        {
            try
            {
                await UniTask.WaitUntil(() => NetworkClient.localPlayer != null,
                                        cancellationToken: this.GetCancellationTokenOnDestroy());
                TryMarkLocal();
            }
            catch (OperationCanceledException)
            {
                // 기다리는 중 파괴됨 — 정상 흐름
            }
        }

        private void TryMarkLocal()
        {
            if (LocalCompanion == this) return;
            if (_ownerNetId == 0 || NetworkClient.localPlayer == null) return;
            if (_ownerNetId != NetworkClient.localPlayer.netId) return;

            LocalCompanion = this;
            LocalCompanionReady?.Invoke(this);
        }

        #endregion
    }
}
