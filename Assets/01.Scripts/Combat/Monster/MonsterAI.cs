using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Stateless;
using Mirror;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat.Monster
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(PooledObject))]
    [RequireComponent(typeof(Rigidbody))] // Rigidbody 없이는 CharacterController가 꺼진 원격 플레이어와
                                          // OnTriggerEnter 자체가 발생하지 않는다 (Unity 물리 규칙)
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
        protected NetworkAnimator _networkAnimator;   // Trigger 타입 파라미터는 이걸 거쳐야 다른 클라에 동기화됨
        private StateMachine<MonsterState, MonsterTrigger> _stateMachine;
        protected ISkillSelector _skillSelector;
        private readonly AIContext _aiContext = new AIContext();
        private Transform _target;
        private bool _actionLocked;

        // 진입한 유효 대상의 루트 Transform → 콜라이더 개수 참조카운트.
        // 콜라이더가 여러 개인 대상이 하나씩 나가도 완전히 나가기 전엔 후보에서 지워지지 않게 한다.
        private readonly Dictionary<Transform, int> _targetRefCounts = new Dictionary<Transform, int>();

        // RangedMonsterHitbox가 조준점으로 읽어감. 타겟이 없으면 null.
        public Vector3? CurrentTargetPosition => _target != null ? (Vector3?)_target.position : null;

        // 실제로 풀에 반납된 시점(=죽는 애니메이션까지 완전히 끝난 시점) 통지
        // — MonsterWaveSpawner가 웨이브 전환 타이밍으로 구독
        public event Action OnReturnedToPool;
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
            _networkAnimator = GetComponent<NetworkAnimator>();

            // Health.OnEnable()보다 먼저 실행돼야 MaxHP가 정확히 반영됨
            // (같은 오브젝트의 모든 컴포넌트는 Awake가 전부 끝난 뒤에야 OnEnable이 실행되므로 안전)
            _health.Initialize(_stats.MaxHP, CombatFaction.Monster);
            _agent.speed = _stats.MoveSpeed;

            // RequireComponent가 기본값(isKinematic=false, useGravity=true)으로 방금 새로 붙인 것이므로
            // 여기서 직접 세팅해야 한다. Rigidbody 존재 자체가 목적이지 물리 시뮬레이션이 목적이 아니다 —
            // NavMeshAgent가 이동을 담당하므로 중력/충돌 반응은 꺼서 서로 간섭하지 않게 한다.
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

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
            // 태그가 아니라 Faction으로 판별 — 플레이어든 컴패니언이든 "플레이어 편"이면 전부 유효 대상
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.Faction != CombatFaction.Player) return;

            bool wasEmpty = _targetRefCounts.Count == 0;
            var root = other.transform.root;

            // Key: 대상의 루트 Transform(즉, 플레이어/컴패니언 전체 오브젝트)
            // Value: 그 루트에 속한 콜라이더가 현재 Trigger 안에 몇 개 들어와 있는지 카운트
            // 같은 플레이어의 여러 콜라이더가 들어오면 카운트가 계속 올라감
            _targetRefCounts.TryGetValue(root, out int count);
            _targetRefCounts[root] = count + 1;

            if (_target == null)
                _target = root;

            if (wasEmpty)
            {
                bool wasIdle = _stateMachine.State == MonsterState.Idle;
                SafeFire(MonsterTrigger.PlayerDetected);
                if (wasIdle)
                    OnPlayerFirstDetected();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.Faction != CombatFaction.Player) return;

            var root = other.transform.root;
            if (!_targetRefCounts.TryGetValue(root, out int count)) return;

            // 플레이어 편 콜라이더가 나가면:
            // 해당 루트 Transform의 카운트를 -1
            // 카운트가 0이 되면 → 완전히 나간 것으로 판단하고 Dictionary에서 제거
            count -= 1;
            if (count <= 0)
                _targetRefCounts.Remove(root);
            else
                _targetRefCounts[root] = count;

            // 현재 타겟(_target)이 그 루트였고 _targetRefCounts에서 콜라이더가 다 빠져나갔다면,
            // 다른 후보를 다시 선택(ChooseNearestTarget)
            if (!_targetRefCounts.ContainsKey(root) && _target == root)
                _target = ChooseNearestTarget();

            // 전원 나갔을 때만 해제 — 기존엔 "누구 하나 나가면 무조건 해제"였다
            if (_targetRefCounts.Count == 0)
            {
                SafeFire(MonsterTrigger.PlayerLost);
                _target = null;
            }
        }

        #endregion

        #region 팩토리 초기화

        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다.
        // AddComponent는 오브젝트가 활성 상태면 그 자리에서 즉시 Awake를 실행시키므로,
        // Awake가 필요로 하는 _stats를 먼저 채워두지 않으면 Awake가 null 체크에 걸려 자기 자신을 비활성화한다.
        //
        // 순수 프리팹엔 Animator/Collider/NavMeshAgent가 아예 없다 — RequireComponent가 방금 기본값으로 새로 만든 것들이라
        // Avatar/Controller/몸통 크기/이동 파라미터를 전부 여기서 채워야 실제 게임플레이와 같은 결과가 나온다.
        public void Initialize(MonsterStatsSO stats)
        {
            _stats = stats;

            // Animator는 프리팹에 사전 부착 + Avatar/Controller가 이미 세팅되어 있어야 함
            // (클라이언트가 자기 Animator로 애니메이션을 재생하려면 서버 AddComponent 경로 없이도 준비돼 있어야 하기 때문)
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_detectionTrigger == null) _detectionTrigger = GetComponent<SphereCollider>();

            var body = GetComponent<CapsuleCollider>();
            body.center = stats.BodyColliderCenter;
            body.radius = stats.BodyColliderRadius;
            body.height = stats.BodyColliderHeight;

            ConfigureNavMeshAgent(GetComponent<NavMeshAgent>());
        }

        // MoveSpeed(스탯 SO 값)를 제외한 나머지는 몬스터 전체가 공유하는 고정 휴머노이드 프로파일.
        // 몬스터별로 값이 갈라져야 하는 사례가 나오면 그때 스탯 SO 필드로 옮긴다.
        private static void ConfigureNavMeshAgent(NavMeshAgent agent)
        {
            agent.baseOffset = 0f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = true;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = 50;
            agent.autoTraverseOffMeshLink = true;
            agent.autoRepath = true;
            agent.areaMask = NavMesh.AllAreas;
        }

        // 스포너가 pool.Rent() 직후 호출 — 최초 생성/재사용 두 경우 모두 이 경로로 통일
        public virtual void PrepareForSpawn(Vector3 position, Quaternion rotation)
        {
            InitStateMachine(); // Dead 등 이전 상태 제거 → Idle로 재생성
            if (!_agent.Warp(position))
                Debug.LogWarning($"[MonsterAI] Warp 실패 — spawnPoint가 NavMesh 밖에 있을 수 있음 on {gameObject.name}", this);
            this.transform.rotation = rotation;
            _target = null;
            _targetRefCounts.Clear(); // 풀 재사용 시 이전 생애의 후보 잔재를 남기지 않는다
            _actionLocked = false;
            // 쿨다운/패턴 상태 완전 초기화
            // (BossAI는 이 메서드를 오버라이드해 Phase 0으로 돌아감)
            _skillSelector = CreateDefaultSkillSelector();

            if (_animator != null)
            {
                _animator.Rebind(); // 이전 생애의 트리거/상태(Death 등) 전부 초기화
                _animator.Update(0);
            }

            // 클라이언트는 이 재사용된 GameObject를 모름
            // 서버가 다시 스폰 메시지를 보내야 클라 쪽에 새 사본이 생김
            // NetworkServer.active 가드로 로컬(비-네트워크) 테스트도 그대로 동작
            if (NetworkServer.active)
                NetworkServer.Spawn(gameObject);
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
                        if (_networkAnimator != null)
                            _networkAnimator.SetTrigger(AttackTrigger);
                        LockUntilAnimationFinishes();
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
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                if (_networkAnimator != null)
                    _networkAnimator.SetTrigger(GetDeathTriggerHash());
                await UniTask.Delay(TimeSpan.FromSeconds(_stats.DeathDuration));

                // 클라이언트 사본을 먼저 정리 — SetActive(false)보다 먼저 호출해야 함
                if (NetworkServer.active)
                    NetworkServer.UnSpawn(gameObject);

                // 풀 반납 — 내부적으로 SetActive(false)까지 처리됨
                GetComponent<PooledObject>().ReturnToPool();
                OnReturnedToPool?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // 대기 중 오브젝트가 파괴됨 — 정상 흐름, 별도 처리 불필요
            }
        }

        #endregion

        #region 유틸리티

        private bool SafeFire(MonsterTrigger trigger)
        {
            if (!_stateMachine.CanFire(trigger)) return false;
            _stateMachine.Fire(trigger);
            return true;
        }

        // 실제 대기 로직 (awaitable). 앞뒤로 다른 처리(이동 멈춤/재개 등)를 끼워 넣어야 할 때 이걸 직접 await한다.
        protected async UniTask WaitForCurrentAnimationToFinish()
        {
            _actionLocked = true;
            var ct = this.GetCancellationTokenOnDestroy();

            try
            {
                int baselineHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

                // 1) 트리거로 인한 전이가 실제로 일어나 상태가 기준선에서 벗어날 때까지 대기
                int enteredStateHash = baselineHash;
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Yield(ct);
                    // Base Layer(레이어 인덱스 0)에서 실제로 재생 중인 상태를 읽어서 그 이름의 해시값을 저장
                    enteredStateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                    if (enteredStateHash != baselineHash)
                        break;
                }

                // 2) 그 상태가 끝나거나(정상 종료) 다른 상태로 또 바뀔 때까지(인터럽트) 대기
                while (!ct.IsCancellationRequested)
                {
                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    // shortNameHash != enteredStateHash: 우리가 기다리던 상태가 아니라 다른 상태로 바뀜 
                    bool interrupted = stateInfo.shortNameHash != enteredStateHash;
                    // normalizedTime >= 1f: "이 애니메이션을 최소 한 번은 끝까지 재생했다"는 뜻.
                    // IsInTransition(0)은 지금 크로스페이드가 진행 중인지 알려주는 값.
                    //  - normalizedTime이 1을 넘었어도 아직 블렌드 중이면 계속 기다림
                    bool finished = stateInfo.normalizedTime >= 1f && !_animator.IsInTransition(0);
                    // 매 프레임 상태를 다시 읽어서 두 조건 중 하나라도 맞으면 루프를 빠져나감
                    if (interrupted || finished)
                        break;

                    await UniTask.Yield(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 대기 중 오브젝트가 파괴됨 — 정상 흐름
            }
            finally
            {
                _actionLocked = false;
            }
        }


        // 그냥 발동만 시키고 잊어도 되는 경우(공격류)용 — FaceTarget()이 이미 isStopped를 관리해주기 때문에 별도 뒤처리 불필요.
        protected void LockUntilAnimationFinishes() => WaitForCurrentAnimationToFinish().Forget();

        private Transform ChooseNearestTarget()
        {
            Transform nearest = null;
            float nearestSqrDist = float.MaxValue;
            foreach (var candidate in _targetRefCounts.Keys)
            {
                float sqrDist = (candidate.position - transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        #endregion

        #region 하위 클래스(BossAI 등) 확장 지점

        protected virtual ISkillSelector CreateDefaultSkillSelector()
            => _stats is RangedMonsterStatsSO rangedStats
                ? new RangedMobSkillSelector(rangedStats.RetreatDistance, rangedStats.AttackCooldown)
                : new MeleeMobSkillSelector(_stats.AttackCooldown);

        protected virtual int GetDeathTriggerHash() => DeathTrigger;
        protected virtual void OnTick() { }
        protected virtual bool IsUpdatePaused => _actionLocked;
        protected virtual void OnIdleUpdate() { }
        protected virtual void OnEnterIdle() { }
        protected virtual void OnPlayerFirstDetected() { }

        #endregion
    }
}
