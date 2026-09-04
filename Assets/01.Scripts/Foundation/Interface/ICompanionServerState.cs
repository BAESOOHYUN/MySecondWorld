using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    public interface ICompanionServerState
    {
        bool IsAlive { get; }
        bool IsPresent { get; }
        event System.Action<bool> AliveChanged;

        void Initialize(Transform ownerTransform);
        void ResetCombatState(); // 씬 전환처럼 기존 타겟 참조가 무의미해진 시점에 전투 상태(타겟/상태머신) 초기화
        void DebugKill();
        void MarkDead();
        void Revive(Vector3 position);
        void Dismiss();
        void Summon(Vector3 position);
        void Warp(Vector3 position); // 포탈/낙하 복귀 시 위치 강제 동기화
        void ToggleFollowPaused();
    }
}