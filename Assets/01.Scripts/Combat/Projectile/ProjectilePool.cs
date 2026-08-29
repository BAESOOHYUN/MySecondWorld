using UnityEngine;
using UnityEngine.SceneManagement;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat
{
    // 투사체 풀 — (프리팹 × 씬) 단위로 공유한다.
    //
    // 씬으로 나누는 이유: 던전이 Additive 씬이라, 인스턴스가 엉뚱한 씬에 남으면
    // 그 씬이 언로드될 때 풀은 살아있는데 알맹이만 파괴된 상태가 된다.
    // 프리팹으로 나누는 이유: 몹마다 다른 화살을 쓸 수 있어서.
    //
    // 축 구현체(SO)는 무상태여야 하므로 풀은 SO 바깥인 여기가 소유한다.
    // 캐스터가 소유하지 않는 것도 중요하다 — 몬스터 한 마리 죽었다고 풀을 지우면 안 되니까.
    public static class ProjectilePool
    {
        private static GameObjectPoolRegistry<(int prefabId, int sceneHandle)> _registry
            = new GameObjectPoolRegistry<(int, int)>(defaultCapacity: 4, maxSize: 32);

        // Domain Reload OFF라 static이 Play 세션 사이에 안 지워진다.
        // 이전 세션의 풀은 알맹이가 이미 파괴됐으므로 Dispose하지 않고 통째로 버린다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnNewSession()
        {
            _registry = new GameObjectPoolRegistry<(int, int)>(defaultCapacity: 4, maxSize: 32);

            SceneManager.sceneUnloaded -= OnSceneUnloaded; // 중복 구독 방지 (Domain Reload OFF)
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public static GameObject Rent(GameObject prefab, Scene scene)
        {
            if (prefab == null)
            {
                Debug.LogError("[ProjectilePool] prefab이 null — 발사를 건너뜀");
                return null;
            }

            return _registry.Rent((prefab.GetInstanceID(), scene.handle), pool =>
            {
                var instance = Object.Instantiate(prefab);

                if (instance.TryGetComponent(out PooledObject pooled))
                    pooled.Init(pool);
                else
                    Debug.LogError($"[ProjectilePool] {prefab.name}에 PooledObject가 없다 — 반납이 불가능하다", prefab);

                SceneManager.MoveGameObjectToScene(instance, scene);
                return instance;
            });
        }

        // 씬이 내려가면 그 씬 풀도 함께 정리한다. 풀 수명을 캐스터가 아니라 씬에 묶는 지점.
        private static void OnSceneUnloaded(Scene scene)
            => _registry.DisposeWhere(key => key.sceneHandle == scene.handle);
    }
}