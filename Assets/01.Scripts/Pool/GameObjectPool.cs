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
                actionOnDestroy: obj => UnityEngine.Object.Destroy(obj),
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