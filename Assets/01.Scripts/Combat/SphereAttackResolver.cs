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
            LayerMask targetLayerMask, float power, ISkill skill)
        {
            // Physics.OverlapSphere : 물리 충돌이 아니라 순수 공간 질의. "그 안에 지금 걸쳐있는 콜라이더들이 뭐가 있어?"
            var candidates = Physics.OverlapSphere(attackPoint, attackRadius, targetLayerMask, QueryTriggerInteraction.Ignore) // 공격 지점 반경 안의 몬스터 레이어 콜라이더 전부 탐지(트리거 콜라이더는 무조건 무시)
                .OrderBy(c => Vector3.Distance(attackPoint, c.transform.position)) // 가까운 순 정렬 (SingleTargetSelector가 첫 번째를 "가장 가까운 대상"으로 씀)
                .Select(c => c.GetComponentInParent<IDamageable>()) // 콜라이더 → 자신+조상 중 IDamageable 붙은 가장 가까운 오브젝트로 변환
                .Where(d => d != null)                              // IDamageable이 없는 대상(오탐) 제거
                .Distinct()                                         // 여러 콜라이더가 같은 IDamageable로 겹칠 때 중복 제거
                .ToList();                                          // 지연 평가를 여기서 즉시 실행해 리스트로 확정 (Candidates가 인덱스 접근 필요)

            var ctx = new SkillContext(caster, candidates, power);
            skill.Execute(ctx).Forget();
        }
    }
}
