using System;
using UnityEngine;
using Mirror;

namespace MySecondWorld.Combat.Companion
{
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkMatch))]
    public class CompanionServerState : NetworkBehaviour, ICompanionServerState
    {
        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool _isAlive = true;


        [SyncVar(hook = nameof(OnHPChanged))]
        private float _currentHP;

        private Health _health;
        public bool IsAlive => _isAlive;
        public float CurrentHP => _currentHP;
        public event Action<float> HPChanged; // CompanionHealthBarUI가 구독

        public override void OnStartServer()
        {
            _health = GetComponent<Health>();
            _currentHP = _health.CurrentHP;
            _health.OnDamaged += HandleDamaged;
        }

        public override void OnStopServer()
        {
            if (_health != null)
                _health.OnDamaged -= HandleDamaged;
        }

        [Server]
        private void HandleDamaged(float newCurrentHP) => _currentHP = newCurrentHP;

        // CompanionAI.PlayDeathSequence()가 죽음 애니메이션 재생 완료 후 호출
        [Server]
        public void MarkDead() => _isAlive = false;

        // CompanionFactory가 서버에서만 호출 — CompanionAI 쪽으로 위임(같은 Combat 어셈블리라 직접 참조 가능)
        [Server]
        public void Initialize(Transform ownerTransform) => GetComponent<CompanionAI>().Initialize(ownerTransform);

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

        // 테스트용 즉사기 
        [Server]
        public void DebugKill() => GetComponent<Health>().TakeDamage(99999f);

        private void OnAliveChanged(bool _, bool newValue)
        {
            this.gameObject.SetActive(newValue);

            if (!isServer) return; // AI 판단은 서버 전용 — 클라이언트 로컬 사본은 절대 켜면 안 됨

            var ai = GetComponent<CompanionAI>();
            var hitbox = GetComponent<CompanionHitbox>();
            if (ai != null) ai.enabled = newValue;
            if (hitbox != null) hitbox.enabled = newValue;
        }

        private void OnHPChanged(float _, float newValue)
        {
            HPChanged?.Invoke(newValue);
        }
    }
}