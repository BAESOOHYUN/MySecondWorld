using System;
using UnityEngine;
using Mirror;
using VContainer;
using VContainer.Unity;
using MessagePipe;
using R3;
using MySecondWorld.AI;

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
        [Inject] private ISubscriber<NPCDialogueEvent> _subscriber;
        private IDisposable _subscription; //구독 영수증. 구독을 끊는 것도 리소스 정리의 일종이라 Dispose()로 통일. 

        private void Awake()
        { 
            // Awake에서 LifetimeScope.Find<NPCScope>()를 직접 호출하면 타이밍 이슈로 에러 발생 가능
            // 만약 컴포넌트 주입이 필요하다면 NPCScope.Configure에 등록하는 것이 안전합니다.
            try
            {
                var scope = GetComponent<NPCScope>() ?? LifetimeScope.Find<NPCScope>();
                if (scope != null && scope.Container != null)
                    scope.Container.Inject(this);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NPCDialogueSync] Inject 지연/스킵: {ex.Message}");
            }
        }

        private void OnEnable()
        {
            if (_subscription != null) return; // Awake 전 OnEnable 방어
            //_subscriber.Subscribe 구독 신청
            _subscription = _subscriber.Subscribe(e => HandleDialogueRequested(e.NpcName, e.Message));
        }

        private void OnDisable()
        {
            _subscription?.Dispose(); // 구독 해지
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