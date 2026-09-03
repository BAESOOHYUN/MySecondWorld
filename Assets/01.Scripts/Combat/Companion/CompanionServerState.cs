using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using MySecondWorld.Foundation;
using MySecondWorld.Network;

namespace MySecondWorld.Combat.Companion
{
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkMatch))]
    public class CompanionServerState : NetworkBehaviour, ICompanionServerState, IHealthView
    {
        [SyncVar(hook = nameof(OnHPChanged))]
        private float _currentHP;

        [SyncVar(hook = nameof(OnHPChanged))]
        private float _maxHP = 1f;

        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool _isAlive = true;

        // 소환/소환해제 — 생사와 별개다. 죽은 채로 소환해제하면 다음에 다시 불러도 죽은 채로 나온다.
        [SyncVar(hook = nameof(OnPresentChanged))]
        private bool _isPresent = true;

        [SyncVar(hook = nameof(OnOwnerChanged))]
        private uint _ownerNetId;

        private Health _health;

        public Transform Anchor => transform;
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsAlive => _isAlive;
        public bool IsPresent => _isPresent;

        public event Action HPChanged;
        public event Action<bool> AliveChanged;

        public static CompanionServerState LocalCompanion { get; private set; }
        public static event Action<CompanionServerState> LocalCompanionReady;
        public static event Action LocalCompanionLost;

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

        public override void OnStartClient()
        {
            // 목적지 씬 활성화 이벤트 구독
            ClientSceneSyncService.OnDestinationSceneActivated += HandleDestinationSceneActivated;
            WaitForLocalPlayerThenMark().Forget();
        }

        public override void OnStopClient()
        {
            // 구독 해제
            ClientSceneSyncService.OnDestinationSceneActivated -= HandleDestinationSceneActivated;
            if (LocalCompanion != this) return;
            LocalCompanion = null;
            LocalCompanionLost?.Invoke();
        }

        // 목적지 씬이 활성화되면 컴패니언 스스로 새 씬으로 이사
        private void HandleDestinationSceneActivated(Scene newScene)
        {
            if (LocalCompanion == this && gameObject.scene != newScene && newScene.IsValid() && newScene.isLoaded)
            {
                if (transform.parent != null)
                    transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(gameObject, newScene);
                Debug.Log($"[Companion] 로컬 컴패니언이 {newScene.name}으로 안전하게 이사 완료!");
            }
        }

        #region ICompanionServerState — 서버 전용 조작

        [Server]
        public void Initialize(Transform ownerTransform)
        {
            if (ownerTransform != null && ownerTransform.TryGetComponent(out NetworkIdentity owner))
                _ownerNetId = owner.netId;

            GetComponent<CompanionAI>().Initialize(ownerTransform);
        }

        // 디버그 키 전용 즉사 — Health 데미지 → 사망 애니메이션까지 다 거치는 "진짜 죽음"
        [Server]
        public void DebugKill() => GetComponent<Health>().TakeDamage(99999f);

        // CompanionAI.PlayDeathSequence()가 사망 애니메이션 재생 완료 후 호출 — 실전투 사망.
        // 재접속 시 "죽은 채로 재소환"을 재현할 때도 재사용한다(애니메이션 없이 즉시).
        [Server]
        public void MarkDead() => _isAlive = false;

        [Server]
        public void Revive(Vector3 position)
        {
            if (_isAlive) return;

            var ai = GetComponent<CompanionAI>();
            ai.Warp(position);
            ai.ResetForRevive();
            _currentHP = _health.MaxHP;
            _isAlive = true;
            _isPresent = true; // 소환해제된 채로 부활 요청이 와도 보이도록
        }

        // 초상화 클릭 소환해제 — 생사와 무관하게 그냥 감춘다. 애니메이션도 HP 변화도 없다.
        [Server]
        public void Dismiss()
        {
            _isPresent = false;

            // 서버에서도 즉시 타겟 불가 처리 및 AI 전투 강제 종료
            if (_health == null) _health = GetComponent<Health>();
            _health.SetTargetable(false);
            if (TryGetComponent(out CompanionAI ai))
                ai.ResetToFollow(); // 전투 상태 즉시 해제
        }

        // 초상화 클릭 재소환 — 감췄던 걸 내 위치로 다시 보여준다. 죽어있었다면 죽은 채로 나온다.
        [Server]
        public void Summon(Vector3 position)
        {
            // 1. Warp를 호출하면 내부의 CompanionAI가 플레이어 우측 후방 1.5m 안전 지면을 찾아 이동
            Warp(position);

            // 2. 화면 표시 및 타겟 가능 상태 복구
            _isPresent = true;
            if (_health == null) _health = GetComponent<Health>();
            _health.SetTargetable(true);
        }

        // 포탈/낙하 구조 시 서버 AI 이동 + 네트워크 텔레포트 + 클라이언트 스냅 동기화
        [Server]
        public void Warp(Vector3 position)
        {
            // 1. 서버 AI 네비메시 워프 (CompanionAI.Warp 내부에서 GetSafeOffsetPosition 계산)
            if (TryGetComponent(out CompanionAI ai))
                ai.Warp(position);
            // 2. Mirror NetworkTransform 텔레포트 (보간 버퍼 리셋)
            if (TryGetComponent(out NetworkTransformBase netTransform))
                netTransform.ServerTeleport(position, transform.rotation);
            // 3. 클라이언트 위치 스냅 RPC
            RpcWarp(position);
        }

        [Server]
        public void ToggleFollowPaused()
        {
            var ai = GetComponent<CompanionAI>();
            if (ai.IsFollowPaused) ai.ResumeFollow();
            else ai.PauseFollow();
        }

        // 모든 관전 클라이언트에게 즉시 위치 강제 스냅
        [ClientRpc]
        private void RpcWarp(Vector3 position)
        {
            transform.position = position;
        }

        [Server]
        private void HandleDamaged(float newCurrentHP) => _currentHP = newCurrentHP;

        #endregion

        #region SyncVar 훅

        private void OnHPChanged(float oldValue, float newValue) => HPChanged?.Invoke();

        // 생사만 담당 — 화면에서 감추는 건 OnPresentChanged 몫이다.
        // 죽으면 그 자리에 쓰러진 채 남아있어야 하므로 여기선 렌더러/콜라이더를 건드리지 않는다.
        private void OnAliveChanged(bool oldValue, bool newValue)
        {
            AliveChanged?.Invoke(newValue);

            if (!isServer) return; // AI 판단은 서버 전용

            var ai = GetComponent<CompanionAI>();
            var hitbox = GetComponent<CompanionHitbox>();
            bool activeState = newValue && _isPresent;
            if (ai != null) ai.enabled = activeState;
            if (hitbox != null) hitbox.enabled = activeState;
        }

        // 화면 표시만 담당 — gameObject는 항상 active로 유지한다.
        // SetActive(false)로 껐다 켜면 Health.OnEnable()이 HP를 리셋해버려서 "죽은 채 재소환"이 깨진다.
        private void OnPresentChanged(bool oldValue, bool newValue)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = newValue;
            foreach (var c in GetComponentsInChildren<Collider>(true))
                c.enabled = newValue;

            if (_health == null) _health = GetComponent<Health>();
                _health.SetTargetable(newValue); // 서버/클라 상관없이 즉시 반영

            if (!isServer) return;

            var ai = GetComponent<CompanionAI>();
            var hitbox = GetComponent<CompanionHitbox>();
            bool activeState = newValue && _isAlive;
            if (ai != null) ai.enabled = activeState;
            if (hitbox != null) hitbox.enabled = activeState;
        }

        private void OnOwnerChanged(uint oldValue, uint newValue) => TryMarkLocal();

        #endregion

        #region "내 컴패니언" 판정

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