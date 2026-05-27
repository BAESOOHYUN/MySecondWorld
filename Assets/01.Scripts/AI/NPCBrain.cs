using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using MessagePipe;
using VContainer;
using MySecondWorld.Network;
using System.Linq;

namespace MySecondWorld.AI
{
    /// <summary>
    /// ILLMService를 통해 NPC 응답 생성, 팩트 추출, 메모리 관리를 담당.
    /// </summary>
    public class NPCBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCSystemPromptSO _systemPrompt;
        [SerializeField] private NPCMemorySO _memory;
        [SerializeField] private NPCServerState _serverState; // 선택적, 없으면 null 허용

        [ContextMenu("메모리 초기화")]
        private void DebugClearMemory() => ResetMemory();

        private string NpcNameKey =>
            (_systemPrompt != null && !string.IsNullOrEmpty(_systemPrompt.NpcGuid))
                ? _systemPrompt.NpcGuid : name;

        // 대화 세션 버퍼 — 창 닫힐 때 한 번에 팩트 추출
        private readonly List<(string user, string npc)> _sessionBuffer = new();

        // 외부 읽기 전용 접근 (NPCInteractable에서 npcName 접근용)
        public NPCSystemPromptSO SystemPrompt => _systemPrompt;

        // _responsePub와 _thinkingPub는 람다 안에서 쓰인다.
        // 람다는 Construct()가 끝난 훨씬 나중에 , 플레이어가 메시지를 보낼 때마다 실행된다.
        // 그 시점에도 살아있어야 하므로 필드에 저장
        private IPublisher<NPCResponseReceivedEvent> _responsePub;
        private IPublisher<NPCThinkingStartedEvent> _thinkingPub;
        private IPublisher<NPCDialogueEvent> _dialoguePub;
        private IPublisher<NPCTTSSentenceEvent> _ttsPub;
        private ILLMService _llmService;

        // [Inject]가 붙은 메서드를 VContainer(NPCLifetimeScope)가 찾아서
        // 파라미터 타입에 맞는 인스턴스를 컨테이너에서 꺼내 자동으로 호출한다.
        // 파라미터에 뭘 넣는가의 기준: 이 클래스가 동작하는 데 외부에서 받아야 하는 것들
        // 호출 순서 Awake() -> VContainer가 Construct() 호출 ←[Inject]주입 -> Start() 호출
        // Start()에서 주입된 필드를 안전하게 쓸 수 있다.
        [Inject]
        private void Construct(
            ISubscriber<string, PlayerMessageSentEvent> messageSub,
            IPublisher<NPCResponseReceivedEvent> responsePub,
            IPublisher<NPCThinkingStartedEvent> thinkingPub,
            IPublisher<NPCDialogueEvent> dialoguePub,
            IPublisher<NPCTTSSentenceEvent> ttsPub,
            ILLMService llmService
            )
        {
            _responsePub = responsePub;
            _thinkingPub = thinkingPub;
            _dialoguePub = dialoguePub;
            _ttsPub = ttsPub;
            _llmService = llmService;

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

                        _sessionBuffer.Add((evt.Text, response));
                    }
                    catch (OperationCanceledException)
                    {
                        // 취소는 정상 흐름 — 무시
                    }
                })
                .AddTo(destroyCancellationToken);
        }

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

            _llmService.SetSystemPrompt(BuildSystemPromptWithFacts());
        }

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

        // 응답 생성 — 팩트 추출 없이 응답만 반환해 대기 시간 최소화
        private async UniTask<string> GenerateResponseAsync(
            string userInput, CancellationToken ct = default)
        {
            if (!_llmService.IsReady)
            {
                Debug.LogWarning("[NPCBrain] LLM 서비스가 아직 준비되지 않았습니다.");
                return string.Empty;
            }

            float t0 = Time.realtimeSinceStartup;
            string lastToken = "";
            int lastSentEnd = 0;
            Action<string> onToken = token =>
            {
                lastToken = token; // 누적 전체 텍스트 갱신
                                   // 마지막 문자가 문장 끝이면 token[lastSentEnd..]만 TTS로 발행하고 커서 전진
                string lastchar = token[token.Length - 1].ToString();
                if (lastchar == "." ||
                    lastchar == "!" ||
                    lastchar == "?" ||
                    lastchar == "\n")
                {
                    string sentence = token[lastSentEnd..];
                    if (sentence.Any(char.IsLetterOrDigit))
                    {
                        // token[lastSentEnd..] : C# 범위 연산자, lastSentEnd부터 끝까지(새로 추가된 부분만) 슬라이스
                        _ttsPub.Publish(new NPCTTSSentenceEvent(NpcNameKey, token[lastSentEnd..]));
                        lastSentEnd = token.Length;
                    }
                }
            };
            // 콜백 1회차: "안녕하세요." → 문장 끝 감지
            //     token[0..] = "안녕하세요."  → TTS 발행
            //     lastSentEnd = 7
               
            // 콜백 2회차: "안녕하세요. 저는 의사입니다." → 문장 끝 감지
            //     token[7..] = " 저는 의사입니다."  → TTS 발행(앞 7글자는 이미 발행했으니 스킵)
            //     lastSentEnd = 20
               
            // 콜백 3회차: "안녕하세요. 저는 의사입니다. 무엇을" → 문장 끝 아님 → 발행 안 함
               
            // 콜백 4회차: "안녕하세요. 저는 의사입니다. 무엇을 도와드릴까요?" → 문장 끝 감지
            //     token[20..] = " 무엇을 도와드릴까요?"  → TTS 발행
            //     lastSentEnd = 34

            string response = await _llmService.ChatAsync(userInput, onToken, ct);

            // 마침표 없이 끝나는 경우 대비
            // ex)"안녕하세요. 저는 의사입니다. 무엇을 도와드릴까요"
            // "무엇을 도와드릴까요" 출력
            if (lastSentEnd < lastToken.Length)
            {
                string sentence = lastToken[lastSentEnd..];
                if (sentence.Any(char.IsLetterOrDigit))
                {
                    _ttsPub.Publish(new NPCTTSSentenceEvent(NpcNameKey, lastToken[lastSentEnd..]));
                }
            }

            Debug.Log($"[NPCBrain] 추론 시간: {Time.realtimeSinceStartup - t0:F2}s");

            if (this == null)
                return string.Empty;

            _memory?.IncrementConversationCount();
            _memory?.Save(NpcNameKey);

            return response;
        }

        // NPCInteractable.OnEnterIdle()에서 호출 — 대화창 닫힐 때 세션 전체 팩트 추출
        public void OnConversationEnded()
        {
            if (_sessionBuffer.Count == 0) return;

            var snapshot = new List<(string user, string npc)>(_sessionBuffer);
            _sessionBuffer.Clear();

            ExtractAndSaveFactAsync(snapshot, destroyCancellationToken).Forget();
        }

        private async UniTaskVoid ExtractAndSaveFactAsync(
            List<(string user, string npc)> transcript, CancellationToken ct)
        {
            if (!_llmService.IsReady || _memory == null) return;

            string playerName = string.IsNullOrWhiteSpace(MySecondWorld.Network.PlayerSession.LocalPlayerName)
                ? "플레이어"
                : MySecondWorld.Network.PlayerSession.LocalPlayerName;

            // 전체 대화록 조립
            using var transcriptSb = ZString.CreateStringBuilder();
            foreach (var (user, npc) in transcript)
            {
                transcriptSb.Append($"{playerName}: ");
                transcriptSb.AppendLine(user);
                transcriptSb.Append("NPC: ");
                transcriptSb.AppendLine(npc);
            }

            // 기존 사실 블록 — 중복 방지용
            string knownBlock = "";
            if (_memory.KnownFacts != null && _memory.KnownFacts.Count > 0)
            {
                using var sb = ZString.CreateStringBuilder();
                sb.AppendLine("이미 알려진 사실 (중복 금지):");
                foreach (var f in _memory.KnownFacts)
                {
                    sb.Append("- ");
                    sb.AppendLine(f);
                }
                knownBlock = sb.ToString();
            }

            string prompt =
                $"{knownBlock}" +
                $"아래는 {playerName}과 나눈 대화 전체입니다.\n" +
                $"{transcriptSb}\n" +
                $"이 대화에서 {playerName}에 대해 새롭게 알게 된 사실이 있으면 한 문장으로 써줘.\n" +
                $"예: '{playerName}은 기침 때문에 걱정하고 있다.'\n" +
                $"이미 알려진 사실과 같거나 비슷하면 '없음'이라고만 써.\n" +
                $"NPC 자신에 대한 내용은 절대 쓰지 마.";

            try
            {
                string fact = await _llmService.ExtractFactAsync(prompt, ct);

                if (this == null) return;

                string trimmed = fact?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed != "없음")
                {
                    Debug.Log($"[NPCBrain] 새 팩트: {trimmed}");
                    _memory.AddKnowFact(trimmed);
                    _memory.Save(NpcNameKey);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ResetMemory()
        {
            _memory?.Clear(NpcNameKey);
            _llmService?.ClearHistory(); // 대화 히스토리도 같이 초기화
        }
    }
}