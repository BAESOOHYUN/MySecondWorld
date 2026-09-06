using UnityEngine;
using MySecondWorld.Combat.Vfx;

namespace MySecondWorld.Combat.Monster
{
    // 보스 광역 슬램의 애니메이션 이벤트 훅. MonsterHitbox와 구조는 같고 타겟팅만 CircleAoESelector로 교체.
    public class BossAoEHitbox : MonoBehaviour
    {
        [SerializeField] private Transform _attackPoint;
        [SerializeField] private BossStatsSO _stats;
        private LayerMask _targetLayerMask;

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

        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        public void Initialize(Transform attackPoint, BossStatsSO stats)
        {
            _attackPoint = attackPoint;
            _stats = stats;
            _targetLayerMask = LayerMask.GetMask("Player", "NPC");   // 컴패니언이 NPC 레이어라 같이 훑는다
        }

        // Slam 애니메이션 클립의 충격 프레임에 Animation Event로 연결
        public void OnSlamImpact()
        {
            if (_stats == null || _stats.AoESkill == null) return;

            // 보스 전방 지면 충격 지점(전방 1.5m)을 중심으로 광역 판정 실행
            Vector3 center = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;

            // 슬램 애니메이션 자체가 NetworkAnimator로 전원 동기화되므로 이 Animation Event는
            // 모든 피어에서 각자 로컬로 호출된다 — 별도 RPC 없이 Instantiate해도 전원에게 보인다.
            if (_stats.AoeSlamVfxPrefab != null)
                CombatVfxSpawner.Spawn(_stats.AoeSlamVfxPrefab, center, Quaternion.identity);

            SphereAttackResolver.ResolveAndExecute(
                this.gameObject, center, _stats.AoERadius, _targetLayerMask,
                _stats.Attack, _stats.AoESkill);
        }
    }
}