using System;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    /// <summary>
    /// 몬스터의 생존/HP/몸통치수를 클라이언트로 동기화한다. 순수 프리팹에 authoring한다.
    /// Health(서버 권위 계산)와 분리하는 구조는 CompanionServerState와 같다.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [DisallowMultipleComponent]
    public class MonsterServerState : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool _isAlive = true;

        // 비율로 나르는 이유: MaxHP는 MonsterStatsSO에서 오는데 그 Initialize가 서버에서만 돈다.
        // 클라 사본의 Health._maxHP는 서버 값과 같다는 보장이 없다.
        [SyncVar(hook = nameof(OnHPChanged))]
        private float _hpNormalized = 1f;

        // 몸통 캡슐 치수. 클라 사본엔 MonsterAI도 MonsterStatsSO도 없어서
        // 자기 몸 크기를 알 방법이 이것뿐이다 — 탭 판정 캡슐을 이 값으로 만든다.
        // 스폰 메시지에 실려 오므로 OnStartClient 시점엔 이미 채워져 있다.
        [SyncVar] private Vector3 _bodyCenter;
        [SyncVar] private float _bodyRadius;
        [SyncVar] private float _bodyHeight;

        private Health _health;
        private MonsterTapTarget _tapTarget;

        public bool IsAlive => _isAlive;
        public float HPNormalized => _hpNormalized;

        public event Action<float> HPChanged;   // 3단계 HP바 UI가 구독
        public event Action Died;               // 4단계 선택 해제가 구독

        private void Awake() => _tapTarget = GetComponent<MonsterTapTarget>();

        public override void OnStartServer()
        {
            // 풀에서 재사용된 인스턴스는 직전 개체의 값을 들고 있다. 스폰마다 되돌린다.
            _isAlive = true;
            _hpNormalized = 1f;

            // MonsterAI.Initialize가 스탯 SO 값으로 이미 맞춰놓은 캡슐을 그대로 읽는다.
            if (TryGetComponent(out CapsuleCollider body))
            {
                _bodyCenter = body.center;
                _bodyRadius = body.radius;
                _bodyHeight = body.height;
            }

            _health = GetComponent<Health>();   // 서버에서 MonsterFactory가 붙인 것
            if (_health == null)
            {
                Debug.LogError($"[MonsterServerState] Health를 찾지 못했습니다 — {name}", this);
                return;
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
            if (_tapTarget == null) return;
            _tapTarget.BuildTapCollider(_bodyCenter, _bodyRadius, _bodyHeight);
            _tapTarget.SetTappable(_isAlive);
        }

        [Server]
        private void HandleDamaged(float newCurrentHP)
        {
            if (_health.MaxHP > 0f)
                _hpNormalized = newCurrentHP / _health.MaxHP;
        }

        [Server]
        private void HandleDied() => _isAlive = false;

        private void OnAliveChanged(bool oldValue, bool newValue)
        {
            // 죽은 몸이 뒤에 살아있는 몬스터를 가리지 않도록 판정 캡슐 자체를 꺼버린다.
            _tapTarget?.SetTappable(newValue);
            if (!newValue) Died?.Invoke();
        }

        private void OnHPChanged(float oldValue, float newValue) => HPChanged?.Invoke(newValue);
    }
}