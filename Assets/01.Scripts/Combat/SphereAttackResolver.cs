using Cysharp.Threading.Tasks;
using System.Linq;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // MonsterHitbox/CompanionHitbox/PlayerAttackAuthority가 공통으로 쓰던
    // "OverlapSphere 스캔 → IDamageable 후보 추출 → SkillContext 구성 → ISkill.Execute" 시퀀스를 추출.
    // 베이스 클래스(MonoBehaviour/NetworkBehaviour)나 네트워크 가드(isServer, MaxAttackReach 등)는
    // 호출부마다 달라서 그대로 각자 유지하고, 이 5줄짜리 반복 로직만 공용화한다.
    public static class SphereAttackResolver
    {
        public static void ResolveAndExecute(GameObject caster, Vector3 attackPoint, float attackRadius,
            LayerMask targetLayerMask, float power, ISkill skill, Transform preferred = null)
        {
            // Physics.OverlapSphere : 물리 충돌이 아니라 순수 공간 질의. "그 안에 지금 걸쳐있는 콜라이더들이 뭐가 있어?"
            var candidates = Physics.OverlapSphere(attackPoint, attackRadius, targetLayerMask, QueryTriggerInteraction.Ignore) // 공격 지점 반경 안의 몬스터 레이어 콜라이더 전부 탐지(트리거 콜라이더는 무조건 무시)
                // 플레이어가 탭으로 고른 대상이 후보에 있으면 맨 앞으로 보낸다.
                // SingleTargetSelector가 [0]을 "가장 가까운 대상"으로 쓰므로 정렬만 바꾸면 끝난다.
                .OrderBy(c => preferred != null && (c.transform == preferred || c.transform.IsChildOf(preferred)) ? 0 : 1)
                .Select(c => c.GetComponentInParent<IDamageable>()) // 콜라이더 → 자신+조상 중 IDamageable 붙은 가장 가까운 오브젝트로 변환
                .Where(d => d != null && !d.IsDead)                 // IDamageable이 없는 대상(오탐) 제거, 죽은 대상은 후보에서 제외 — 공격이 시체에 낭비되지 않게
                .Distinct()                                         // 여러 콜라이더가 같은 IDamageable로 겹칠 때 중복 제거
                .ToList();                                          // 지연 평가를 여기서 즉시 실행해 리스트로 확정 (Candidates가 인덱스 접근 필요)

            var ctx = new SkillContext(caster, candidates, power);
            skill.Execute(ctx).Forget();
        }
    }
}
