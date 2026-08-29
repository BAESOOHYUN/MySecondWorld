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


        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        public void Initialize(Transform attackPoint, MonsterStatsSO stats)
        {
            _attackPoint = attackPoint;
            _stats = stats;
            _targetLayerMask = LayerMask.GetMask("Player");
        }

        private void Awake()
        {
            if (_attackPoint == null || _stats == null)
            {
                Debug.LogError($"MonsterHitbox 참조 누락(_attackPoint 또는 _stats) on {gameObject.name}", this);
                enabled = false;
                return;
            }

            if (_stats.AttackSkill == null)
            {
                Debug.LogError($"MonsterHitbox: {_stats.name}에 AttackSkill 미할당", _stats);
                enabled = false;
                return;
            }
        }

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
            // Animation Event(SendMessage)는 enabled=false여도 무시하고 호출됨
            // — Awake에서 참조 누락으로 꺼진 경우 방어
            if (_stats == null || _stats.AttackSkill == null) return;

            SphereAttackResolver.ResolveAndExecute(
                this.gameObject, _attackPoint.position, _attackRadius, _targetLayerMask,
                _stats.Attack, _stats.AttackSkill);
        }
    }
}