using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    public interface ICompanionServerState
    {
        bool IsAlive { get; }
        void Revive(Vector3 position); // 부활 포지션
    }
}