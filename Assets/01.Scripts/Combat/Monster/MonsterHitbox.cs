using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 몬스터 근접 공격의 애니메이션 이벤트 훅.
    // PlayerAttackController.OnAttackHit()과 대칭 구조.
    public class MonsterHitbox : MonoBehaviour
    {
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private LayerMask _targetLayerMask; // "맞을 수 있는" 대상 레이어
        [SerializeField] private MonsterStatsSO _stats;

        private SkillExecutor _skillExecutor;

        private void Awake()
        {
            if (_attackPoint == null || _stats == null)
            {
                Debug.LogError($"MonsterHitbox 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() });
        }

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
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