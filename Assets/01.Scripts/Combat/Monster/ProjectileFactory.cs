using UnityEngine;
using UnityEngine.SceneManagement;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat.Monster
{
    // 순수 시각 프리팹(메시만, 스크립트/Rigidbody/Collider 없음)에
    // EnemyProjectile을 스폰 시점에 붙이는 팩토리. EnemyProjectile은 NetworkBehaviour가
    // 아니라(전투 시스템 로컬 전용) 스폰 이후 동적 부착에 제약이 없다(FloorItem과 대비되는 지점).
    public static class ProjectileFactory
    {
        // 풀 createFunc 전용 — 인스턴스가 최초로 태어날 때 1회만 호출됨
        public static GameObject CreatePooled(GameObject purePrefab, Vector3 colliderCenter, float colliderRadius, IGameObjectPool pool, Scene scene)
        {
            // RangedMonsterHitbox.Awake가 이미 null을 걸러내 실제로는 도달하지 않는 방어코드.
            // 만약 도달하면 여기서 반환하는 null이 GameObjectPool.Rent() 내부(actionOnGet)에서 NRE로 즉시 이어짐 — "안전하게 스킵"은 아님
            if (purePrefab == null)
            {
                Debug.LogError("[ProjectileFactory] purePrefab이 비어있어 스폰을 건너뜀");
                return null;
            }
            var go = Object.Instantiate(purePrefab); // position/rotation 인자 없음 — Rent 이후 캐스트 쪽에서 SetPositionAndRotation
            var projectile = go.AddComponent<EnemyProjectile>();
            projectile.Initialize(colliderCenter, colliderRadius);

            var pooled = go.GetComponent<PooledObject>();
            pooled.Init(pool);
            SceneManager.MoveGameObjectToScene(go, scene);

            return go; // GameObject 반환 — Func<GameObject> 시그니처에 맞춰야 함
        }
    }
}
