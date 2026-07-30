using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "RangedMonsterStatsSO", menuName = "Combat/Ranged Monster Stats")]
    public class RangedMonsterStatsSO : MonsterStatsSO
    {
        // MaxHP/Attack/AttackRange(사격 사거리로 재해석)
        // AttackCooldown(발사 쿨다운으로 재해석)
        // DetectRange/MoveSpeed는 베이스 그대로 재사용.
        [SerializeField] private float _retreatDistance = 3f; // 이 거리보다 가까워지면 후퇴
        [SerializeField] private float _projectileSpeed = 15f;
        [SerializeField] private GameObject _projectilePrefab; // 순수 프리팹(스크립트 없음) — MonsterFactory가 RangedMonsterHitbox에 주입, 몹마다 다른 투사체를 쓸 수 있어 데이터로 관리

        // 순수 프리팹엔 SphereCollider가 없어서(팩토리가 동적 부착) 판정 위치·크기를 데이터로 들고 있는다.
        // 화살촉 등 모델마다 판정 지점이 달라 MonsterStatsSO.BodyColliderCenter/Radius와 동일한 이유로 데이터화.
        [Header("투사체 콜라이더 (SphereCollider)")]
        [SerializeField] private Vector3 _projectileColliderCenter;
        [SerializeField] private float _projectileColliderRadius = 0.1f;

        public float RetreatDistance => _retreatDistance;
        public float ProjectileSpeed => _projectileSpeed;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public Vector3 ProjectileColliderCenter => _projectileColliderCenter;
        public float ProjectileColliderRadius => _projectileColliderRadius;
    }
}