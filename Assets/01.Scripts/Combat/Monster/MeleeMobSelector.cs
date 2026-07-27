using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 근접몹 스킬 선택 전략 — 지금은 스킬이 근접 공격 하나뿐이라 쿨다운 통과 여부만 판단.
    // 스킬이 늘어나면 SelectSkill() 안에서 상황(AIContext)에 따라 다른 스킬 ID를 반환하도록 확장한다.
    public class MeleeMobSelector : ISkillSelector
    {
        private readonly float _cooldown;
        private float _nextAttackTime;

        public MeleeMobSelector(float cooldown)
        {
            _cooldown = cooldown;
        }

        public int? SelectSkill(AIContext context)
        {
            if (Time.time < _nextAttackTime)
                return null;

            _nextAttackTime = Time.time + _cooldown;
            return MonsterSkillId.MeleeAttack; // 쿨다운 통과했으니 "이 스킬 ID를 써라"
        }
    }
}