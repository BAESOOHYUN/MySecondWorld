using System;
using UnityEngine;

namespace MySecondWorld.Pool
{
    public interface IGameObjectPool : IDisposable
    {
        GameObject Rent();
        void Return(GameObject obj);
    }
}