using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    public interface ICompanionServerState
    {
        bool IsAlive { get; }
        bool IsPresent { get; }
        event System.Action<bool> AliveChanged;

        void Revive(Vector3 position);
        void Initialize(Transform ownerTransform);
        void DebugKill();
        void MarkDead();
        void Dismiss();
        void Summon(Vector3 position);
        void ToggleFollowPaused();
    }
}