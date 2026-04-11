using System.Threading;
using Cysharp.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace MySecondWorld.AI
{
    /// <summary>
    /// LLMUnity (llama.cpp 기반) 래퍼.
    /// NPCSystemPromptSO + NPCMemorySO.knownFacts를 LLMAgent.systemPrompt에 주입하고,
    /// GenerateResponseAsync로 비동기 응답을 생성한다.
    /// </summary>
    public class NPCBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCSystemPromptSO _systemPrompt;
        [SerializeField] private NPCMemorySO _memory;

        [Tooltip("씬에 배치된 LLMAgent 컴포넌트를 직접 연결한다.")]
        [SerializeField] private LLMAgent _llmAgent;

        // 외부 읽기 전용 접근 (NPCInteractable에서 npcName 접근용)
        public NPCSystemPromptSO SystemPrompt => _systemPrompt;

        // NPC 저장 키 — GUID 기반. GUID 미설정 시 GameObject 이름으로 폴백.
        // 주의: 동명 NPC 복수 배치 시 파일 충돌 방지를 위해 반드시 npcGuid를 설정할 것.
        private string NpcNameKey =>
            (_systemPrompt != null && !string.IsNullOrEmpty(_systemPrompt.NpcGuid))
                ? _systemPrompt.NpcGuid
                : name;

        // LLMAgent가 준비됐는지 확인
        private bool _isReady => _llmAgent != null && _llmAgent.llmAgent != null;

        // ── 초기화 ───────────────────────────────────────────────────────────────
        private void Start()
        {
            _memory?.Load(NpcNameKey);
            Debug.Log($"[NPCBrain] Start: _llmAgent={_llmAgent}, _systemPrompt={_systemPrompt}, _memory={_memory}");

            if (_llmAgent == null)
            {
                Debug.LogError($"[NPCBrain] LLMAgent가 연결되지 않았습니다. Inspector에서 LLMAgent 컴포넌트를 연결하세요.", this);
                return;
            }

            // systemPrompt 주입 — 대화 세션 시작 전에 설정해야 히스토리 컨텍스트가 보존됨
            _llmAgent.systemPrompt = BuildSystemPromptWithFacts();
            Debug.Log($"[NPCBrain] systemPrompt 주입 완료: {_llmAgent.systemPrompt}");
        }

        // ── 시스템 프롬프트 빌드 ─────────────────────────────────────────────────
        private string BuildSystemPromptWithFacts()
        {
            var sb = new System.Text.StringBuilder();

            if (_systemPrompt != null)
                sb.AppendLine(_systemPrompt.BuildSystemPrompt());

            if (_memory != null && _memory.KnownFacts != null && _memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("당신이 기억해야 할 중요 사실:");
                foreach (string fact in _memory.KnownFacts)
                    sb.AppendLine($"- {fact}");
            }

            return sb.ToString().Trim();
        }

        // ── 응답 생성 ────────────────────────────────────────────────────────────
        public async UniTask<string> GenerateResponseAsync(
            string userInput, CancellationToken ct = default)
        {
            Debug.Log($"[NPCBrain] GenerateResponseAsync 시작: isReady={_isReady}, input={userInput}");
            if (!_isReady)
            {
                Debug.LogWarning($"[NPCBrain] LLMAgent가 아직 준비되지 않았습니다. _llmAgent={_llmAgent}, llmAgent={_llmAgent?.llmAgent}");
                return string.Empty;
            }

            ct.Register(() => _llmAgent.CancelRequests());
            string response = await _llmAgent.Chat(userInput).AsUniTask().AttachExternalCancellation(ct);
            Debug.Log($"[NPCBrain] 응답 수신: {response}");

            if (this == null) return string.Empty;

            _memory?.Save(NpcNameKey);

            return response;
        }

        // ── 정리 ─────────────────────────────────────────────────────────────────
        private void OnDestroy()
        {
            _memory?.Save(NpcNameKey);
        }
    }
}
