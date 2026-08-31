using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using MySecondWorld.Foundation;

namespace MySecondWorld.Combat.Monster
{
    /// <summary>
    /// 몬스터의 생존/HP/몸통치수를 클라이언트로 동기화한다. 순수 프리팹에 authoring한다.
    /// Health(서버 권위 계산)와 분리하는 구조는 CompanionServerState와 같다.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [DisallowMultipleComponent]
    public class MonsterServerState : NetworkBehaviour, IHealthView
    {
        // 비율이 아니라 절대값 두 개를 나른다 — "78/100" 같은 숫자 표시가 필요해서다.
        // MaxHP는 MonsterStatsSO(서버 전용)에서 오므로 클라가 알 방법이 이것뿐이다.
        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _currentHP;

        [SyncVar(hook = nameof(OnHPValueChanged))]
        private float _maxHP = 1f;   // 0이면 UI의 나눗셈이 터진다

        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool _isAlive = true;

        // 몸통 캡슐 치수. 클라 사본엔 MonsterAI도 MonsterStatsSO도 없어서
        // 자기 몸 크기를 알 방법이 이것뿐이다 — 탭 판정 캡슐을 이 값으로 만든다.
        // 스폰 메시지에 실려 오므로 OnStartClient 시점엔 이미 채워져 있다.
        [SyncVar] private Vector3 _bodyCenter;
        [SyncVar] private float _bodyRadius;
        [SyncVar] private float _bodyHeight;

        private MonsterTapTarget _tapTarget;
        private Health _health;

        public Transform Anchor => transform;
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsAlive => _isAlive;

        public event Action HPChanged;
        public event Action Died;

        // 클라이언트에 살아있는 몬스터 목록. HP바를 몇 개 띄울지 UI가 알아야 해서 노출한다.
        private static readonly List<MonsterServerState> _activeOnClient = new();
        public static IReadOnlyList<MonsterServerState> ActiveOnClient => _activeOnClient;

        public static event Action<MonsterServerState> ClientSpawned;
        public static event Action<MonsterServerState> ClientDespawned;

        // Domain Reload가 꺼져 있어 목록과 구독이 플레이 세션을 넘어 살아남는다.
        // 초기화 안 하면 두 번째 플레이에서 파괴된 몬스터가 목록에 쌓인다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _activeOnClient.Clear();
            ClientSpawned = null;
            ClientDespawned = null;
        }

        private void Awake() => _tapTarget = GetComponent<MonsterTapTarget>();

        public override void OnStartServer()
        {
            // 풀에서 재사용된 인스턴스는 직전 개체의 값을 들고 있다. 스폰마다 되돌린다.
            _isAlive = true;

            _health = GetComponent<Health>();   // MonsterFactory가 서버에서 붙인 것
            if (_health == null)
            {
                Debug.LogError($"[MonsterServerState] Health를 찾지 못했습니다 — {name}", this);
                return;
            }

            // Health.OnEnable이 이미 CurrentHP를 MaxHP로 채운 뒤다.
            _maxHP = _health.MaxHP;
            _currentHP = _health.CurrentHP;

            if (TryGetComponent(out CapsuleCollider body))
            {
                _bodyCenter = body.center;
                _bodyRadius = body.radius;
                _bodyHeight = body.height;
            }

            _health.OnDamaged += HandleDamaged;
            _health.OnDied += HandleDied;
        }

        public override void OnStopServer()
        {
            if (_health == null) return;

            _health.OnDamaged -= HandleDamaged;
            _health.OnDied -= HandleDied;
            _health = null;
        }

        // SyncVar 초기값은 스폰 메시지에 함께 오므로 여기서 바로 쓸 수 있다.
        public override void OnStartClient()
        {
            if (_tapTarget != null)
            {
                _tapTarget.BuildTapCollider(_bodyCenter, _bodyRadius, _bodyHeight);
                _tapTarget.SetTappable(_isAlive);
            }

            // 풀 재사용 시 같은 인스턴스가 다시 스폰되므로 중복 등록을 막는다.
            if (!_activeOnClient.Contains(this))
            {
                _activeOnClient.Add(this);
                ClientSpawned?.Invoke(this);
            }
        }

        public override void OnStopClient()
        {
            if (_activeOnClient.Remove(this))
                ClientDespawned?.Invoke(this);
        }

        [Server]
        private void HandleDamaged(float newCurrentHP) => _currentHP = newCurrentHP;

        [Server]
        private void HandleDied() => _isAlive = false;

        private void OnHPValueChanged(float oldValue, float newValue) => HPChanged?.Invoke();

        private void OnAliveChanged(bool oldValue, bool newValue)
        {
            // 죽은 몸이 뒤에 살아있는 몬스터를 가리지 않도록 판정 캡슐 자체를 꺼버린다.
            _tapTarget?.SetTappable(newValue);
            if (!newValue) Died?.Invoke();
        }
    }
}
