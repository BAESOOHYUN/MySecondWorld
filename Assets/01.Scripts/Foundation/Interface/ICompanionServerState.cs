using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    public interface ICompanionServerState
    {
        bool IsAlive { get; }
        bool IsPresent { get; }
        event System.Action<bool> AliveChanged;

        void Initialize(Transform ownerTransform);
        void DebugKill();
        void MarkDead();
        void Revive(Vector3 position);
        void Dismiss();
        void Summon(Vector3 position);
        void Warp(Vector3 position); // 포탈/낙하 복귀 시 위치 강제 동기화
        void ToggleFollowPaused();
    }
}