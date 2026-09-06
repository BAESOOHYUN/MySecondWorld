using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // MonsterAI 확장 — 페이즈 전환(Collapse→Reassemble), 다중 근접 모션, 대기 연출(Taunt/IdleBreak),
    // 이동 애니메이션까지 담당. 일반 몹과 분기점이 너무 많아져서 상속으로 분리함.
    public class BossAI : MonsterAI
    {
        #region Fields

        private BossStatsSO _bossStats;

        private int[] _meleeAttackHashes;
        private int _aoeSlamHash;
        private int _collapseHash;
        private int _reassembleHash;
        private int _tauntHash;
        private int _idleBreakHash;
        private int _deathHash;
        private int _locomotionHash;

        private int _currentPhaseIndex;
        private bool _inPhaseTransition;
        private float _idleBreakTimer;
        private float _nextIdleBreakInterval;

        #endregion

        #region Unity 생명주기

        protected override void Awake()
        {
            // base.Awake()가 CreateDefaultSkillSelector()를 내부에서 호출하므로,
            // 그 시점엔 이미 _bossStats가 준비돼 있어야 함 -> base.Awake() 호출 전에 캐스팅.
            // _stats는 "이 몬스터가 뭐든 간에 공통으로 가진 스탯"용,
            // _bossStats는 "이게 보스일 때만 접근 가능한 전용 스탯"용으로 역할이 나뉨
            _bossStats = _stats as BossStatsSO;
            base.Awake();

            if (!enabled) return;

            if (_bossStats == null)
            {
                Debug.LogError($"BossAI에는 BossStatsSO가 필요합니다 on {gameObject.name}", this);
                enabled = false;
                return;
            }

            _meleeAttackHashes = new int[_bossStats.MeleeAttackTriggers.Count];
            for (int i = 0; i < _meleeAttackHashes.Length; i++)
                _meleeAttackHashes[i] = Animator.StringToHash(_bossStats.MeleeAttackTriggers[i]);
            _aoeSlamHash = Animator.StringToHash(_bossStats.AoESlamTrigger);
            _collapseHash = Animator.StringToHash(_bossStats.CollapseTrigger);
            _reassembleHash = Animator.StringToHash(_bossStats.ReassembleTrigger);
            _tauntHash = Animator.StringToHash(_bossStats.TauntTrigger);
            _idleBreakHash = Animator.StringToHash(_bossStats.IdleBreakTrigger);
            _deathHash = Animator.StringToHash(_bossStats.DeathTrigger);
            _locomotionHash = Animator.StringToHash(_bossStats.LocomotionParam);
        }
        #endregion


        #region 팩토리 초기화
        // MonsterAI.PrepareForSpawn이 상태머신/Warp/애니메이터/스킬셀렉터(CreateDefaultSkillSelector 오버라이드로 Phase 0 재생성)를
        // 이미 처리해주므로, 여기선 보스 전용 상태(페이즈/전환플래그/Idle Break 타이머)만 추가로 리셋
        public override void PrepareForSpawn(Vector3 position, Quaternion rotation)
        {
            base.PrepareForSpawn(position, rotation);

            _phaseTransitionCts?.Cancel();
            _phaseTransitionCts?.Dispose();
            _phaseTransitionCts = null;

            _currentPhaseIndex = 0;
            _inPhaseTransition = false;
            _idleBreakTimer = 0f;
            _nextIdleBreakInterval = 0f;
        }
        #endregion

        #region 스킬 선택 / 공격 재생

        protected override ISkillSelector CreateDefaultSkillSelector() => CreateSelectorForPhase(0);

        protected override void PlayAttackSkill(int? skillId)
        {
            switch (skillId)
            {
                case MonsterSkillId.MeleeAttack:
                    {
                        FaceTarget();
                        if (_networkAnimator != null)
                            _networkAnimator.SetTrigger(_meleeAttackHashes[UnityEngine.Random.Range(0, _meleeAttackHashes.Length)]);
                        LockUntilAnimationFinishes();
                    }
                    break;
                case MonsterSkillId.AoESlam:
                    {
                        FaceTarget();
                        if (_networkAnimator != null)
                            _networkAnimator.SetTrigger(_aoeSlamHash);
                        LockUntilAnimationFinishes();
                    }
                    break;
                default:
                    base.PlayAttackSkill(skillId); // Retreat 등 공용 처리 위임
                    break;
            }
        }

        #endregion

        #region 페이즈 전환

        private System.Threading.CancellationTokenSource _phaseTransitionCts;

        protected override bool IsUpdatePaused => base.IsUpdatePaused || _inPhaseTransition;

        protected override void OnTick()
        {
            if (_health != null && _health.IsDead) return;

            UpdateLocomotionAnimation();
            if (!_inPhaseTransition)
                CheckPhaseTransition();
        }

        private void CheckPhaseTransition()
        {
            if (_health == null || _health.IsDead || _health.CurrentHP <= 0f) return;

            int newPhaseIndex = CalculatePhaseIndex(_health.CurrentHP / _health.MaxHP);
            if (newPhaseIndex == _currentPhaseIndex) return;

            _currentPhaseIndex = newPhaseIndex;
            _inPhaseTransition = true;
            _agent.isStopped = true;
            _skillSelector = CreateSelectorForPhase(_currentPhaseIndex);
            PlayPhaseTransitionSequence().Forget();
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
            // 페이즈 0(체력 50%↑): 근접 2번당 슬램 1번 (AoE 비중 낮음)
            // 페이즈 1(체력 50%↓): 근접 1번당 슬램 2번 + 쿨다운도 AttackCooldown(근접용, 보통 더 김) 대신 AoECooldown으로 교체
            return phaseIndex switch
            {
                // phaseIndex가 정확히 0일 때
                0 => new BossPatternSelector(
                    new[] { MonsterSkillId.MeleeAttack, MonsterSkillId.MeleeAttack, MonsterSkillId.AoESlam },
                    _bossStats.AttackCooldown),
                // 그 외 전부 (1, 2, 3, ... 뭐가 오든)
                _ => new BossPatternSelector(
                    new[] { MonsterSkillId.MeleeAttack, MonsterSkillId.AoESlam, MonsterSkillId.AoESlam },
                    _bossStats.AoECooldown),
            };
        }

        // Collapse(쓰러짐) 재생 후 대기 -> Reassemble(재조립) 재생 후 대기 -> 전투 복귀.
        // 두 클립 다 자체 트랜지션으로 Locomotion에 복귀하지만, 그 시간만큼은
        // 우리 상태머신도 Attack으로 안 돌아가야 이동/공격 로직이 애니메이션과 안 겹침
        // 상태머신을 안 거치고 MonsterAI.IsUpdatePaused로만 얼려놨다가 풀기 때문에 트리거/퍼밋 추가가 필요 없음.
        private async UniTaskVoid PlayPhaseTransitionSequence()
        {
            _phaseTransitionCts?.Cancel();
            _phaseTransitionCts?.Dispose();
            _phaseTransitionCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var ct = _phaseTransitionCts.Token;

            try
            {
                if (_networkAnimator != null)
                    _networkAnimator.SetTrigger(_collapseHash);
                await UniTask.Delay(TimeSpan.FromSeconds(_bossStats.CollapseDuration), cancellationToken: ct);

                if (_health != null && _health.IsDead)
                {
                    _inPhaseTransition = false;
                    return;
                }

                if (_networkAnimator != null)
                    _networkAnimator.SetTrigger(_reassembleHash);
                await UniTask.Delay(TimeSpan.FromSeconds(_bossStats.ReassembleDuration), cancellationToken: ct);

                _inPhaseTransition = false;
            }
            catch (OperationCanceledException)
            {
                _inPhaseTransition = false;
            }
        }

        #endregion

        #region 대기 / 발견 연출

        protected override void OnEnterIdle() => _idleBreakTimer = 0f;

        // 대기 중 랜덤 간격으로 Idle Break 연출 재생 (게임플레이 영향 없음)
        protected override void OnIdleUpdate()
        {
            _idleBreakTimer += Time.deltaTime;
            if (_idleBreakTimer < _nextIdleBreakInterval) return;

            _idleBreakTimer = 0f;
            _nextIdleBreakInterval = UnityEngine.Random.Range(_bossStats.IdleBreakIntervalMin, _bossStats.IdleBreakIntervalMax);

            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(_idleBreakHash);
        }

        protected override void OnPlayerFirstDetected()
        {
            // 최초 발견 시 Taunt
            if (_networkAnimator != null)
                _networkAnimator.SetTrigger(_tauntHash);
            PlayTauntSequence().Forget();
        }

        private async UniTaskVoid PlayTauntSequence()
        {
            _agent.updateRotation = false;
            FaceTarget();
            await WaitForCurrentAnimationToFinish(); 
            _agent.updateRotation = true;

            var targetPos = CurrentTargetPosition;
            if (targetPos == null) return; // 대기 중 플레이어가 감지 범위를 벗어남 — OnTriggerExit가 이미 Idle로 전환 처리함

            float distance = Vector3.Distance(transform.position, targetPos.Value);
            if (distance > _stats.AttackRange)
                _agent.isStopped = false;
        }
        #endregion

        #region 이동 / 사망 애니메이션

        // NavMeshAgent의 실제 이동 속도를 Locomotion 파라미터로 환산
        // (0=Idle ~ 1=최대속도, 몬스터별 범위는 BossStatsSO에서 설정)
        private void UpdateLocomotionAnimation()
        {
            if (_animator == null) return;

            float normalizedSpeed = _stats.MoveSpeed > 0f
                ? Mathf.Clamp01(_agent.velocity.magnitude / _stats.MoveSpeed)
                : 0f;
            float value = Mathf.Lerp(_bossStats.LocomotionIdleValue, _bossStats.LocomotionMaxValue, normalizedSpeed);
            _animator.SetFloat(_locomotionHash, value);
        }

        protected override int GetDeathTriggerHash() => _deathHash;

        #endregion
    }
}
