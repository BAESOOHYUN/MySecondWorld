using UnityEngine;

namespace MySecondWorld.Combat
{
    // 기존 축2+축3을 그 시각에 발동하는 클립
    // 축2(누구를: TargetSelectorSO) + 축3(무슨 효과: SkillEffectSO)을 묶어 StartTime 시각에 발동하는 클립.
    // 축1(언제)는 SkillDefinition이 타임라인 시작 전에 한 번 대기하므로 여기에 없다.

    // 대상 후보를 어디서 찾을지.
    public enum HitBasis
    {
        AimPoint,    // 시전 순간 정해진 조준 지점(마법·범위기). 기존 동작.
        CasterFront, // 이 클립이 실행되는 순간 시전자 몸 앞(근접). 닿아야 맞는다.
    }

    [System.Serializable]
    public class EffectClip : SkillClip
    {
        [SerializeField] private TargetSelectorSO _targetSelector;
        [SerializeField] private SkillEffectSO[] _effects;

        [Header("판정 기준")]
        [SerializeField] private HitBasis _hitBasis = HitBasis.AimPoint;
        [Tooltip("CasterFront 전용: 시전자 앞 몇 m를 중심으로 판정할지")]
        [SerializeField] private float _hitForward = 1.5f;
        [Tooltip("CasterFront 전용: 판정 반경(m)")]
        [SerializeField] private float _hitRadius = 1.5f;

        private static int _monsterMask = -1;
        private static int MonsterMask => _monsterMask != -1 ? _monsterMask : (_monsterMask = LayerMask.GetMask("Monster"));

        public override void Execute(SkillContext ctx)
        {
            // 툴에서 막 추가한 빈 클립은 슬롯이 비어 있다 — 예외 대신 조용히 건너뛴다.
            if (_targetSelector == null || _effects == null) return;

            // 근접: 시전 때 모아 둔 후보를 쓰지 않고, 지금 이 순간 몸 앞을 다시 훑는다.
            if (_hitBasis == HitBasis.CasterFront && ctx.Caster != null)
            {
                var t = ctx.Caster.transform;
                var point = t.position + t.forward * _hitForward;
                ctx.SetCandidates(SphereAttackResolver.QueryCandidates(ctx.Caster, point, _hitRadius, MonsterMask));
                ctx.SetEpicenter(point, _hitRadius);
            }

            foreach (var target in _targetSelector.GetTargets(ctx))
                foreach (var effect in _effects)
                    effect.Apply(target, ctx);
        }
    }
}