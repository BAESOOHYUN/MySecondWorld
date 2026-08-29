using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 보스 광역 슬램의 애니메이션 이벤트 훅. MonsterHitbox와 구조는 같고 타겟팅만 CircleAoESelector로 교체.
    public class BossAoEHitbox : MonoBehaviour
    {
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private BossStatsSO _stats;
        [SerializeField] private LayerMask _targetLayerMask;


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
                Debug.LogError($"BossAoEHitbox 참조 누락(_attackPoint 또는 _stats) on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            if (_stats.AoESkill == null)
            {
                // 컨텍스트 인자를 this가 아니라 _stats로 넘긴다 —
                // 콘솔에서 이 줄을 클릭하면 씬의 몬스터 클론이 아니라 문제의 스탯 에셋이 선택된다
                Debug.LogError($"BossAoEHitbox: {_stats.name}에 AoESkill 미할당", _stats);
                enabled = false;
                return;
            }
        }

        // Slam 애니메이션 클립의 충격 프레임에 Animation Event로 연결
        public void OnSlamImpact()
        {
            if (_stats == null || _stats.AoESkill == null) return;

            SphereAttackResolver.ResolveAndExecute(
                this.gameObject, _attackPoint.position, _stats.AoERadius, _targetLayerMask,
                _stats.Attack, _stats.AoESkill);
        }
    }
}