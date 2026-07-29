using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 보스 광역 슬램의 애니메이션 이벤트 훅. MonsterHitbox와 구조는 같고 타겟팅만 CircleAoESelector로 교체.
    public class BossAoEHitbox : MonoBehaviour
    {
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private BossStatsSO _stats;
        [SerializeField] private LayerMask _targetLayerMask;

        private SkillExecutor _skillExecutor;

        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        public void Initialize(Transform attackPoint, BossStatsSO stats)
        {
            _attackPoint = attackPoint;
            _stats = stats;
            _targetLayerMask = LayerMask.GetMask("Player");
        }

        private void Awake()
        {
            if (_attackPoint == null || _stats == null)
            {
                Debug.LogError($"BossAoEHitbox 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _skillExecutor = new SkillExecutor(
                new CircleAoESelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() });
        }

        // Slam 애니메이션 클립의 충격 프레임에 Animation Event로 연결
        public void OnSlamImpact()
        {
            var candidates = Physics.OverlapSphere(_attackPoint.position, _stats.AoERadius, _targetLayerMask, QueryTriggerInteraction.Ignore)
                .Select(c => c.GetComponentInParent<IDamageable>())
                .Where(d => d != null)
                .Distinct()
                .ToList();

            var ctx = new SkillContext(this.gameObject, candidates, _stats.Attack);
            _skillExecutor.Execute(ctx).Forget();
        }
    }
}