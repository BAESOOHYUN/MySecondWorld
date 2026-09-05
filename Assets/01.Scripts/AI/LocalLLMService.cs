using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LLMUnity;
using VContainer;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MySecondWorld.AI
{
    /// <summary>
    /// LLMForUnity (llama.cpp 기반) 래퍼
    /// </summary>
    public class LocalLLMService : MonoBehaviour, ILLMService
    {
        private LLMAgent _llmAgent; // (undreamai/LLMForUnity)
        private NPCSystemPromptSO _systemPrompt;
        private static bool _isExitingPlayMode = false;
#if UNITY_EDITOR
        // 에디터 로드 시 1회 실행 — PlayMode 상태 변화 감지 이벤트 사전 등록
        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if(state == PlayModeStateChange.ExitingPlayMode)
                    _isExitingPlayMode = true;    // ← "종료 중" 플래그 ON
                else if (state == PlayModeStateChange.EnteredEditMode)
                    _isExitingPlayMode = false;   // ← Edit Mode 진입 후 리셋
            };
        }
#endif

        // NPC 저장 키 — GUID 기반. GUID 미설정 시 GameObject 이름으로 폴백.
        // 주의: 동명 NPC 복수 배치 시 파일 충돌 방지를 위해 반드시 npcGuid를 설정할 것.
        private string NpcNameKey => (_systemPrompt != null && !string.IsNullOrEmpty(_systemPrompt.NpcGuid))
                                    ? _systemPrompt.NpcGuid : name;
        public bool IsReady => _llmAgent != null && _llmAgent.llmAgent != null;
        
        [Inject]
        private void Construct(NPCSystemPromptSO systemPrompt)
        {
            _systemPrompt = systemPrompt;
        }

        private void Awake()
        {
            _llmAgent = GetComponent<LLMAgent>();
        }

        private void Start()
        {
            if (_llmAgent == null)
            {
                Debug.LogError("[LocalLLMService] LLMAgent가 연결되지 않았습니다.", this);
                return;
            }
            _llmAgent.save = NpcNameKey + ".json";
            _llmAgent.slot = _systemPrompt != null ? Mathf.Abs(_systemPrompt.NpcGuid.GetHashCode()) : 0 ;
        }

        private void OnDestroy()
        {
            if (_isExitingPlayMode) return; // Play Mode 종료 중 → _isExitingPlayMode = true → CancelRequests() 스킵 → 에디터 안 뻗음
            _llmAgent?.CancelRequests(); // 게임 중 NPC Destroy(예: 씬 전환) → _isExitingPlayMode = false → CancelRequests() 정상 실행
        }

        public void SetSystemPrompt(string prompt, int maxTokens)
        {
            if (_llmAgent != null)
            {
                _llmAgent.systemPrompt = prompt;
                _llmAgent.numPredict = maxTokens;
            }
            // numPredict: 모델이 EOS 토큰(LLM이 학습할 때 문장/응답이 끝나는 자리마다 심어두는 특수 토큰) 없이도 이 개수에서 강제로 생성을 멈추게 하는 상한.
            // Groq의 max_tokens와 같은 개념 — 저사양 기기에서 답변이 쓸데없이 길어져 느려지는 걸 막는다.
            // NPCSystemPromptSO.maxResponseTokens 하나로 Groq/로컬 두 백엔드의 응답 길이를 동일 기준으로 통제.
        }

        public void ClearHistory() => _llmAgent?.ClearHistory();

        public async UniTask<string> ChatAsync(string userInput, Action<string> onToken, CancellationToken ct)
        {
            if (!IsReady) return string.Empty;
            
            ct.Register(() =>
            {
                if (!_isExitingPlayMode)
                    _llmAgent.CancelRequests();
            });

            return await _llmAgent.Chat(userInput, onToken).AsUniTask().AttachExternalCancellation(ct);
            //                  └─ LLM 추론 시작, Task 반환   └─ Task<string> → UniTask<string> 으로 변환
            // .Chat()은 내부적으로 취소를 지원하지 않으므로 .AsUniTask()만 하면 취소 신호(ct)를 보내도 LLM 작업이 멈추지 않는다.
            // AttachExternalCancellation(ct)는 "이 토큰이 취소되면 UniTask도 취소된 것으로 간주해라"는 뜻. 외부 취소 신호 연결.
        }

        public async UniTask<string> ExtractFactAsync(string prompt, CancellationToken ct)
        {
            if (!IsReady) return string.Empty;

            // onToken 스트리밍 콜백. grammar 응답 형식 제약 (JSON 강제 등에 쓰는 옵션) → null,
            // addToHistory:false → 팩트 추출은 일회성 분석이므로 대화 히스토리에 남기지 않음
            // 대화 히스토리에 섞이면 NPC가 팩트 추출 프롬프트를 실제 대화로 오해하기 때문에 false 로 기억 못하게 막음.
            return await _llmAgent.Chat(prompt, null, null, addToHistory: false).AsUniTask().AttachExternalCancellation(ct);
        }
    }
}

// ── _isExitingPlayMode 존재 이유 ───────────────────────────────────────────
// 문제: Play 종료 시 LLMInitializer.CleanupNative()가 native handle을 먼저 해제한 뒤
//       OnDestroy()의 CancelRequests()가 해제된 handle에 접근(dangling pointer)
//       → LLM_Cancel() P/Invoke → access violation → 에디터 강제종료 💥
//
// 해결:
// ■ 버튼 클릭
//   ├─ ExitingPlayMode  → _isExitingPlayMode = true
//   ├─ CleanupNative()  → native handle 해제
//   └─ OnDestroy()      → _isExitingPlayMode == true → CancelRequests() 스킵 ✅
//      EnteredEditMode  → _isExitingPlayMode = false (다음 실행 준비)
// ────────────────────────────────────────────────────────────────────────────