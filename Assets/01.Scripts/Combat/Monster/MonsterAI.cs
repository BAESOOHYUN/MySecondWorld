using UnityEngine;
using UnityEngine.AI;
using Stateless;
using Cysharp.Threading.Tasks;
using System;

namespace MySecondWorld.Combat.Monster
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Health))]
    public class MonsterAI : MonoBehaviour
    {
        #region State Machine 정의

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

        #endregion

        #region Fields

        [SerializeField] protected MonsterStatsSO _stats;
        [SerializeField] private SphereCollider _detectionTrigger;
        [SerializeField] protected Animator _animator;

        private static readonly int AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));
        private static readonly int DeathTrigger = Animator.StringToHash(nameof(DeathTrigger));

        protected NavMeshAgent _agent;
        protected Health _health;
        private StateMachine<MonsterState, MonsterTrigger> _stateMachine;
        protected ISkillSelector _skillSelector;
        private readonly AIContext _aiContext = new AIContext();
        private Transform _target;

        // RangedMonsterHitbox가 조준점으로 읽어감. 타겟이 없으면 null.
        public Vector3? CurrentTargetPosition => _target != null ? (Vector3?)_target.position : null;

        #endregion

        #region Unity 생명주기

        protected virtual void Awake()
        {
            if (_stats == null)
            {
                Debug.LogError($"MonsterStatsSO missing on {gameObject.name}", this);
                enabled = false;
                return;
            }

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

            // 스탯 SO 타입으로 몹 종류 판별
            _skillSelector = CreateDefaultSkillSelector();

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
            OnTick();
            if (IsUpdatePaused) return;

            switch (_stateMachine.State)
            {
                case MonsterState.Idle:
                    OnIdleUpdate();
                    break;
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

            bool wasIdle = _stateMachine.State == MonsterState.Idle;
            SafeFire(MonsterTrigger.PlayerDetected);
            if (wasIdle)
                OnPlayerFirstDetected();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            SafeFire(MonsterTrigger.PlayerLost);
            _target = null;
        }

        #endregion

        #region State Machine 동작

        private void InitStateMachine()
        {
            _stateMachine = new StateMachine<MonsterState, MonsterTrigger>(MonsterState.Idle, FiringMode.Queued);

            _stateMachine.Configure(MonsterState.Idle)
                .OnEntry(() => { _agent.isStopped = true; OnEnterIdle(); })
                .Permit(MonsterTrigger.PlayerDetected, MonsterState.Chase)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Chase)
                .OnEntry(() => { _agent.isStopped = false; _agent.updateRotation = true; })
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
            float distance = Vector3.Distance(transform.position, _target.position);
            if (distance <= _stats.AttackRange)
                SafeFire(MonsterTrigger.TargetInRange);
        }

        private void UpdateAttack()
        {
            if (_target == null) return;

            float distance = Vector3.Distance(transform.position, _target.position);
            // AttackRange보다 확실히 더 멀어져야 Chase로 돌아가게 함 (유격 20%) — 경계에서 떨리는 것 방지
            float exitRange = _stats.AttackRange * 1.2f;
            if (distance > exitRange)
            {
                SafeFire(MonsterTrigger.TargetOutOfRange);
                return;
            }

            _aiContext.DistanceToTarget = distance;
            PlayAttackSkill(_skillSelector.SelectSkill(_aiContext));
        }

        // 스킬 ID -> 실제 모션/이동 처리. 보스는 override해서 자기 트리거로 교체.
        protected virtual void PlayAttackSkill(int? skillId)
        {
            switch (skillId)
            {
                case MonsterSkillId.Retreat: // 후퇴
                    {
                        _agent.isStopped = false;
                        _agent.updateRotation = false; // 이동 방향이 아니라 타겟을 계속 보게 하려고 자동회전 끔
                        Vector3 awayDirection = (this.transform.position - _target.position).normalized;
                        _agent.SetDestination(this.transform.position + awayDirection * _stats.AttackRange); // 공격범위 밖으로 후퇴.
                        RotateTowardsTarget(); // 후퇴하는 매 프레임 타겟 쪽을 계속 바라봄
                    }
                    break;
                case MonsterSkillId.MeleeAttack:
                case MonsterSkillId.RangedShoot:
                    {
                        FaceTarget();
                        if (_animator != null)
                            _animator.SetTrigger(AttackTrigger);
                    }
                    break;
                // null: 쿨다운 대기, 아무것도 안 함
            }
        }

        // NavMeshAgent는 정지하면(isStopped = true) 회전도 같이 멈춰서 엉뚱한 방향을 보게 됨 →
        // 정지해서 공격하는 순간에만 타겟 쪽으로 직접 회전시켜줌
        protected void FaceTarget()
        {
            _agent.isStopped = true;
            RotateTowardsTarget();

        }
        private void RotateTowardsTarget()
        {
            Vector3 lookDirection = _target.position - transform.position;
            lookDirection.y = 0f; // 위아래로 기울지 않게 수평 방향만
            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }
        #endregion

            #region 사망 처리

        private void HandleDied() => SafeFire(MonsterTrigger.Died);

        private void HandleEnterDead()
        {
            _agent.isStopped = true;
            PlayDeathSequence().Forget();
        }

        private async UniTaskVoid PlayDeathSequence()
        {
            if (_animator != null)
                _animator.SetTrigger(GetDeathTriggerHash());
            await UniTask.Delay(TimeSpan.FromSeconds(_stats.DeathDuration));
            this.gameObject.SetActive(false);
        }

        #endregion

        #region 유틸리티

        private bool SafeFire(MonsterTrigger trigger)
        {
            if (!_stateMachine.CanFire(trigger)) return false;
            _stateMachine.Fire(trigger);
            return true;
        }

        #endregion

        #region 하위 클래스(BossAI 등) 확장 지점

        protected virtual ISkillSelector CreateDefaultSkillSelector()
            => _stats is RangedMonsterStatsSO rangedStats
                ? new RangedMobSkillSelector(rangedStats.RetreatDistance, rangedStats.AttackCooldown)
                : new MeleeMobSkillSelector(_stats.AttackCooldown);

        protected virtual int GetDeathTriggerHash() => DeathTrigger;
        protected virtual void OnTick() { }
        protected virtual bool IsUpdatePaused => false;
        protected virtual void OnIdleUpdate() { }
        protected virtual void OnEnterIdle() { }
        protected virtual void OnPlayerFirstDetected() { }

        #endregion
    }
}
