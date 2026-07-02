using UnityEngine;
using Mirror;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 서버 권한으로 관리하는 NPC 상태.
    /// isAlive, questStage, factionRelations를 SyncVar로 모든 클라이언트에 동기화.
    /// NPCBrain이 이 값을 읽어 systemPrompt 맥락에 반영한다.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkMatch))]
    public class NPCServerState : NetworkBehaviour, INPCServerState
    {
        // [ContextMenu]는 [Server] 어트리뷰트를 무시하지 않나 걱정될 수 있지만
        // 에디터에서 호출하면 서버 인스턴스에서 실행되는 거라 문제없음
        [ContextMenu("테스트 : NPC 사망")]
        private void DebugKill() => SetAlive(false);
        [ContextMenu("테스트 : NPC 부활")]
        private void DebugRevive() => SetAlive(true);
        [ContextMenu("테스트 : 퀘스트 1단계")]
        private void DebugQuestStage1() => SetQuestStage(1);

        // 서버에서 _isAlive를 바꾸면 클라이언트에서 OnAliveChanged()가 호출
        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool _isAlive = true;

        [SyncVar(hook = nameof(OnQuestStageChanged))]
        private int _questStage = 0;

        [SyncVar]
        private float _factionRelations = 0f;

        public bool IsAlive => _isAlive;
        public int QuestStage => _questStage;
        public float FactionRelations => _factionRelations;

        #region 서버 전용 변경 메서드
        // [Server] 이 메서드는 서버에서만 실행 가능하다.
        // SyncVar는 서버만 값을 바꿀 수 있다.
        [Server] public void SetAlive(bool value) => _isAlive = value;
        [Server] public void SetQuestStage(int value) => _questStage = value;
        [Server] public void SetFactionRelations(float value) => _factionRelations = Mathf.Clamp(value, -100f, 100f);
        #endregion

        #region SyncVar 훅
        private void OnAliveChanged(bool _, bool newValue)
        {
            // 클라이언트: NPC 비활성화 처리 (콜라이더, 렌더러 등)

            Debug.Log($"[NPCServerState] OnAliveChanged: {_} → {newValue}");
            gameObject.SetActive(newValue);
        }

        private void OnQuestStageChanged(int _, int newValue)
        {
            // NPCBrain이 퀘스트 단계 변경 알림 (필요시 확장)
            Debug.Log($"[NPCServerState] {name} questStage -> {newValue} ");
        }

        /// <summary>
        /// NPCBrain이 systemPrompt 빌드 시 호출.
        /// 서버 상태를 한 줄 문자열로 반환한다.
        /// </summary>
        public string GetContextLine()
        {
            return $"[서버 상태] 생존 : {_isAlive} / 퀘스트 단계 : {_questStage} / 파벌관계 : {_factionRelations:F0}";
        }
        #endregion

        //private void LoadFromSave(SaveData data)
        //{ 
        //}
    }
}