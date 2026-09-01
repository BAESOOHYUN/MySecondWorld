using UnityEngine;

namespace MySecondWorld.Pool
{
    public class PooledObject : MonoBehaviour
    {
        private IGameObjectPool _pool;
        public void Init(IGameObjectPool pool) => _pool = pool;
        public void ReturnToPool() => _pool?.Return(this.gameObject);
    }
}