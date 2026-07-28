using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 원거리몹 스킬 선택 전략 — 너무 가까우면 쿨다운 무시하고 후퇴 최우선, 아니면 쿨다운 통과 시 사격.
    public class RangedMobSelector : ISkillSelector
    {
        private readonly float _retreatDistance;
        private readonly float _cooldown;
        private float _nextShootTime;
        public RangedMobSelector(float retreatDistance, float cooldown)
        {
            _retreatDistance = retreatDistance;
            _cooldown = cooldown;
        }

        public int? SelectSkill(AIContext context)
        {
            if (context.DistanceToTarget < _retreatDistance) 
                return MonsterSkillId.Retreat; // 쿨다운과 무관하게 최우선

            if (Time.time < _nextShootTime)
                return null;

            _nextShootTime = Time.time + _cooldown;
            return MonsterSkillId.RangedShoot;
        }
    }
}