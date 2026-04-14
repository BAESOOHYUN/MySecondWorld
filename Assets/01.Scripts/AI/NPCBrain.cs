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

        // 외부 읽기 전용 접근 (NPCInteractable에서 npcName 접근용)
        public NPCSystemPromptSO SystemPrompt => _systemPrompt;

        // MessagePipe 주입
        // VContainer가 [Inject]를 보고 Construct()를 자동으로 호출한다.
        // MonoBehaviour는 new로 생성할 수 없으므로, 생성자 주입 대신 메서드 주입을 쓴다.
        private IPublisher<NPCResponseReceivedEvent> _responsePub;
        private IPublisher<NPCThinkingStartedEvent> _thinkingPub;

        // [Inject]가 붙은 메서드를 VContainer가 찾아서,
        // 파라미터 타입에 맞는 인스턴스를 컨테이너에서 꺼내 자동으로 호출한다.
        // 파라미터에 뭘 넣는가의 기준: 이 클래스가 동작하는 데 외부에서 받아야 하는 것들
        // 호출 순서
        // Unity가 Awake() 호출
        // VContainer가 Construct() 호출   ← [Inject]
        // Unity가 Start() 호출
        // Awake보다 늦고 Start보다 이르다. 그래서 Start()에서 주입된 필드를 안전하게 쓸 수 있다.
        [Inject]
        private void Constrct(
            ISubscriber<PlayerMessageSentEvent> messageSub, // 플레이어 메시지를 받아야 한다. Construct 안에서 쓰고 끝
            IPublisher<NPCResponseReceivedEvent> responsePub, // 응답을 발행해야 한다
            IPublisher<NPCThinkingStartedEvent> thinkingPub // 생각 중임을 발행해야 한다
            )
        {
            // messageSub의 람다가 실행될 때 responsePub, thinkingPub를 참조할 수 있으려면 어딘가에 살아있어야 한다.
            // 그래서 필드에 저장한다.
            _responsePub = responsePub;
            _thinkingPub = thinkingPub;

            // destroyCancellationToken: MonoBehaviour가 Destroy될 때 자동으로 Cancel된다.
            // 구독도 함께 해제되므로 OnDestroy에서 따로 정리할 필요가 없다.

            // messageSub는 Construct() 안에서.Subscribe()를 호출하는 순간 구독이 연결된다.
            // 그 이후로 NPCBrain이 직접 쓸 일이 없다.필드에 저장할 이유가 없다.
            messageSub
                .Subscribe(async evt =>
                {
                    // NpcGuid가 일치하는 메시지만 처리한다.
                    // 복수 NPC가 같은 채널을 공유하므로 반드시 필터가 필요하다.
                    if (evt.NpcGuid != NpcNameKey)
                        return;

                    _thinkingPub.Publish(new NPCThinkingStartedEvent(NpcNameKey));
                    var response = await GenerateResponseAsync(evt.Text, destroyCancellationToken);
                    _responsePub.Publish(new NPCResponseReceivedEvent(
                            NpcNameKey,
                            _systemPrompt?.NpcName ?? name,
                            response));
                })
                .AddTo(destroyCancellationToken);
                    // destroyCancellationToken : Unity 6에서 MonoBehaviour에 내장된 토큰
                    // .AddTo(destroyCancellationToken)을 쓰면 Destroy 시 구독이 자동으로 정리된다
        }

        // NPC 저장 키 — GUID 기반. GUID 미설정 시 GameObject 이름으로 폴백.
        // 주의: 동명 NPC 복수 배치 시 파일 충돌 방지를 위해 반드시 npcGuid를 설정할 것.
        private string NpcNameKey =>
            (_systemPrompt != null && !string.IsNullOrEmpty(_systemPrompt.NpcGuid))
                ? _systemPrompt.NpcGuid
                : name;

        // LLMAgent가 준비됐는지 확인
        private bool _isReady => _llmAgent != null && _llmAgent.llmAgent != null;

        // 초기화
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
    }
}
