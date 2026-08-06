using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using MySecondWorld.Combat;

namespace MySecondWorld.Combat.Companion
{
    // MonsterHitbox와 대칭 구조(Animation Event 훅). 단, Companion은 NetworkIdentity가 있는 오브젝트라
    // NetworkAnimator가 서버 애니메이션 상태를 클라이언트에도 복제 재생시킨다 — Animation Event(SendMessage)는
    // enabled 여부와 무관하게 호출되므로, isServer 가드가 없으면 클라이언트마다 데미지 판정이 중복 실행된다.
    public class CompanionHitbox : NetworkBehaviour
    {
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private LayerMask _targetLayerMask;
        [SerializeField] private CompanionStatsSO _stats;

        private SkillExecutor _skillExecutor;

        private void Awake()
        {
            if (_attackPoint == null || _stats == null)
            {
                Debug.LogError($"CompanionHitbox 참조 누락(_attackPoint 또는 _stats) {gameObject.name}", this);
                enabled = false;
                return;
            }

            _targetLayerMask = LayerMask.GetMask("Monster");
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() });
        }

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
            if (!isServer) return; // NetworkAnimator 복제 재생으로 클라에서도 호출됨 — 서버 인스턴스만 판정
            if (_skillExecutor == null) return;

            var candidates = Physics.OverlapSphere(_attackPoint.position, _attackRadius, _targetLayerMask, QueryTriggerInteraction.Ignore)
                .OrderBy(c => Vector3.Distance(_attackPoint.position, c.transform.position))
                .Select(c => c.GetComponentInParent<IDamageable>())
                .Where(d => d != null)
                .ToList();

            var ctx = new SkillContext(this.gameObject, candidates, _stats.Attack);
            _skillExecutor.Execute(ctx).Forget();
        }
    }
}
