using System;
using Mirror;
using UnityEngine;
using MySecondWorld.Foundation;

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
        // 비율이 아니라 절대값 두 개를 나른다 — "78/100" 숫자 표시가 필요해서다.
        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _currentHP;

        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _maxHP = 1f;   // 0이면 UI의 나눗셈이 터진다

        private Health _health;

        public Transform Anchor => transform;      // 화면 고정 바에서는 안 쓰이지만 계약이라 채운다
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public event Action HPChanged;

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

        public override void OnStartServer()
        {
            _health = GetComponent<Health>();
            _maxHP = _health.MaxHP;
            _currentHP = _health.CurrentHP;

            _health.OnDamaged += HandleDamaged;
        }

        public override void OnStopServer()
        {
            if (_health != null) _health.OnDamaged -= HandleDamaged;
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
    }
}