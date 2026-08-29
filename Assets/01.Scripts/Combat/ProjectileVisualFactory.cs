using UnityEngine;
using UnityEngine.SceneManagement;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat
{
    public static class ProjectileVisualFactory
    {
        public static GameObject CreatePooled(GameObject purePrefab, IGameObjectPool pool, Scene scene)
        {
            if (purePrefab == null)
            {
                Debug.LogError("[ProjectileVisualFactory] purePrefab이 비어있어 스폰을 건너뜀");
                return null;
            }
            var instance = Object.Instantiate(purePrefab);
            instance.AddComponent<ProjectileVisualMover>();

            var pooled = instance.AddComponent<PooledObject>();
            pooled.Init(pool);
            SceneManager.MoveGameObjectToScene(instance, scene);

            return instance;
        }
    }
}