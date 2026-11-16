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

        // 축2가 최종적으로 읽는 대상 후보.
        // 탐지 단계(OverlapSphere 등)가 채운 값으로 시작하고,
        // 축1이 "대기 중에 대상을 확정하는" 종류라면 SetCandidates로 덮어쓴다.
        public IReadOnlyList<IDamageable> Candidates { get; private set; }

        // 투사체 스킬의 발사 정보. 즉시발동(InstantCast) 스킬은 사용하지 않아 기본값 null.
        public ProjectileRequest? Shot { get; }
        public Animator CasterAnimator { get; }

        // 서버에서 실행되는 스킬이 시전자 화면의 애니메이션을 재생하게 하는 통로(없으면 CasterAnimator 직접 사용).
        public System.Action<string> PlayAnimation { get; set; }

        // 애니메이션 클립을 뺀 모든 클립(이펙트·사운드·타격)을 이만큼 늦춘다.
        // = 공격 애니메이션의 타격 프레임(OnAttackHit)까지 걸리는 시간. 타임라인의 시간은 "타격 순간 기준 상대값"이 된다.
        public float TimeOffset { get; set; }

        // 스플래시 판정의 폭심지/반경. 값이 없으면(null) 스플래시가 아니므로
        // SplashDamageEffectSO는 감쇠 없이 Power를 그대로 적용한다.
        public Vector3? Epicenter { get; private set; }
        public float SplashRadius { get; private set; }


        public SkillContext(
            GameObject caster,
            IReadOnlyList<IDamageable> candidates,
            float power,
            ProjectileRequest? shot = null,
            Animator casterAnimator = null
            )
        {
            Caster = caster;
            Candidates = candidates;
            Power = power;
            Shot = shot;
            CasterAnimator = casterAnimator;
        }

        // 축1 전용 — 대기가 끝난 시점에 확정된 대상으로 후보를 교체한다.
        public void SetCandidates(IReadOnlyList<IDamageable> candidates) => Candidates = candidates;

        public void SetEpicenter(Vector3 point, float radius)
        {
            Epicenter = point;      // 폭심지(중심 좌표)
            SplashRadius = radius;  // 스플래시 반경
        }
    }
}