using UnityEngine;
using Mirror;
using System;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 파티 전투 등 특수 상황에서 NPC 대화를 다른 플레이어와 공유할 때만 사용.
    /// 평상시 1:1 대화는 클라이언트 로컬에서 처리 — 이 컴포넌트 비활성화 상태로 유지.
    /// -흐름-
    /// 파티 전투 시작 → npcDialogueSync.enabled = true  → OnEnable → 이벤트 구독
    /// 전투 종료    → npcDialogueSync.enabled = false → OnDisable → 이벤트 해제
    /// 대화 발생 시 → NPCDialogueSync.OnDialogueRequested?.Invoke(npcName, message)
    ///             → 활성화된 NPC만 수신 → RpcShowDialogue
    /// </summary>
    public class NPCDialogueSync : NetworkBehaviour
    {
        public static event Action<string, string> OnDialogueRequested;
        private void OnEnable()
        {
            OnDialogueRequested += HandleDialogueRequested;
        }

        private void OnDisable()
        {
            OnDialogueRequested -= HandleDialogueRequested;
        }

        private void HandleDialogueRequested(string npcName, string message)
        {
            if (!isServer) return;
            RpcShowDialogue(npcName, message); 
        }

        /// <summary>
        /// 서버에서 특정 NPC 발화를 파티 전체에 브로드캐스트.
        /// 예: 몬스터 NPC가 파티를 향해 외치는 대사.
        /// </summary>
        [Server]
        public void BroadcastDialogue(string npcName, string message)
        {
            RpcShowDialogue(npcName, message);
        }

        [ClientRpc]
        private void RpcShowDialogue(string npcName, string message) 
        {
            // TODO : UIManager를 통해 월드 공유 대화창에 표시
            Debug.Log($"[NPCDialogueSync]  {npcName}: {message}");
        }
    }
}