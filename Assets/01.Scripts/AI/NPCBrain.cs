using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using LLMUnity;
using MessagePipe;
using VContainer;

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

        [ContextMenu("메모리 초기화")]
        private void DebugClearMemory() => ResetMemory();

        // 외부 읽기 전용 접근 (NPCInteractable에서 npcName 접근용)
        public NPCSystemPromptSO SystemPrompt => _systemPrompt;

        // _responsePub와 _thinkingPub는 람다 안에서 쓰인다.
        // 람다는 Construct()가 끝난 훨씬 나중에 , 플레이어가 메시지를 보낼 때마다 실행된다.
        // 그 시점에도 살아있어야 하므로 필드에 저장
        private IPublisher<NPCResponseReceivedEvent> _responsePub;
        private IPublisher<NPCThinkingStartedEvent> _thinkingPub;

        // [Inject]가 붙은 메서드를 VContainer가 찾아서,
        // 파라미터 타입에 맞는 인스턴스를 컨테이너에서 꺼내 자동으로 호출한다.
        // 파라미터에 뭘 넣는가의 기준: 이 클래스가 동작하는 데 외부에서 받아야 하는 것들
        // 호출 순서 Awake() -> VContainer가 Construct() 호출 ←[Inject]주입 -> Start() 호출
        // Start()에서 주입된 필드를 안전하게 쓸 수 있다.
        [Inject]
        private void Constrct(
            ISubscriber<string, PlayerMessageSentEvent> messageSub,
            IPublisher<NPCResponseReceivedEvent> responsePub,
            IPublisher<NPCThinkingStartedEvent> thinkingPub
            )
        {
            _responsePub = responsePub;
            _thinkingPub = thinkingPub;

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

        private void Start()
        {
            _memory?.Load(NpcNameKey);

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
            _llmAgent.slot = Mathf.Abs(_systemPrompt.NpcGuid.GetHashCode());
            _llmAgent.systemPrompt = BuildSystemPromptWithFacts();
        }

        // 정리
        private void OnDestroy()
        {
            _memory?.Save(NpcNameKey);
        }

        // 시스템 프롬프트 빌드
        private string BuildSystemPromptWithFacts()
        {
            using var sb = ZString.CreateStringBuilder();

            if (_systemPrompt != null)
                sb.AppendLine(_systemPrompt.BuildSystemPrompt());

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

            ct.Register(() => _llmAgent.CancelRequests());
            string response = await _llmAgent.Chat(userInput).AsUniTask().AttachExternalCancellation(ct);

            if (this == null) 
                return string.Empty;

            string extractPrompt = $"다음 대화에서 플레이어에 대해 NPC가 기억해야 할 중요한 사실이 있다면 한 줄로만 써줘. " +
                                    $"없으면 '없음'이라고만 써. \n\n플레이어: {userInput}\nNPC: {response}";
            string fact = await _llmAgent.Chat(extractPrompt, null, null, addToHistory: false).AsUniTask().AttachExternalCancellation(ct);
            Debug.Log($"NPCBrain fact  1 : {fact.Trim()}");
            if (!string.IsNullOrEmpty(fact) && fact.Trim() != "없음")
            {
                Debug.Log($"NPCBrain fact 2 : {fact.Trim()}");
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
