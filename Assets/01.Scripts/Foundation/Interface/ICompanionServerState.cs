using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    public interface ICompanionServerState
    {
        bool IsAlive { get; }
        void Revive(Vector3 position); // 부활 포지션
        void Initialize(Transform ownerTransform); // companion이 누굴 쫓아다녀야 하는지를 알려주는 값
        void DebugKill();
        void ToggleFollowPaused();
    }
}