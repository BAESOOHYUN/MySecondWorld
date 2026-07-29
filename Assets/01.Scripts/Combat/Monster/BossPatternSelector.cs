using System;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 보스 패턴 selector — 스킬 ID 배열을 정해진 간격으로 순환(cyclic).
    // 페이즈가 바뀔 때마다 MonsterAI가 이 인스턴스를 다른 패턴으로 통째로 교체한다.
    public class BossPatternSelector : ISkillSelector
    {
        private readonly int[] _pattern;
        private readonly float _actionInterval;
        private int _patternIndex;
        private float _nextActionTime;

        public BossPatternSelector(int[] pattern, float actionInterval)
        {
            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("pattern은 최소 1개 이상이어야 합니다.", nameof(pattern));

            _pattern = pattern;
            _actionInterval = actionInterval;
        }

        public int? SelectSkill(AIContext context)
        {
            if (Time.time < _nextActionTime)
                return null;

            int skillId = _pattern[_patternIndex];
            _patternIndex = (_patternIndex + 1) % _pattern.Length;
            _nextActionTime = Time.time + _actionInterval;
            return skillId;
        }
    }
}
