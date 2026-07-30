using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 순수 시각 프리팹(메시만, 스크립트/Rigidbody/Collider 없음)에
    // EnemyProjectile을 스폰 시점에 붙이는 팩토리. EnemyProjectile은 NetworkBehaviour가
    // 아니라(전투 시스템 로컬 전용) 스폰 이후 동적 부착에 제약이 없다(FloorItem과 대비되는 지점).
    public static class ProjectileFactory
    {
        public static EnemyProjectile Create(GameObject purePrefab, Vector3 position, Quaternion rotation, Vector3 colliderCenter, float colliderRadius)
        {
            if (purePrefab == null)
            {
                Debug.LogError("[ProjectileFactory] purePrefab이 비어있어 스폰을 건너뜀");
                return null;
            }

            var go = Object.Instantiate(purePrefab, position, rotation);
            var projectile = go.AddComponent<EnemyProjectile>(); // RequireComponent가 Rigidbody+SphereCollider도 같이 붙여줌, Awake에서 Kinematic/LayerMask 자체 설정
            projectile.Initialize(colliderCenter, colliderRadius);

            return projectile;
        }
    }
}
