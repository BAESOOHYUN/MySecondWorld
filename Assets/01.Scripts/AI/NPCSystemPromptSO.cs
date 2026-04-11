using UnityEngine;

namespace MySecondWorld.AI
{
    //성격/세계관/토큰 제한 SO
    [CreateAssetMenu(fileName = "NPCSystemPromptSO", menuName = "NPC/System Prompt")]
    public class NPCSystemPromptSO : ScriptableObject
    {
        [Header("NPC Identity")]
        [SerializeField] private string npcName = "아리";

        [Tooltip("NPC 인스턴스 고유 식별자. Inspector 수동 입력 또는 [Generate GUID] 컨텍스트 메뉴로 자동 생성.")]
        [SerializeField] private string npcGuid = "";

        [TextArea(3, 6)]
        [SerializeField] private string personality = "당신은 마을 주민 아리입니다. 친절하고 호기심이 많습니다.";
        [TextArea(3, 6)]
        [SerializeField] private string worldContext = "여기는 중세 판타지 마을입니다. 마법과 몬스터가 존재합니다.";

        [Header("Inference Settings")]
        [Tooltip("응답 최대 토큰 수. 저사양 기기에서는 30~50 권장")]
        [SerializeField] private int maxResponseTokens = 40;

        [Header("Future: World Awareness")]
        [Tooltip("NPC가 알고 있는 장소 목록 (장기 세계 인식 확장용)")]
        [SerializeField] private string[] knownLocations = { };

        [Tooltip("NPC의 관계 정보 (예: '대장장이 철수:친구', '영주:두려움')")]
        [SerializeField] private string[] relationships = { };

        // 읽기 전용 프로퍼티
        public string NpcName => npcName;
        public string NpcGuid => npcGuid;

        /// <summary>
        /// Gemma에 넘길 시스템 프롬프트 문자열 조합
        /// </summary>
        public string BuildSystemPrompt()
        {
            var sb = new System.Text.StringBuilder();
            // AppendLine 문자열을 추가할 때 줄 바꿈 문자(\n, \r\n)를 함께 추가하는 메서드
            sb.AppendLine(personality);
            sb.AppendLine(worldContext);

            if (knownLocations.Length > 0)
                sb.AppendLine("알고 있는 장소: " + string.Join(", ", knownLocations));

            if (relationships.Length > 0)
                sb.AppendLine("관계: " + string.Join(", ", relationships));

            sb.AppendLine("짧고 자연스럽게 대답하세요. 한국어로 대화합니다.");
            return sb.ToString().Trim();
        }

#if UNITY_EDITOR
        [ContextMenu("Generate GUID")]
        private void GenerateGuid()
        {
            npcGuid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[NPCSystemPromptSO] GUID 생성 완료: {npcGuid}");
        }
#endif
    }
}