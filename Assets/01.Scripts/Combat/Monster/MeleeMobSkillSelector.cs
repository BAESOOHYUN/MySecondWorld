using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 근접 일반몹 스킬 선택 전략 — 이 몹 종류는 지금 근접 공격 하나뿐이라 쿨다운 통과 여부만 판단.
    // (보스의 다중 스킬 패턴은 BossPatternSelector가 별도로 담당)
    // 스킬이 늘어나면 SelectSkill() 안에서 상황(AIContext)에 따라 다른 스킬 ID를 반환하도록 확장한다.
    public class MeleeMobSkillSelector : ISkillSelector
    {
        private readonly float _cooldown;
        private float _nextAttackTime;

        public MeleeMobSkillSelector(float cooldown)
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