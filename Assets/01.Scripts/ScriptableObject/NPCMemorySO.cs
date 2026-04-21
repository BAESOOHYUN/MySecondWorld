using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MySecondWorld.AI
{
    /// <summary>
    /// LLM 컨텍스트 밖의 영구 상태 저장소.
    /// 대화 히스토리는 LLMAgent.save가 담당한다.
    /// 이 SO는 LLM이 잊어버리거나 애초에 알 수 없는 게임 시스템 상태를 보존한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NPCMemorySO", menuName = "NPC/Memory")]
    public class NPCMemorySO : ScriptableObject
    {
        [Header("Relationship")]
        [Tooltip("플레이어와의 호감도/적대도 수치. 양수=호감, 음수=적대")]
        [SerializeField] private float relationScore = 0f;

        [Tooltip("파벌 관계 수치. 추후 Mirror SyncVar 연동 예정")]
        [SerializeField] private float factionRelation = 0f;

        [Header("Quest")]
        [Tooltip("퀘스트 진행 단계 (0 = 시작 전)")]
        [SerializeField] private int questStage = 0;

        [Tooltip("퀘스트 전달 여부")]
        [SerializeField] private bool questGiven = false;

        [Header("Meeting History")]
        [Tooltip("처음 만난 날짜 (인게임 또는 실제 날짜 문자열)")]
        [SerializeField] private string firstMetDate = "";

        [Tooltip("처음 만난 장소")]
        [SerializeField] private string firstMetLocation = "";

        [Tooltip("누적 대화 횟수")]
        [SerializeField] private int conversationCount = 0;

        [Header("Known Facts")]
        [Tooltip("컨텍스트 오버플로우로 잘린 중요 사실 목록. NPCBrain이 세션 시작 시 systemPrompt에 주입한다.")]
        [SerializeField] private List<string> knownFacts = new();

        // 읽기 전용 프로퍼티
        public float RelationScore => relationScore;
        public float FactionRelation => factionRelation;
        public int QuestStage => questStage;
        public bool QuestGiven => questGiven;
        public string FirstMetDate => firstMetDate;
        public string FirstMetLocation => firstMetLocation;
        public int ConversationCount => conversationCount;
        public List<string> KnownFacts => knownFacts;

        // Mutator : 변수의 변경을 통제하기 위해 사용하는 메서드
        public void IncrementConversationCount() => conversationCount++;

        public void SetFirstMet(string date, string location)
        {
            if (!string.IsNullOrEmpty(firstMetDate))
                return; // 첫만남 날짜 이미 기록됐으면 스킵
            firstMetDate = date;
            firstMetLocation = location;
        }

        public void AddRelationScore(float delta)
        {
            relationScore = Mathf.Clamp(relationScore + delta, -100f, 100f);
        }

        public void AddKnowFact(string fact)
        {
            if (!string.IsNullOrEmpty(fact) && !knownFacts.Contains(fact))
                knownFacts.Add(fact);
        }


        // 저장 경로
        private string SavePath(string npcKey)
        {
           return Path.Combine(Application.persistentDataPath, $"npc_memory_{npcKey}.json");
        }

        // 저장/불러오기
        [Serializable]
        private class SaveData
        {
            public float relationScore;
            public float factionRelation;
            public int questStage;
            public bool questGiven;
            public string firstMetDate;
            public string firstMetLocation;
            public int conversationCount;
            public List<string> knownFacts;
        }

        public void Save(string npcKey)
        {
            var data = new SaveData
            {
                relationScore = relationScore,
                factionRelation = factionRelation,
                questStage = questStage,
                questGiven = questGiven,
                firstMetDate = firstMetDate,
                firstMetLocation = firstMetLocation,
                conversationCount = conversationCount,
                knownFacts = knownFacts
            };
            File.WriteAllText(SavePath(npcKey), JsonUtility.ToJson(data));
        }

        public void Load(string npcKey)
        {
            string path = SavePath(npcKey);
            if (!File.Exists(path))
                return;

            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null)
                return;

            relationScore = data.relationScore;
            factionRelation = data.factionRelation;
            questStage = data.questStage;
            questGiven = data.questGiven;
            firstMetDate = data.firstMetDate ?? "";
            firstMetLocation = data.firstMetLocation ?? "";
            conversationCount = data.conversationCount;
            knownFacts = data.knownFacts ?? new List<string>();
        }

        public void Clear(string npcKey)
        {
            relationScore = 0f;
            factionRelation = 0f;
            questStage = 0;
            questGiven = false;
            firstMetDate = "";
            firstMetLocation = "";
            conversationCount = 0;
            knownFacts.Clear();

            string path = SavePath(npcKey);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
