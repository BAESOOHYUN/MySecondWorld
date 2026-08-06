using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 서버 권위 공격 판정 전용 NetworkBehaviour.
    // NetworkWeaponEquip(Network 어셈블리)에 두면 Network→Combat 참조가 생겨
    // 기존 Combat→Network 참조와 순환 참조가 되므로, Combat 어셈블리 안에 별도로 둔다.
    public class PlayerAttackAuthority : NetworkBehaviour
    {
        #region Fields

        private SkillExecutor _skillExecutor;

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() });
        }

        #endregion

        #region 공격 판정

        // PlayerAttackController.OnAttackHit() 이 위임하는 서버 권위 판정.
        // 몬스터의 Health/몸통 Collider는 서버 인스턴스에만 존재하므로(Mirror 몬스터 풀링 설계),
        // 원격 클라이언트가 로컬에서 직접 OverlapSphere를 해봐야 대상 자체를 못 찾는다 — 반드시 서버에서 판정해야 함.
        [Command]
        public void CmdResolveAttackHit(Vector3 attackPointPosition, float attackRadius, float power, int targetLayerMaskValue)
        {
            LayerMask targetLayerMask = targetLayerMaskValue;

            var candidates = Physics.OverlapSphere(attackPointPosition, attackRadius, targetLayerMask, QueryTriggerInteraction.Ignore) // 공격 지점 반경 안의 몬스터 레이어 콜라이더 전부 탐지(트리거 콜라이더는 무조건 무시)
                .OrderBy(c => Vector3.Distance(attackPointPosition, c.transform.position)) // 가까운 순 정렬 (SingleTargetSelector가 첫 번째를 "가장 가까운 대상"으로 씀)
                .Select(c => c.GetComponentInParent<IDamageable>()) // 콜라이더 → 자신+조상 중 IDamageable 붙은 가장 가까운 오브젝트로 변환
                .Where(d => d != null)                              // IDamageable이 없는 대상(오탐) 제거
                .ToList();                                          // 지연 평가를 여기서 즉시 실행해 리스트로 확정 (Candidates가 인덱스 접근 필요)

            var ctx = new SkillContext(this.gameObject, candidates, power);
            _skillExecutor.Execute(ctx).Forget();
        }

        #endregion
    }
}