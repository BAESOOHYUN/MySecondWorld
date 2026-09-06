using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 몬스터 근접 공격의 애니메이션 이벤트 훅.
    // PlayerAttackController.OnAttackHit()과 대칭 구조.
    public class MonsterHitbox : MonoBehaviour
    {
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private MonsterStatsSO _stats;
        private LayerMask _targetLayerMask; // "맞을 수 있는" 대상 레이어

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

        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        public void Initialize(Transform attackPoint, MonsterStatsSO stats)
        {
            _attackPoint = attackPoint;
            _stats = stats;
            _targetLayerMask = LayerMask.GetMask("Player", "NPC");   // 컴패니언이 NPC 레이어라 같이 훑는다
            if (_stats != null)
            {
                // 공격 사거리 내에서 공격할 때 헛스윙이 나지 않도록 최소 AttackRange 이상 반경 보정
                _attackRadius = Mathf.Max(_attackRadius, _stats.AttackRange + 0.3f);
            }
        }

        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
            // Animation Event(SendMessage)는 enabled=false여도 무시하고 호출됨
            // — Awake에서 참조 누락으로 꺼진 경우 방어
            if (_stats == null || _stats.AttackSkill == null) return;

            // 몬스터 전방 궤적 중심점과 반경을 넉넉하게 계산해 헛스윙 원천 차단 (딱 붙지 않아도 사거리 내 확실 타격)
            Vector3 center = transform.position + transform.forward * (_stats.AttackRange * 0.5f) + Vector3.up * 1.0f;
            float radius = Mathf.Max(_attackRadius, _stats.AttackRange + 0.8f);

            SphereAttackResolver.ResolveAndExecute(
                this.gameObject, center, radius, _targetLayerMask,
                _stats.Attack, _stats.AttackSkill);
        }
    }
}