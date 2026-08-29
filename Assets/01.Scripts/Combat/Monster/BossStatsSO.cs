using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "BossStatsSO", menuName = "Combat/Boss Stats")]
    public class BossStatsSO : MonsterStatsSO
    {
        // 오름차순 배열. HP비율이 이 값 이하로 내려갈 때마다 다음 페이즈로 전환.
        // 예: [0.5f] = 1회 전환(페이즈1→2), 50% 미만이면 페이즈2.
        [SerializeField] private float[] _phaseHpThresholds = { 0.5f };
        [SerializeField] private float _aoeRadius = 3f;
        [SerializeField] private float _aoeCooldown = 4f;
        [SerializeField] private SkillDefinition _aoeSkill;

        [Header("페이즈 전환 연출 (Collapse -> Reassemble)")]
        [SerializeField] private float _collapseDuration = 1.5f;
        [SerializeField] private float _reassembleDuration = 1.5f;

        [Header("대기 중 연출")]
        [SerializeField] private float _idleBreakIntervalMin = 4f;
        [SerializeField] private float _idleBreakIntervalMax = 8f;

        [Header("애니메이터 트리거 이름 (몬스터별 컨트롤러 파라미터명에 맞게 조정)")]
        [SerializeField] private string[] _meleeAttackTriggers = { "Attack 1", "Attack 2", "Attack 3" };
        [SerializeField] private string _aoeSlamTrigger = "Attack 4";
        [SerializeField] private string _collapseTrigger = "Collapse";
        [SerializeField] private string _reassembleTrigger = "Reassemble";
        [SerializeField] private string _tauntTrigger = "Taunt";
        [SerializeField] private string _idleBreakTrigger = "Idle Break";
        [SerializeField] private string _deathTrigger = "Death";

        [Header("이동 애니메이션")]
        [SerializeField] private string _locomotionParam = "Locomotion";
        [SerializeField] private float _locomotionIdleValue = 0f;
        [SerializeField] private float _locomotionMaxValue = 1f;

        [Header("모션 잠금 시간 (재생 중 이동/판단 멈춤)")]
        [SerializeField] private float _tauntDuration = 1.5f;
        [SerializeField] private float _meleeAttackDuration = 1f;
        [SerializeField] private float _aoeSlamDuration = 1.5f;

        #region Property
        public IReadOnlyList<float> PhaseHpThresholds => _phaseHpThresholds;
        public float AoERadius => _aoeRadius;
        public float AoECooldown => _aoeCooldown;
        public SkillDefinition AoESkill => _aoeSkill;
        public float CollapseDuration => _collapseDuration;
        public float ReassembleDuration => _reassembleDuration;
        public float IdleBreakIntervalMin => _idleBreakIntervalMin;
        public float IdleBreakIntervalMax => _idleBreakIntervalMax;
        public IReadOnlyList<string> MeleeAttackTriggers => _meleeAttackTriggers;
        public string AoESlamTrigger => _aoeSlamTrigger;
        public string CollapseTrigger => _collapseTrigger;
        public string ReassembleTrigger => _reassembleTrigger;
        public string TauntTrigger => _tauntTrigger;
        public string IdleBreakTrigger => _idleBreakTrigger;
        public string DeathTrigger => _deathTrigger;
        public string LocomotionParam => _locomotionParam;
        public float LocomotionIdleValue => _locomotionIdleValue;
        public float LocomotionMaxValue => _locomotionMaxValue;
        public float TauntDuration => _tauntDuration;
        public float MeleeAttackDuration => _meleeAttackDuration;
        public float AoESlamDuration => _aoeSlamDuration;

        #endregion
    }
}