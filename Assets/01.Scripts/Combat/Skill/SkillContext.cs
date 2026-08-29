using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 시전자/공격력/대상 후보를 세 축에 공통으로 전달하는 데이터 컨테이너.
    // 실행 1회당 새로 만들어지므로, 실행 중 값이 바뀌어도 다른 실행과 섞이지 않는다.
    public class SkillContext
    {
        public GameObject Caster { get; }
        public float Power { get; }

        // 축1이 최종적으로 읽는 대상 후보.
        // 탐지 단계(OverlapSphere 등)가 채운 값으로 시작하고,
        // 축2가 "대기 중에 대상을 확정하는" 종류라면 SetCandidates로 덮어쓴다.
        public IReadOnlyList<IDamageable> Candidates { get; private set; }

        // 투사체 스킬의 발사 정보. 즉시발동(InstantCast) 스킬은 사용하지 않아 기본값 null.
        public ProjectileRequest? Shot { get; }

        public SkillContext(
            GameObject caster,
            IReadOnlyList<IDamageable> candidates,
            float power,
            ProjectileRequest? shot = null
            )
        {
            Caster = caster;
            Candidates = candidates;
            Power = power;
            Shot = shot;
        }

        // 축2 전용 — 대기가 끝난 시점에 확정된 대상으로 후보를 교체한다.
        public void SetCandidates(IReadOnlyList<IDamageable> candidates) => Candidates = candidates;
    }
}