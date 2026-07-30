using UnityEngine;

namespace MySecondWorld.Pool
{
    public interface IGameObjectPool
    {
        GameObject Rent();
        void Return(GameObject obj);
    }
}