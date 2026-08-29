using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 원거리 몹의 발사 애니메이션 이벤트 훅. PlayerAttackController와 대칭 구조.
    public class RangedMonsterHitbox : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private MonsterAI _monsterAI;
        [SerializeField] private RangedMonsterStatsSO _stats;

        [Header("원거리 판정")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private GameObject _projectilePrefab; // 순수 프리팹(스크립트 없음) — ProjectileFactory가 스폰 시점에 Projectile을 붙임
        [SerializeField] private float _aimHeightOffset = 1.4f; // 플레이어 피벗이 발밑이라 가슴 높이 정도로 보정

        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        // _projectilePrefab은 스탯 SO(RangedMonsterStatsSO.ProjectilePrefab)에서 가져온다 — 몹마다 투사체가 다를 수 있어 데이터로 관리.
        public void Initialize(Transform spawnPoint, RangedMonsterStatsSO stats)
        {
            _monsterAI = GetComponent<MonsterAI>();
            _spawnPoint = spawnPoint;
            _stats = stats;
            _projectilePrefab = stats.ProjectilePrefab;
        }

        private void Awake()
        {
            if (_monsterAI == null || _stats == null || _spawnPoint == null || _projectilePrefab == null)
            {
                Debug.LogError($"RangedMonsterHitbox 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }
        }

        private void OnDestroy()
        {
        }

        // Shoot 애니메이션 클립의 발사(화살이 손을 떠나는) 프레임에 Animation Event로 연결
        public void OnShootRelease()
        {
        }
    }
}