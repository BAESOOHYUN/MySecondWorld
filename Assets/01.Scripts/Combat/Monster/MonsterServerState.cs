using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using MySecondWorld.Foundation;
using MySecondWorld.Combat.Vfx;

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
        [Header("VFX")]
        [SerializeField] private CombatVfxDefaultsSO _vfxDefaults;

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

        // 몸통 중심의 월드 좌표. 원거리 조준점으로 쓴다 — 피벗(발밑)에 고정 오프셋을 더하면
        // 몬스터 종류마다 어긋난다(슬라임은 머리 위로, 드래곤은 발목으로).
        public Vector3 BodyCenterWorld =>
            _bodyCenter.sqrMagnitude > 0.001f
                ? transform.TransformPoint(_bodyCenter)
                : transform.position + Vector3.up * 1.0f;

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

            // MonsterAI의 RequireComponent가 스폰 전에 붙여주므로 지금 경로에선 항상 있다.
            // 다른 경로로 스폰하게 되면 조용히 폴백 크기로 떨어지므로 알려준다.
            if (TryGetComponent(out CapsuleCollider body))
            {
                _bodyCenter = body.center;
                _bodyRadius = body.radius;
                _bodyHeight = body.height;
            }
            else
            {
                Debug.LogWarning($"[MonsterServerState] CapsuleCollider가 없어 탭 판정 캡슐이 " +
                                 $"기본 크기로 만들어집니다 — {name}", this);
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

            // 풀 재사용 시 직전 생애에서 맞았던 Hit/Death 트리거 잔여물로 인해 스폰 즉시 피격 모션이 도는 현상 방지
            var anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
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
        private void HandleDamaged(float newCurrentHP)
        {
            _currentHP = newCurrentHP;
            RpcPlayHitVfx(BodyCenterWorld);
        }

        [Server]
        private void HandleDied()
        {
            _isAlive = false;
            // 만약 서버 전용(단독 서버) 모드인 경우 SyncVar 훅이 호출되지 않으므로 보장
            if (!isClient)
            {
                Died?.Invoke();
            }
            RpcPlayDeathVfx(BodyCenterWorld);
        }

        [ClientRpc]
        private void RpcPlayHitVfx(Vector3 position)
        {
            if (_vfxDefaults == null || _vfxDefaults.HitVfxPrefab == null) return;
            CombatVfxSpawner.Spawn(_vfxDefaults.HitVfxPrefab, position, Quaternion.identity);
        }

        [ClientRpc]
        private void RpcPlayDeathVfx(Vector3 position)
        {
            if (_vfxDefaults == null || _vfxDefaults.DeathVfxPrefab == null) return;
            CombatVfxSpawner.Spawn(_vfxDefaults.DeathVfxPrefab, position, Quaternion.identity);
        }

        private void OnHPValueChanged(float oldValue, float newValue) => HPChanged?.Invoke();

        private void OnAliveChanged(bool oldValue, bool newValue)
        {
            // 죽은 몸이 뒤에 살아있는 몬스터를 가리지 않도록 판정 캡슐 자체를 꺼버린다.
            _tapTarget?.SetTappable(newValue);
            if (!newValue) Died?.Invoke();
        }
    }
}
