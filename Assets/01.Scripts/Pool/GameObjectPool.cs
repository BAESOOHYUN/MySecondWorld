using System;
using UnityEngine;
using UnityEngine.Pool;

namespace MySecondWorld.Pool
{

    public class GameObjectPool : IGameObjectPool, IDisposable
    {
        private readonly ObjectPool<GameObject> _pool;

        // 일반 Object.Instantiate 전용. Addressable 프리팹은 사용 불가
        // (Addressables.ReleaseInstance와 혼용 시 메모리 누수 발생)
        // TODO : AddressableObjectPool 클래스를 나중에 추가
        public GameObjectPool(Func<GameObject> createFunc, int defaultCapacity = 10, int maxSize = 100)
        {
            _pool = new ObjectPool<GameObject>(
                createFunc: createFunc,
                actionOnGet: obj => obj.SetActive(true),
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: obj =>
                {
                    // 플레이 모드를 빠져나오는 중에도 이 콜백이 온다(씬 언로드 → 풀 Dispose).
                    // 그 시점엔 이미 에디트 모드라 Destroy가 거부되고, 어차피 씬 오브젝트는
                    // 유니티가 통째로 정리하므로 여기서 할 일이 없다.
                    if (!Application.isPlaying) return;
                    UnityEngine.Object.Destroy(obj);
                },
                collectionCheck: true, // 개발중 true (이중 Return 시 예외로 즉시 발각), ★빌드시 false로 되돌림
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        public GameObject Rent() => _pool.Get();
        public void Return(GameObject obj) => _pool.Release(obj);
        public void Dispose() => _pool.Dispose();
    }
}