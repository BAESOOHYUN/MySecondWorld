using UnityEngine;
using UnityEngine.AI;
using Stateless;

namespace MySecondWorld.Combat.Monster
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class MonsterAI : MonoBehaviour
    {
        private enum MonsterState
        {
            Idle,
            Chase,
            Attack,
            Dead,
        }
        private enum MonsterTrigger
        {
            PlayerDetected,
            PlayerLost,
            TargetInRange,
            TargetOutOfRange,
            Died,
        }
        [SerializeField] private MonsterStatsSO _stats;
        [SerializeField] private SphereCollider _detectionTrigger;
        [SerializeField] private Animator _animator;

        private static readonly int AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));

        private NavMeshAgent _agent;
        private Health _health;
        private StateMachine<MonsterState, MonsterTrigger> _stateMachine;
        private ISkillSelector _skillSelector;
        private readonly AIContext _aiContext = new AIContext();
        private Transform _target;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            // Health.OnEnable()보다 먼저 실행돼야 MaxHP가 정확히 반영됨
            // (같은 오브젝트의 모든 컴포넌트는 Awake가 전부 끝난 뒤에야 OnEnable이 실행되므로 안전)
            _health.Initialize(_stats.MaxHP);
            _agent.speed = _stats.MoveSpeed;

            if (_detectionTrigger != null)
            {
                _detectionTrigger.isTrigger = true;
                _detectionTrigger.radius = _stats.DetectRange;
            }

            // TODO : 몬스터 추가시 외부에서 어떤 selector를 쓸지 정해서 넣어주는 방식으로 변경
            _skillSelector = new MeleeMobSelector(_stats.AttackCooldown);

            InitStateMachine();
        }

        private void OnEnable()
        {
            _health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            _health.OnDied -= HandleDied;
        }

        private void Update()
        {
            switch (_stateMachine.State)
            {
                case MonsterState.Chase:
                    UpdateChase();
                    break;
                case MonsterState.Attack:
                    UpdateAttack();
                    break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _target = other.transform;
            SafeFire(MonsterTrigger.PlayerDetected);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            SafeFire(MonsterTrigger.PlayerLost);
            _target = null;
        }

        private void InitStateMachine()
        {
            _stateMachine = new StateMachine<MonsterState, MonsterTrigger>(MonsterState.Idle, FiringMode.Queued);

            _stateMachine.Configure(MonsterState.Idle)
                .OnEntry(() => _agent.isStopped = true)
                .Permit(MonsterTrigger.PlayerDetected, MonsterState.Chase)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Chase)
                .OnEntry(() => _agent.isStopped = false)
                .Permit(MonsterTrigger.PlayerLost, MonsterState.Idle)
                .Permit(MonsterTrigger.TargetInRange, MonsterState.Attack)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Attack)
                .OnEntry(() => _agent.isStopped = true)
                .Permit(MonsterTrigger.TargetOutOfRange, MonsterState.Chase)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Dead)
                .OnEntry(HandleEnterDead);

            _stateMachine.OnTransitioned(t => Debug.Log($"[MonsterAI] {t.Source} → {t.Destination} ({t.Trigger})"));
        }

        private void UpdateChase()
        {
            if (_target == null) return;

            _agent.SetDestination(_target.position);

            if (Vector3.Distance(transform.position, _target.position) <= _stats.AttackRange)
                SafeFire(MonsterTrigger.TargetInRange);
        }

        private void UpdateAttack()
        {
            if (_target == null) return;

            if (Vector3.Distance(transform.position, _target.position) > _stats.AttackRange)
            {
                SafeFire(MonsterTrigger.TargetOutOfRange);
                return;
            }

            if (_skillSelector.SelectSkill(_aiContext) != null && _animator != null)
                _animator.SetTrigger(AttackTrigger);
        }

        private void HandleDied() => SafeFire(MonsterTrigger.Died);
        // PLAN: 사망 시 처리는 비활성화만 (루팅/보상/이펙트는 제외)
        private void HandleEnterDead()
        {
            _agent.isStopped = true;
            gameObject.SetActive(false);
        }

        private void SafeFire(MonsterTrigger trigger)
        {
            if (_stateMachine.CanFire(trigger))
                _stateMachine.Fire(trigger);
        }
    }
}