using System;
using UnityEngine;
using Mirror;

namespace MySecondWorld.Combat.Companion
{
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkMatch))]
    public class CompanionServerState : NetworkBehaviour
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

        [Server]
        public void Revive(Vector3 position)
        {
            if (!_isAlive) return;

            // Warp 전에 먼저 켜야 NavMeshAgent가 동작함(Health.OnEnable()도 여기서 같이 실행돼 CurrentHP가 MaxHP로 리셋됨)
            this.gameObject.SetActive(true);
            GetComponent<CompanionAI>().Warp(position);
            _currentHP = _health.MaxHP;
            _isAlive = true;
        }

        private void OnAliveChanged(bool _, bool newValue)
        {
            var ai = GetComponent<CompanionAI>();
            var hitbox = GetComponent<CompanionHitbox>();
            if (ai != null) ai.enabled = newValue;
            if (hitbox != null) hitbox.enabled = newValue;
            this.gameObject.SetActive(newValue);
        }

        private void OnHPChanged(float _, float newValue)
        {
            HPChanged?.Invoke(newValue);
        }
    }
}