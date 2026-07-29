using UnityEngine;
using UnityEngine.AI;
using Stateless;
using System.Linq;
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
        private enum MonsterState
        {
            Idle,
            Chase,
            Attack,
            PhaseTransition, // 보스 전용
            Dead,
        }
        private enum MonsterTrigger
        {
            PlayerDetected,
            PlayerLost,
            TargetInRange,
            TargetOutOfRange,
            HpThresholdCrossed,  // 보스 전용
            TransitionComplete,  // 보스 전용
            Died,
        }
        [SerializeField] private MonsterStatsSO _stats;
        [SerializeField] private SphereCollider _detectionTrigger;
        [SerializeField] private Animator _animator;

        private static readonly int AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));
        private static readonly int AoESlamTrigger = Animator.StringToHash(nameof(AoESlamTrigger));
        private static readonly int PhaseTransitionTrigger = Animator.StringToHash(nameof(PhaseTransitionTrigger));

        private NavMeshAgent _agent;
        private Health _health;
        private StateMachine<MonsterState, MonsterTrigger> _stateMachine;
        private ISkillSelector _skillSelector;
        private readonly AIContext _aiContext = new AIContext();
        private Transform _target;

        private BossStatsSO _bossStats; // null이면 보스가 아님
        private int _currentPhaseIndex;


        // RangedMonsterHitbox가 조준점으로 읽어감. 타겟이 없으면 null.
        public Vector3? CurrentTargetPosition => _target != null ? (Vector3?)_target.position : null;


        private void Awake()
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

            // 스탯 SO 타입으로 몹 종류 판별 — 별도 enum 없이 RangedMonsterStatsSO 상속 여부로 분기
            if (_stats is BossStatsSO bossStats)
            {
                _bossStats = bossStats;
                _skillSelector = CreateSelectorForPhase(0);
            }
            else if (_stats is RangedMonsterStatsSO rangedStats)
            {
                _skillSelector = new RangedMobSkillSelector(rangedStats.RetreatDistance, rangedStats.AttackCooldown);
            }
            else if (_stats is MonsterStatsSO)
            {
                _skillSelector = new MeleeMobSkillSelector(_stats.AttackCooldown);
            }

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
            if (_bossStats != null && _stateMachine.State != MonsterState.Dead)
                CheckPhaseTransition();

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
                .Permit(MonsterTrigger.HpThresholdCrossed, MonsterState.PhaseTransition)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Attack)
                .OnEntry(() => _agent.isStopped = true)
                .Permit(MonsterTrigger.TargetOutOfRange, MonsterState.Chase)
                .Permit(MonsterTrigger.HpThresholdCrossed, MonsterState.PhaseTransition)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.PhaseTransition)
                .OnEntry(HandleEnterPhaseTransition)
                .Permit(MonsterTrigger.TransitionComplete, MonsterState.Attack)
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


            switch (_skillSelector.SelectSkill(_aiContext))
            {
                case MonsterSkillId.Retreat: // 후퇴
                    {
                        _agent.isStopped = false;
                        Vector3 awayDirection = (this.transform.position - _target.position).normalized;
                        _agent.SetDestination(this.transform.position + awayDirection * _stats.AttackRange); // 공격범위 밖으로 후퇴.
                        // 이동 중엔 NavMeshAgent의 자동 회전(updateRotation)에 맡김 — 강제로 타겟을 보게 하면 경합함
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
                case MonsterSkillId.AoESlam:
                    {
                        FaceTarget();
                        if (_animator != null)
                            _animator.SetTrigger(AoESlamTrigger);
                    }
                    break;

                    // null: 쿨다운 대기, 아무것도 안 함
            }
        }

        // NavMeshAgent는 정지하면(isStopped = true) 회전도 같이 멈춰서 엉뚱한 방향을 보게 됨 →
        // 정지해서 공격하는 순간에만 타겟 쪽으로 직접 회전시켜줌
        private void FaceTarget()
        {
            _agent.isStopped = true;

            Vector3 lookDirection = _target.position - transform.position;
            lookDirection.y = 0f; // 위아래로 기울지 않게 수평 방향만
            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        private void CheckPhaseTransition()
        {
            int newPhaseIndex = CalculatePhaseIndex(_health.CurrentHP / _health.MaxHP);
            if (newPhaseIndex == _currentPhaseIndex) return;

            // 실제로 발동에 성공했을 때만 인덱스 갱신 (Idle 등에서 씹히면 다음 프레임에 다시 시도)
            if (SafeFire(MonsterTrigger.HpThresholdCrossed))
                _currentPhaseIndex = newPhaseIndex;
        }

        private int CalculatePhaseIndex(float hpRatio)
        {
            // PhaseHpThresholds : 오름차순 배열
            // HP비율이 이 값 이하로 내려갈 때마다 다음 페이즈로 전환.
            // 예: [0.5f] = 1회 전환(페이즈1→2), 50% 미만이면 페이즈2.
            var thresholds = _bossStats.PhaseHpThresholds;
            int index = 0;
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (hpRatio <= thresholds[i])
                    index = i + 1;
            } 
            return index;
        }

        private ISkillSelector CreateSelectorForPhase(int phaseIndex)
        {
            // 페이즈가 올라갈수록 광역 슬램 비중이 커지고 간격이 짧아짐
            return phaseIndex switch
            {
                0 => new BossPatternSelector(
                    new[] { MonsterSkillId.MeleeAttack, MonsterSkillId.MeleeAttack, MonsterSkillId.AoESlam },
                    _bossStats.AttackCooldown),
                _ => new BossPatternSelector(
                    new[] { MonsterSkillId.MeleeAttack, MonsterSkillId.AoESlam, MonsterSkillId.AoESlam },
                    _bossStats.AoECooldown),
            };
        }

        private void HandleEnterPhaseTransition()
        {
            _agent.isStopped = true;
            _skillSelector = CreateSelectorForPhase(_currentPhaseIndex);

            if (_animator != null)
                _animator.SetTrigger(PhaseTransitionTrigger);

            WaitAndResumeAttack().Forget();
        }

        private async UniTask WaitAndResumeAttack()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_bossStats.PhaseTransitionDuration));
            SafeFire(MonsterTrigger.TransitionComplete);
        }

        private void HandleDied() => SafeFire(MonsterTrigger.Died);
        // PLAN: 사망 시 처리는 비활성화만 (루팅/보상/이펙트는 제외)
        private void HandleEnterDead()
        {
            _agent.isStopped = true;
            this.gameObject.SetActive(false);
        }

        private bool SafeFire(MonsterTrigger trigger)
        {
            if (!_stateMachine.CanFire(trigger)) return false;
            _stateMachine.Fire(trigger);
            return true;
        }
    }
}