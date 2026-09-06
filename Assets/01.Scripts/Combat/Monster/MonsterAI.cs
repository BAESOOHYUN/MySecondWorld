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
    public class MonsterAI : MonoBehaviour, IStunnable
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

        #region Constants & Statics

        private const float HitAnimationCooldown = 1.5f; // 최소 1.5초 간격

        private static readonly int AttackTrigger = Animator.StringToHash(nameof(AttackTrigger));
        private static readonly int DeathTrigger = Animator.StringToHash(nameof(DeathTrigger));
        private static readonly int HitTrigger = Animator.StringToHash(nameof(HitTrigger));
        private static readonly int IsMoving = Animator.StringToHash(nameof(IsMoving));
        // (만약 나중에 float Speed를 사용한다면: private static readonly int Speed = Animator.StringToHash("Speed");)

        #endregion

        #region Inspector Fields

        [SerializeField] protected MonsterStatsSO _stats;
        [SerializeField] private SphereCollider _detectionTrigger;
        [SerializeField] protected Animator _animator;

        #endregion

        #region Internal References & Runtime States

        protected NavMeshAgent _agent;
        protected Health _health;
        protected NetworkAnimator _networkAnimator;   // Trigger 타입 파라미터는 이걸 거쳐야 다른 클라에 동기화됨
        protected ISkillSelector _skillSelector;

        private StateMachine<MonsterState, MonsterTrigger> _stateMachine;
        private UniTaskCompletionSource _deathAnimationTcs;
        private readonly AIContext _aiContext = new AIContext();
        private Transform _target;
        private bool _actionLocked;
        private Vector3 _spawnPosition;
        private float _lastHitAnimationTime = -999f;

        // 진입한 유효 대상의 루트 Transform → 콜라이더 개수 참조카운트.
        // 콜라이더가 여러 개인 대상이 하나씩 나가도 완전히 나가기 전엔 후보에서 지워지지 않게 한다.
        private readonly Dictionary<Transform, int> _targetRefCounts = new Dictionary<Transform, int>();

        // root별 사망 구독 해제용 델리게이트. Action으로 감싸는 이유는 람다가 캡처한
        // Health 인스턴스와 핸들러를 -=로 정확히 짝지어 해제해야 이벤트 누수가 안 나기 때문.
        private readonly Dictionary<Transform, Action> _targetDeathUnsubscribers = new Dictionary<Transform, Action>();

        #endregion

        #region Properties & Events

        // RangedMonsterHitbox가 조준점으로 읽어감. 타겟이 없으면 null.
        public Vector3? CurrentTargetPosition => _target != null ? (Vector3?)_target.position : null;

        // 실제로 풀에 반납된 시점(=죽는 애니메이션까지 완전히 끝난 시점) 통지
        // — MonsterWaveSpawner가 웨이브 전환 타이밍으로 구독
        public event Action OnReturnedToPool;

        public bool IsStunned => _actionLocked;

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
            _health.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            _health.OnDied -= HandleDied;
            _health.OnDamaged -= HandleDamaged;
        }

        private void Update()
        {
            OnTick();
            UpdateMoveAnimation();

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

            var root = other.transform.root;
            // 죽었거나 소환 해제된 컴패니언은 애초에 어그로 목록에 진입조차 못하게 차단
            if (!IsValidTarget(root)) return;
            bool wasEmpty = _targetRefCounts.Count == 0;

            // Key: 대상의 루트 Transform(즉, 플레이어/컴패니언 전체 오브젝트)
            // Value: 그 루트에 속한 콜라이더가 현재 Trigger 안에 몇 개 들어와 있는지 카운트
            // 같은 플레이어의 여러 콜라이더가 들어오면 카운트가 계속 올라감
            _targetRefCounts.TryGetValue(root, out int count);
            _targetRefCounts[root] = count + 1;

            // 이 root가 처음 들어온 콜라이더라면 사망 구독을 새로 건다(콜라이더 여러 개가 겹쳐 들어와도 한 번만)
            if (count == 0)
            {
                var health = other.GetComponentInParent<Health>();
                if (health != null)
                {
                    void OnTargetRemoved() => HandleTargetDied(root);
                    health.OnDied += OnTargetRemoved;
                    health.OnBecameUntargetable += OnTargetRemoved;
                    _targetDeathUnsubscribers[root] = () =>
                    {
                        health.OnDied -= OnTargetRemoved;
                        health.OnBecameUntargetable -= OnTargetRemoved;
                    };
                }
            }

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
            {
                _targetRefCounts.Remove(root);
                UnsubscribeDeath(root);
            }
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
            _spawnPosition = position;
            _target = null;
            foreach (var unsubscribe in _targetDeathUnsubscribers.Values)
                unsubscribe?.Invoke();
            _targetDeathUnsubscribers.Clear();
            _targetRefCounts.Clear(); // 풀 재사용 시 이전 생애의 후보 잔재를 남기지 않는다
            _actionLocked = false;
            // 쿨다운/패턴 상태 완전 초기화
            // (BossAI는 이 메서드를 오버라이드해 Phase 0으로 돌아감)
            _skillSelector = CreateDefaultSkillSelector();

            if (_animator != null)
            {
                _animator.Rebind(); // 이전 상태 초기화
                _animator.Update(0);
                _animator.SetBool(IsMoving, false); // IsMoving 초기화
                _animator.ResetTrigger(HitTrigger);  // 혹시 남아있을 수 있는 피격 트리거 리셋
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
                .OnEntry(() => {
                    _agent.isStopped = true;
                    SetMovingAnimation(false); // Idle 진입 시 이동 끔
                    OnEnterIdle();
                })
                .Permit(MonsterTrigger.PlayerDetected, MonsterState.Chase)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Chase)
                .OnEntry(() => {
                    _agent.isStopped = false;
                    _agent.updateRotation = true;
                    SetMovingAnimation(true); // Chase 진입 시 이동 켬
                })
                .OnExit(() => SetMovingAnimation(false)) // Chase 벗어날 때 끔
                .Permit(MonsterTrigger.PlayerLost, MonsterState.Idle)
                .Permit(MonsterTrigger.TargetInRange, MonsterState.Attack)
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Attack)
                .OnEntry(() => {
                    _agent.isStopped = true;
                    SetMovingAnimation(false); // 공격 진입 시 이동 끔
                })
                .Permit(MonsterTrigger.TargetOutOfRange, MonsterState.Chase)
                .Permit(MonsterTrigger.PlayerLost, MonsterState.Idle)   // 공격 중 타겟 소실 시 Idle 복귀
                .Permit(MonsterTrigger.Died, MonsterState.Dead);

            _stateMachine.Configure(MonsterState.Dead)
                .OnEntry(HandleEnterDead);

            _stateMachine.OnTransitioned(t => Debug.Log($"[MonsterAI] {t.Source} → {t.Destination} ({t.Trigger})"));
        }

        #endregion

        #region 이동 및 애니메이션 제어

        private void SetMovingAnimation(bool isMoving)
        {
            if (_animator != null)
                _animator.SetBool(IsMoving, isMoving);
        }

        private void UpdateMoveAnimation()
        {
            if (_animator == null || _agent == null) return;
            // 에이전트가 정지 상태가 아니고, 실제로 이동 속도가 있을 때만 true!
            bool isActuallyMoving = !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.05f;
            _animator.SetBool(IsMoving, isActuallyMoving);
        }

        #endregion

        #region State Machine 업데이트

        private void UpdateChase()
        {
            // 추적 중에도 대상이 죽었으면 즉시 추적 중단
            if (!IsValidTarget(_target))
            {
                HandleTargetDied(_target);
                return;
            }

            // 스폰 지점에서 리쉬 범위 밖으로 나가면 포기하고 원위치 복귀
            if ((_target.position - _spawnPosition).sqrMagnitude > _stats.LeashRange * _stats.LeashRange)
            {
                GiveUpChase();
                return;
            }

            _agent.SetDestination(_target.position);
            float distance = Vector3.Distance(transform.position, _target.position);
            if (distance <= _stats.AttackRange)
                SafeFire(MonsterTrigger.TargetInRange);
        }

        private void UpdateAttack()
        {
            // 공격 중에도 대상이 죽었으면 즉시 공격 중단 및 타겟 해제
            if (!IsValidTarget(_target))
            {
                HandleTargetDied(_target);
                return;
            }

            // 공격 중이어도 타겟이 리쉬 밖이면(추격 도중 밀려났다면) 포기
            if ((_target.position - _spawnPosition).sqrMagnitude > _stats.LeashRange * _stats.LeashRange)
            {
                GiveUpChase();
                return;
            }

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

        // 어그로 전부 버리고 스폰 지점으로 귀환
        private void GiveUpChase()
        {
            foreach (var unsubscribe in _targetDeathUnsubscribers.Values)
                unsubscribe?.Invoke();
            _targetDeathUnsubscribers.Clear();
            _targetRefCounts.Clear();
            _target = null;
            SafeFire(MonsterTrigger.PlayerLost);   // Idle 진입 → OnEntry가 isStopped = true로 세팅
            _agent.isStopped = false;              // 스폰 지점까지는 걸어가야 하므로 즉시 다시 풀어줌
            _agent.SetDestination(_spawnPosition);
            SetMovingAnimation(true);
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


        #region 표적추적

        private Transform ChooseNearestTarget()
        {
            Transform nearest = null;
            float nearestSqrDist = float.MaxValue;
            List<Transform> invalids = null; // 순회 끝난 뒤 한꺼번에 제거하기 위한 임시 버퍼

            foreach (var candidate in _targetRefCounts.Keys)
            {
                // 단일 통로: 죽어있는 대상은 후보에서 즉시 영구 퇴장
                if (!IsValidTarget(candidate))
                {
                    // 무효 타겟이 하나도 없는 게 보통이라, 진짜 발견됐을 때만 리스트를 만들도록
                    // 지연 생성(lazy initialization)함 (불필요한 GC 할당 방지)
                    (invalids ??= new List<Transform>()).Add(candidate);
                    continue;
                }

                float sqrDist = (candidate.position - transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }

            if (invalids != null)
                foreach (var invalid in invalids)
                    HandleTargetDied(invalid); // foreach가 완전히 끝난 뒤에만 딕셔너리를 건드림

            return nearest;
        }

        // 표적이 물리적으로 트리거를 나가지 않아도(죽은 채로 그 자리에 있어도) 후보에서 완전히 제거한다
        private void HandleTargetDied(Transform root)
        {
            if (root == null) return; // _target이 이미 null인 채로 호출되는 경로 방어

            _targetRefCounts.Remove(root);
            UnsubscribeDeath(root);

            if (_target == root)
                _target = ChooseNearestTarget();

            if (_targetRefCounts.Count == 0)
            {
                SafeFire(MonsterTrigger.PlayerLost);
                _target = null;
            }
        }

        private void UnsubscribeDeath(Transform root)
        {
            if (_targetDeathUnsubscribers.TryGetValue(root, out var unsubscribe))
            {
                unsubscribe?.Invoke();
                _targetDeathUnsubscribers.Remove(root);
            }
        }

        #endregion

        #region 피격

        private void HandleDamaged(float currentHp)
        {
            // 1. 이미 죽었거나 사망 상태에 진입 중이면 피격 모션을 띄우지 않음 (사망 모션 우선)
            if (_health.IsDead || _stateMachine.State == MonsterState.Dead)
                return;

            // 2. 공격 중 피격 모션으로 캔슬시킬지 여부:
            // [슈퍼아머] 공격 중이거나 액션 락이 걸려있으면 피격 모션 스킵!
            if (_actionLocked || _stateMachine.State == MonsterState.Attack)
                return;
            // [쿨다운] 1.5초가 안 지났으면 피격 모션 스킵!
            if (Time.time - _lastHitAnimationTime < HitAnimationCooldown)
                return;
            _lastHitAnimationTime = Time.time;

            // 3. Mirror NetworkAnimator가 있으면 클라이언트에 동기화, 없으면 로컬 재생
            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(HitTrigger);
            else if (_animator != null)
                _animator.SetTrigger(HitTrigger);

            // [피격 경직] 0.5초간 몬스터 이동 및 행동 일시 정지
            ApplyHitStun().Forget();
        }

        /// <summary>
        /// IStunnable 구현 — 외부 CC기 및 피격 경직 공통 진입점
        /// </summary>
        public void ApplyStun(float duration) => ApplyHitStun(duration).Forget();

        private async UniTaskVoid ApplyHitStun(float duration = 0.5f)
        {
            _actionLocked = true;
            if (_agent != null && _agent.enabled)
                _agent.isStopped = true;
            SetMovingAnimation(false); // 걷기 모션 끄기 (발미끄러짐 방지)
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) { }
            finally
            {
                _actionLocked = false;
                // 살아서 추격 중(Chase)이었다면 네비메시 이동 재개
                if (_agent != null && _agent.enabled && !_health.IsDead && _stateMachine.State == MonsterState.Chase)
                {
                    _agent.isStopped = false;
                }
            }
        }

        #endregion

        #region 사망 처리

        // Death 클립 마지막 프레임 근처에 Animation Event로 연결
        public void OnDeathAnimationEnd() => _deathAnimationTcs?.TrySetResult();
        private void HandleDied() => SafeFire(MonsterTrigger.Died);

        private void HandleEnterDead()
        {
            _agent.isStopped = true;
            PlayDeathSequence().Forget();
        }

        private async UniTaskVoid PlayDeathSequence()
        {
            _deathAnimationTcs = new UniTaskCompletionSource();
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                if (_networkAnimator != null)
                    _networkAnimator.SetTrigger(GetDeathTriggerHash());

                // Animation Event가 정상 호출되면 즉시, 배선이 빠졌으면 DeathDuration 후 타임아웃으로 진행
                await UniTask.WhenAny(
                    _deathAnimationTcs.Task,
                    UniTask.Delay(TimeSpan.FromSeconds(_stats.DeathDuration), cancellationToken: ct));

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

        // MonsterAI.cs 내부 단일 생사 검증 통로
        private bool IsValidTarget(Transform target)
        {
            if (target == null) return false;
            // 1. Health 기본 생사 및 타겟 가능 여부 검증
            var health = target.GetComponentInParent<Health>();
            if (health == null || health.IsDead || !health.IsTargetable)
                return false;
            // 2. 컴패니언인 경우: 죽었거나(IsAlive == false) 소환 해제(IsPresent == false)면 즉시 공격 불가
            if (target.TryGetComponent(out MySecondWorld.Combat.Companion.ICompanionServerState compState))
            {
                if (!compState.IsAlive || !compState.IsPresent)
                    return false;
            }
            return true;


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
                // [안전장치] 최대 2.5초 지나면 강제 탈출하여 AI가 굳는 버그 방지
                using var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.5f));
                using var linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                var token = linkedCts.Token;

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
                while (!ct.IsCancellationRequested) // ct 대신 token 사용
                {
                    // 애니메이터의 0번 레이어(Base Layer)에서 지금 실시간으로 재생 중인 애니메이션의 상태 정보
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
