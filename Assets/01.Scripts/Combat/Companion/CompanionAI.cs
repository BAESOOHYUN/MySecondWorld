using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Stateless;
using Mirror;
using MySecondWorld.Combat.Player;

namespace MySecondWorld.Combat.Companion
{
    // MonsterAI와 대칭 구조(Follow/Chase/Attack/Dead)지만 상속하지 않는다 — 감지 태그/Idle 개념이
    // 정반대라(제 발로 서있는 몹 vs owner를 항상 따라다니는 동료) 상속으로 끼워 맞추면 더 부자연스럽다.
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Rigidbody))]   // 몬스터 감지 트리거가 (MonsterAI와) 같은 이유로 부실할 수 있어 선제 조치
    public class CompanionAI : MonoBehaviour
    {
        #region State Machine 정의

        private enum CompanionState
        {
            Follow,
            Chase,
            Attack,
            Dead,
        }

        private enum CompanionTrigger
        {
            MonsterDetected,
            MonsterLost,
            TargetInRange,
            TargetOutOfRange,
            Died,
        }

        #endregion

        #region Fields

        [SerializeField] private CompanionStatsSO _stats;
        [SerializeField] private SphereCollider _detectionTrigger;
        [SerializeField] private Animator _animator;

        private static readonly int AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));
        private static readonly int DeathTrigger = Animator.StringToHash(nameof(DeathTrigger));
        private static readonly int Speed = Animator.StringToHash(nameof(Speed));
        private int _monsterLayer;

        private NavMeshAgent _agent;
        private Health _health;
        private NetworkAnimator _networkAnimator;
        private Transform _owner;   // 따라다녀야 할 플레이어 
        private Transform _targetM;  // 감지된 몬스터
        private Health _targetMHealth; // 현재 감지된 몬스터의 Health — 사망을 직접 감지하기 위함
        private StateMachine<CompanionState, CompanionTrigger> _stateMachine;
        private float _lastAttackTime;
        private bool _followPaused;
        private Vector3 _lastFollowTarget;
        private const float FollowRecalcThreshold = 0.5f; // 이 거리 이상 움직였을 때만 재계산

        public bool IsInCombat =>
            _stateMachine != null &&
            (_stateMachine.State == CompanionState.Chase || _stateMachine.State == CompanionState.Attack);
        public bool IsOwnerInCombat =>
            _owner != null && _owner.TryGetComponent(out PlayerAttackController attackController) && attackController.IsInCombat;
        public bool IsFollowPaused => _followPaused;

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            if (_stats == null)
            {
                Debug.LogError($"CompanionStatsSO missing on {gameObject.name}", this);
                enabled = false;
                return;
            }

            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _networkAnimator = GetComponent<NetworkAnimator>();

            if (_animator == null) _animator = GetComponent<Animator>();
            if (_detectionTrigger == null) _detectionTrigger = GetComponent<SphereCollider>();

            // Health.OnEnable()보다 먼저 실행돼야 MaxHP가 정확히 반영됨 (MonsterAI와 동일 순서 근거)
            _health.Initialize(_stats.MaxHP, CombatFaction.Monster);
            _agent.speed = _stats.MoveSpeed;

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _detectionTrigger.isTrigger = true;
            _detectionTrigger.radius = _stats.DetectRange;
            _monsterLayer = LayerMask.NameToLayer("Monster");

            InitStateMachine();
        }

        private void OnEnable()
        {
            _health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            _health.OnDied -= HandleDied;
            ClearTarget(); // 추적 중이던 타겟의 OnDied 구독 해제
        }

        private void Update()
        {
            if (_owner == null) return;
            _animator.SetFloat(Speed, _agent.velocity.magnitude);

            switch (_stateMachine.State)
            {
                case CompanionState.Follow:
                    UpdateFollow();
                    break;
                case CompanionState.Chase:
                    UpdateChase();
                    break;
                case CompanionState.Attack:
                    UpdateAttack();
                    break;
            }
        }
        #endregion

        #region 팩토리 초기화

        // CompanionFactory가 서버에서만 호출한다.
        // 프리팹은 CompanionAI가 기본 비활성 상태로 배치돼 있어야 하고(씬 배치 체크리스트 참고),
        // Initialize가 owner를 채운 뒤 enabled = true로 켠다
        // — 클라이언트 관전 사본은 이 메서드가 아예 호출되지 않아 비활성 상태 그대로 남는다
        // (Player 트랙에서 확립된 "팩토리는 서버 인스턴스에만 작용" 패턴과 동일).
        public void Initialize(Transform ownerTransform)
        {
            _owner = ownerTransform;
            enabled = true;
        }

        // CompanionServerState.Revive()가 서버에서 호출 — NavMeshAgent 경유로 안전하게 위치 이동
        public void Warp(Vector3 position)
        {
            if (!_agent.Warp(position))
                Debug.LogWarning($"[CompanionAI] Warp 실패 — position이 NavMesh 밖에 있을 수 있음 on {gameObject.name}", this);
        }

        // CompanionServerState.Revive()가 부활 시 호출 — Dead에서 못 빠져나오는 상태머신을 통째로 재초기화
        public void ResetForRevive()
        {
            InitStateMachine();
            SetAgentStopped(_followPaused);
        }

        #endregion

        #region State Machine 동작

        private void InitStateMachine()
        {
            _stateMachine = new StateMachine<CompanionState, CompanionTrigger>(CompanionState.Follow, FiringMode.Queued);

            _stateMachine.Configure(CompanionState.Follow)
                .OnEntry(() => SetAgentStopped(_followPaused))
                .Permit(CompanionTrigger.MonsterDetected, CompanionState.Chase)
                .Permit(CompanionTrigger.Died, CompanionState.Dead);

            _stateMachine.Configure(CompanionState.Chase)
                .OnEntry(() => { _followPaused = false; SetAgentStopped(false); _agent.updateRotation = true; })
                .Permit(CompanionTrigger.MonsterLost, CompanionState.Follow)
                .Permit(CompanionTrigger.TargetInRange, CompanionState.Attack)
                .Permit(CompanionTrigger.Died, CompanionState.Dead);

            _stateMachine.Configure(CompanionState.Attack)
                .OnEntry(() => SetAgentStopped(true))
                .Permit(CompanionTrigger.TargetOutOfRange, CompanionState.Chase)
                .Permit(CompanionTrigger.MonsterLost, CompanionState.Follow)
                .Permit(CompanionTrigger.Died, CompanionState.Dead);

            _stateMachine.Configure(CompanionState.Dead)
                .OnEntry(HandleEnterDead);

            _stateMachine.OnTransitioned(t => Debug.Log($"[CompanionAI] {t.Source} → {t.Destination} ({t.Trigger})"));
        }

        //  owner가 일정 거리 이상 움직였을 때만 재계산하여 이동
        private void UpdateFollow()
        {
            SetAgentStopped(_followPaused); // 매 프레임 강제 — 다른 경로에서 풀려도 다시 잡음
            if (_followPaused) return;

            float distance = Vector3.Distance(transform.position, _owner.position);
            if (distance < _stats.FollowDistanceThreshold) return;

            if (Vector3.Distance(_owner.position, _lastFollowTarget) < FollowRecalcThreshold) return;

            _lastFollowTarget = _owner.position;
            SetAgentDestination(_owner.position);
        }

        private void UpdateChase()
        {
            if (_targetM == null)
            {
                SafeFire(CompanionTrigger.MonsterLost);
                return;
            }

            SetAgentDestination(_targetM.position);
            float distance = Vector3.Distance(transform.position, _targetM.position);
            if (distance <= _stats.AttackRange)
                SafeFire(CompanionTrigger.TargetInRange);
        }

        private void UpdateAttack()
        {
            if (_targetM == null)
            {
                SafeFire(CompanionTrigger.TargetOutOfRange);
                return;
            }

            float distance = Vector3.Distance(transform.position, _targetM.position);
            float exitRange = _stats.AttackRange * 1.2f; // Monster 쪽과 동일한 유격 — 경계에서 상태 떨림 방지
            if (distance > exitRange)
            {
                SafeFire(CompanionTrigger.TargetOutOfRange);
                return;
            }

            FaceTarget();

            if (Time.time - _lastAttackTime < _stats.AttackCooldown) return;
            _lastAttackTime = Time.time;
            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(AttackTrigger); // 실제 데미지는 CompanionHitbox.OnAttackHit()의 Animation Event가 처리
        }

        private void FaceTarget()
        {
            Vector3 lookDirection = _targetM.position - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        #endregion

        #region 대화 중 이동 정지 (CompanionInteractable이 호출)

        public void PauseFollow()
        {
            _followPaused = true;
            if (_stateMachine.State == CompanionState.Follow)
                SetAgentStopped(true);
        }

        public void ResumeFollow()
        {
            _followPaused = false;
            if (_stateMachine.State == CompanionState.Follow)
                SetAgentStopped(false);
        }

        #endregion

        #region 사망 처리

        private void HandleDied() => SafeFire(CompanionTrigger.Died);

        private void HandleEnterDead()
        {
            SetAgentStopped(true);
            PlayDeathSequence().Forget();
        }

        private async UniTaskVoid PlayDeathSequence()
        {
            try
            {
                if (_networkAnimator != null)
                    _networkAnimator.SetTrigger(DeathTrigger);
                await UniTask.Delay(TimeSpan.FromSeconds(_stats.DeathDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
                GetComponent<CompanionServerState>()?.MarkDead(); // 죽음 애니메이션 '재생 완료 후' 비활성화되도록 호출. MarkDead() => _isAlive = false;
            }
            catch (OperationCanceledException)
            {
                // 대기 중 오브젝트가 파괴됨 — 정상 흐름
            }
        }

        #endregion

        #region 유틸리티

        private bool SafeFire(CompanionTrigger trigger)
        {
            if (!_stateMachine.CanFire(trigger)) return false;
            _stateMachine.Fire(trigger);
            return true;
        }

        // 자식 오브젝트(몬스터 감지 전용 콜라이더, NPC 레이어와 분리)의 트리거 이벤트를 TriggerRelay가 대신 받아서 전달.
        // 코드 참조가 아니라 Companion_Ari.prefab의 TriggerRelay.OnTriggerEntered(UnityEvent) 슬롯에
        // 인스펙터로 직접 연결돼있음 — Find References로는 호출부가 안 잡힘, 프리팹에서 확인해야 함.
        public void HandleDetectionTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != _monsterLayer) return;
            if (_stateMachine.State != CompanionState.Follow) return;

            AcquireNearestTarget();
        }

        // 추적 중이던 타겟이 죽었을 때 — Transform은 안 사라지므로(풀링) 이 이벤트가 유일한 신호
        private void HandleTargetDied()
        {
            Debug.Log($"[CompanionTarget] 사망 콜백 발동: {_targetM?.name}", this);
            ClearTarget();
            SafeFire(CompanionTrigger.MonsterLost); 
            TryDetectNearbyMonster(); // 타겟 사망 순간 감지 범위 안에 이미 다른 몬스터가 있어도(Enter 재발화 안 됨) 즉시 재탐지
        }

        // 구독 해제 + 참조 정리를 한곳에 모음 — 타겟이 바뀌거나 죽거나 컴포넌트가 파괴될 때 전부 여기를 거치게 해서
        // 구독 누락/중복 구독을 막는다
        private void ClearTarget()
        {
            if (_targetMHealth != null)
                _targetMHealth.OnDied -= HandleTargetDied;
            _targetMHealth = null;
            _targetM = null;
        }

        private void TryDetectNearbyMonster() => AcquireNearestTarget();

        // 감지 범위 안 살아있는 몬스터 중 가장 가까운 걸 채택.
        // OverlapSphere 결과 순서는 거리순이 아니라 물리엔진 내부 순서일 뿐이라, 그냥 첫 번째를 집으면
        // 더 가까운 후보를 놔두고 먼 쪽으로 달려가는 문제가 있음(여러 마리 동시 스폰 시 특히 두드러짐).
        private void AcquireNearestTarget()
        {
            var hits = Physics.OverlapSphere(transform.position, _stats.DetectRange, 1 << _monsterLayer);
            Transform nearest = null;
            Health nearestHealth = null;
            float nearestSqrDist = float.MaxValue; // 지금까지 찾은 후보 중 가장 가까운 놈까지의 거리의 제곱

            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<Health>();
                if (health == null || health.IsDead) continue; // 죽음 애니메이션 재생 중인 시체는 후보에서 제외

                // 거리 비교만 할 목적이면 sqrt가 아예 필요 없음.
                // Vector3.sqrMagnitude는 sqrt 없이 x²+y²+z²만 계산해서 비용 훨씬 쌈
                float sqrDist = (hit.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = hit.transform;
                    nearestHealth = health;
                }
            }
            // 루프가 다 끝난 뒤, 최종 확정된 최근접 대상 하나에 대해서만 딱 한 번 실행
            if (nearest == null) return;

            _targetM = nearest;
            _targetMHealth = nearestHealth;
            Debug.Log($"[CompanionTarget] 지정: {_targetM.name} healthId={_targetMHealth?.GetInstanceID()}", this);

            _targetMHealth.OnDied += HandleTargetDied;
            SafeFire(CompanionTrigger.MonsterDetected);
        }

        // NavMeshAgent가 지형에 파묻히는 등으로 NavMesh 밖에 있을 때 isStopped/SetDestination을 호출하면
        // "Resume can only be called on an active agent that has been placed on a NavMesh" 예외가 남 — 방어
        private void SetAgentStopped(bool stopped)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = stopped;
        }

        private void SetAgentDestination(Vector3 destination)
        {
            if (_agent.isOnNavMesh) _agent.SetDestination(destination);
        }

        #endregion
    }
}
