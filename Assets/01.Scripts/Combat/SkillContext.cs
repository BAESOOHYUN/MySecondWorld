using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 시전자/공격력/타겟 후보 목록을 세 축(타겟팅/타이밍/효과) 구현체에 공통으로 전달하는 데이터 컨테이너.
    // Candidates는 탐지 단계(거리순 정렬 등)에서 이미 정리된 후보 목록이고,
    // 이 중 몇 명에게/누구에게 효과를 적용할지는 ITargetSelector가 결정한다.
    public class SkillContext
    {
        public GameObject Caster { get; }
        public IReadOnlyList<IDamageable> Candidates { get; }
        public float Power { get; }

        public SkillContext(GameObject caster, IReadOnlyList<IDamageable> candidates, float power)
        {
            Caster = caster;
            Candidates = candidates;
            Power = power;
        }
    }
}