using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using LLMUnity;
using MessagePipe;
using VContainer;
using MySecondWorld.Network;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [SerializeField] private NPCServerState _serverState; // 선택적, 없으면 null 허용

        [Tooltip("씬에 배치된 LLMAgent 컴포넌트를 직접 연결한다.")]
        [SerializeField] private LLMAgent _llmAgent;

        [ContextMenu("메모리 초기화")]
        private void DebugClearMemory() => ResetMemory();

        // 외부 읽기 전용 접근 (NPCInteractable에서 npcName 접근용)
        public NPCSystemPromptSO SystemPrompt => _systemPrompt;

        // _responsePub와 _thinkingPub는 람다 안에서 쓰인다.
        // 람다는 Construct()가 끝난 훨씬 나중에 , 플레이어가 메시지를 보낼 때마다 실행된다.
        // 그 시점에도 살아있어야 하므로 필드에 저장
        private IPublisher<NPCResponseReceivedEvent> _responsePub;
        private IPublisher<NPCThinkingStartedEvent> _thinkingPub;
        private IPublisher<NPCDialogueEvent> _dialoguePub;

        // [Inject]가 붙은 메서드를 VContainer가 찾아서,
        // 파라미터 타입에 맞는 인스턴스를 컨테이너에서 꺼내 자동으로 호출한다.
        // 파라미터에 뭘 넣는가의 기준: 이 클래스가 동작하는 데 외부에서 받아야 하는 것들
        // 호출 순서 Awake() -> VContainer가 Construct() 호출 ←[Inject]주입 -> Start() 호출
        // Start()에서 주입된 필드를 안전하게 쓸 수 있다.
        [Inject]
        private void Construct(
            ISubscriber<string, PlayerMessageSentEvent> messageSub,
            IPublisher<NPCResponseReceivedEvent> responsePub,
            IPublisher<NPCThinkingStartedEvent> thinkingPub,
            IPublisher<NPCDialogueEvent> dialoguePub
            )
        {
            _responsePub = responsePub;
            _thinkingPub = thinkingPub;
            _dialoguePub = dialoguePub; 

            // NpcNameKey(GUID)를 키로 구독 — 이 NPC에게 온 메시지만 수신된다
            messageSub
                .Subscribe(NpcNameKey, async evt =>
                {
                    try
                    {
                        _thinkingPub.Publish(new NPCThinkingStartedEvent(NpcNameKey));
                        var response = await GenerateResponseAsync(evt.Text, evt.CT);
                        _responsePub.Publish(new NPCResponseReceivedEvent(
                                NpcNameKey,
                                _systemPrompt?.NpcName ?? name,
                                response));
                        _dialoguePub.Publish(new NPCDialogueEvent { NpcName = NpcNameKey, Message = response });
                    }
                    catch (OperationCanceledException)
                    {
                        // 취소는 정상 흐름 — 무시
                    }
                })
                .AddTo(destroyCancellationToken);
        }

        // NPC 저장 키 — GUID 기반. GUID 미설정 시 GameObject 이름으로 폴백.
        // 주의: 동명 NPC 복수 배치 시 파일 충돌 방지를 위해 반드시 npcGuid를 설정할 것.
        private string NpcNameKey =>
            (_systemPrompt != null && !string.IsNullOrEmpty(_systemPrompt.NpcGuid))
                ? _systemPrompt.NpcGuid
                : name;

        // LLMAgent가 준비됐는지 확인
        private bool _isReady => _llmAgent != null && _llmAgent.llmAgent != null;

        //static = NPCBrain 인스턴스가 여러 개 있어도 하나의 값을 공유
        // 역할: "지금 Play Mode 종료 중이야?" 플래그
        // 기본값 false → Play Mode 종료 신호 오면 true
        private static bool s_isExitingPlayMode;

#if UNITY_EDITOR
        // Unity Editor가 시작될 때 자동으로 이 메서드를 1회 실행
        // Play Mode와 무관하게, 에디터 열릴 때마다 호출됨
        // 왜 필요? → Play Mode 감지 이벤트를 미리 등록해 두기 위해
        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    s_isExitingPlayMode = true;                         // ← "종료 중" 플래그 ON
                else if (state == PlayModeStateChange.EnteredEditMode)
                    s_isExitingPlayMode = false;                        // ← Edit Mode 진입 후 리셋
            };
        }
        // ■ 버튼 클릭
        //  → ExitingPlayMode 감지 → s_isExitingPlayMode = true
        //  → (정리 작업들...)
        //  → EnteredEditMode 감지 → s_isExitingPlayMode = false (다음 실행 준비)
#endif

        private void Start()
        {
            _memory?.Load(NpcNameKey);

            // 서버 상태 => 로컬 메모리 동기화
            if (_serverState != null && _memory != null)
                _memory.SyncFactionFromServer(_serverState.FactionRelations);

            // 첫만남 기록
            if( _memory != null && string.IsNullOrEmpty(_memory.FirstMetDate))
            {
                string location = SystemPrompt != null ? _systemPrompt.NpcLocation : "알 수 없음";
                _memory.SetFirstMet(System.DateTime.Now.ToString("yyyy-MM-dd"), location);
            }

            if (_llmAgent == null)
            {
                Debug.LogError($"[NPCBrain] LLMAgent가 연결되지 않았습니다.", this);
                return;
            }
            _llmAgent.save = NpcNameKey + ".json";
            _llmAgent.slot = _systemPrompt != null
                ? Mathf.Abs(_systemPrompt.NpcGuid.GetHashCode())
                : 0;
            _llmAgent.systemPrompt = BuildSystemPromptWithFacts();
        }

        private void OnDestroy()
        {
            _memory?.Save(NpcNameKey);

            if (s_isExitingPlayMode) return; // Play Mode 종료 중 → s_isExitingPlayMode = true → CancelRequests() 스킵 → 에디터 안 뻗음
            _llmAgent?.CancelRequests(); // 게임 중 NPC Destroy(예: 씬 전환) → s_isExitingPlayMode = false → CancelRequests() 정상 실행
        }

        // 시스템 프롬프트 빌드
        private string BuildSystemPromptWithFacts()
        {
            using var sb = ZString.CreateStringBuilder();

            if (_systemPrompt != null)
                sb.AppendLine(_systemPrompt.BuildSystemPrompt());

            if (_serverState != null)
            {
                sb.AppendLine();
                sb.AppendLine(_serverState.GetContextLine());
            }

            if (_memory != null && _memory.KnownFacts != null && _memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("당신이 기억해야 할 중요 사실:");
                foreach (string fact in _memory.KnownFacts)
                {
                    sb.Append("- ");
                    sb.AppendLine(fact);
                }
            }
            return sb.ToString().Trim();
        }

        // 응답 생성
        private async UniTask<string> GenerateResponseAsync(
            string userInput, CancellationToken ct = default)
        {
            if (!_isReady)
            {
                Debug.LogWarning($"[NPCBrain] LLMAgent가 아직 준비되지 않았습니다. _llmAgent={_llmAgent}, llmAgent={_llmAgent?.llmAgent}");
                return string.Empty;
            }

            ct.Register(() => { if (!s_isExitingPlayMode) _llmAgent.CancelRequests(); });

            float t0 = Time.realtimeSinceStartup;
            string response = await _llmAgent.Chat(userInput).AsUniTask().AttachExternalCancellation(ct);
            Debug.Log($"[NPCBrain] 추론 시간: {Time.realtimeSinceStartup - t0:F2}s");

            if (this == null) 
                return string.Empty;

            string extractPrompt = $"아래 대화에서 '{userInput}'을 말한 사람(플레이어)에 대해 새롭게 알게 된 사실이 있으면 " +
                                    $"'[플레이어이름]은 [사실].' 형식으로 한 줄만 써줘. " +
                                    $"NPC 자신에 대한 내용은 절대 쓰지 마. 플레이어에 대한 새 정보가 없으면 '없음'이라고만 써.\n\n" +
                                    $"플레이어 발화: {userInput}\nNPC 응답: {response}";
            string fact = await _llmAgent.Chat(extractPrompt, null, null, addToHistory: false).AsUniTask().AttachExternalCancellation(ct);

            if (!string.IsNullOrEmpty(fact) && fact.Trim() != "없음")
            {
                Debug.Log($"NPCBrain fact : {fact.Trim()}");
                _memory?.AddKnowFact(fact.Trim());
            }

            //응답을 받은 직후, reponse를 반환하기 전, 대화횟수 증가.
            _memory?.IncrementConversationCount();
            _memory?.Save(NpcNameKey);

            return response;
        }

        private void ResetMemory()
        {
            _memory?.Clear(NpcNameKey);
            _llmAgent?.ClearHistory(); // 대화 히스토리도 같이 초기화
        }
    }
}

//문제 발생 순서
//■ 버튼 클릭(Play Mode 종료)
//  │
//  ├─ LLMInitializer.CleanupNative() '먼저 실행'
//  │     → llamalib 프로세스 Kill
//  │     → _llm.Destroy()  ← LLM 메모리 해제
//  │          → native handle 이제 무효(댕글링 포인터)
//  │
//  └─ NPCBrain.OnDestroy() '나중에 실행'
//        → _llmAgent.CancelRequests()
//             → 내부적으로 LLM_Cancel() 호출(P/Invoke)
//             → "LLM아, 취소해줘!" → 근데 LLM 이미 삭제됨
//             → dangling pointer 접근
//             → 💥 에디터 강제종료

//   CleanupNative() → _llm.Destroy() → native handle 해제
//        ↓ (조금 후)
//   NPCBrain.OnDestroy() → CancelRequests() → 해제된 handle 접근 → 💥


// ExitPlayMode 단계에서
// LLMInitializer가 LLM native handle(LLM이 내부적으로 들고 있는 C++ 메모리 주소)을 먼저 정리한 후
// NPCBrain의 OnDestroy가 호출되면 
// LLM_Cancel(P/Invoke : C#이 C++ 네이티브 함수를 직접 호출하는 방법)이
// dangling pointer(이미 해제된 메모리를 가리키는 포인터)를 건드려
// 에디터가 access violation(존재하지 않는 메모리에 접근했을 때 OS가 강제종료 시키는 것)으로 강제종료된다.
// ExitingPlayMode → set true → OnDestroy에서 CancelRequests skip 으로 차단.

//전체 흐름 요약
//■ 버튼 클릭
//  │
//  ├─ s_isExitingPlayMode = true (RegisterPlayModeHook)
//  │
//  ├─ LLMInitializer.CleanupNative()
//  │     → LLM 메모리 해제 (native handle 무효화)
//  │
//  └─ NPCBrain.OnDestroy()
//        → s_isExitingPlayMode가 true
//        → CancelRequests() 건너뜀  ✅ 에디터 생존