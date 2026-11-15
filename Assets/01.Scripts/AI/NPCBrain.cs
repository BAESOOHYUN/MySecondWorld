using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using UnityEngine;
using MessagePipe;
using VContainer;
using MySecondWorld.Network;
using MySecondWorld.Save;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Customization;

namespace MySecondWorld.AI
{
    /// <summary>
    /// ILLMService를 통해 NPC 응답 생성, 팩트 추출, 메모리 관리를 담당.
    /// </summary>
    public class NPCBrain : MonoBehaviour
    {
        #region 인스펙터 필드
        [Header("References")]
        [SerializeField] private NPCSystemPromptSO _systemPrompt;
        [SerializeField] private NPCMemorySO _memory;
        [SerializeField] private MonoBehaviour _serverStateRaw; // 선택적, 없으면 null 허용
        #endregion

        #region 식별자 및 프로퍼티

        // 클론/동적 스폰에서도 겹치지 않는 고유 식별자
        private string _cachedUniqueId;
        public string UniqueId
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedUniqueId))
                {
                    if (_systemPrompt != null && !string.IsNullOrEmpty(_systemPrompt.NpcGuid))
                        _cachedUniqueId = _systemPrompt.NpcGuid;
                    else
                        _cachedUniqueId = $"{name}_{GetInstanceID()}";
                }
                return _cachedUniqueId;
            }
        }

        // 외부 읽기 전용 접근 (NPCInteractable에서 npcName 및 메모리 접근용)
        public NPCSystemPromptSO SystemPrompt => _systemPrompt;
        public NPCMemorySO Memory => _memory;

        // 인스펙터에 연결된 _serverStateRaw를 인터페이스로 해석한 값 (없으면 null)
        private INPCServerState _serverState => _serverStateRaw as INPCServerState;
        #endregion


        #region 상태 필드
        // 추천 가능한 상품 목록 (이 NPC 카탈로그 중 플레이어 성별에 맞는 것). 번호 N은 이 목록의 N번째(1부터)
        // 프롬프트를 만들 때 채우고, 응답에서 추천 번호를 해석할 때 읽는다
        private readonly List<EquipmentItem> _recommendable = new();

        #endregion

        #region 에디터 디버그 툴
        [ContextMenu("메모리 초기화")]
        private void DebugClearMemory() => ResetMemory();
        #endregion

        #region DI
        // _responsePub와 _thinkingPub는 람다 안에서 쓰인다.
        // 람다는 Construct()가 끝난 훨씬 나중에 , 플레이어가 메시지를 보낼 때마다 실행된다.
        // 그 시점에도 살아있어야 하므로 필드에 저장
        private IPublisher<NPCResponseReceivedEvent> _responsePub;
        private IPublisher<NPCThinkingStartedEvent> _thinkingPub;
        private IPublisher<NPCDialogueEvent> _dialoguePub;
        private IPublisher<NPCTTSSentenceEvent> _ttsPub;
        private ILLMService _llmService;
        private IItemRecommendUI _recommendUI;
        private NPCCommonRulesSO _rules;
        private NPCInteractable _interactable;
        private CharacterAppearanceService _appearance;

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
            ILLMService llmService,
            IItemRecommendUI recommendUI,
            NPCCommonRulesSO rules,
            NPCInteractable interactable,
            CharacterAppearanceService appearanceService = null
            )
        {
#if !UNITY_SERVER
            _responsePub = responsePub;
            _thinkingPub = thinkingPub;
            _dialoguePub = dialoguePub;
            _ttsPub = ttsPub;
            _llmService = llmService;
            _recommendUI = recommendUI;
            _rules = rules;
            _interactable = interactable;
            _appearance = appearanceService;


            // 라우팅 키는 SessionKey(인스턴스 단위) — UniqueId(캐릭터 정체성)를 그대로 쓰면
            // 같은 NpcGuid를 공유하는 다른 인스턴스(예: 컴패니언)와 채널이 겹친다.
            string myKey = _interactable != null ? _interactable.SessionKey : UniqueId;
            Debug.Log($"[NPCBrain] '{name}' 대화 등록 완료! 구독 키: '{myKey}', LLM: {llmService?.GetType().Name ?? "null"}");
            // PlayerMessageSentEvent가 오면 이 함수를 불러줘 하고 등록
            messageSub
               .Subscribe(myKey, evt =>
               {
                   if (_interactable != null && !_interactable.IsConversationActive)
                       return; // 나랑 대화 중인 게 아니면 무시 (컴패니언이 몰래 엿듣지 않게)
                   Debug.Log($"[NPCBrain] '{name}' 메시지 수신 성공! 텍스트: '{evt.Text}'");
                   ProcessMessageAsync(evt, myKey).Forget();
               })
               .AddTo(destroyCancellationToken);
#endif
        }

        #endregion


        #region 유니티 생명주기
        private void Awake()
        {

#if UNITY_SERVER
            Debug.LogWarning($"[NPCBrain] {name}: ⚠️ UNITY_SERVER 정의됨 → enabled=false 처리됨!");
            enabled = false;
#else
            Debug.Log($"[NPCBrain] {name}: UNITY_SERVER 없음 — 정상 활성화 상태");
            if (TryGetComponent(out MySecondWorld.Combat.Companion.CompanionServerState companionState))
                companionState.OnPresenceChanged += active => enabled = active;
#endif
        }

        private void Start()
        {
            NpcMemoryClient.OnLoaded += HandleMemoryLoaded;
            _memory?.ResetAll();

            // 서버 상태 => 로컬 메모리 동기화
            if (_serverState != null && _memory != null)
                _memory.SyncFactionFromServer(_serverState.FactionRelations);

            _llmService.SetSystemPrompt(BuildSystemPromptWithFacts(), _systemPrompt?.MaxResponseTokens ?? 40);
        }

        private void OnDestroy()
        {
            NpcMemoryClient.OnLoaded -= HandleMemoryLoaded;
        }

        #endregion

        #region 프롬프트 빌드 & LLM 응답 생성

        // PlayerMessageSentEvent 메세지가 올 때마다 "메시지 왔다!" 하고 알림을 쏨
        private async UniTaskVoid ProcessMessageAsync(PlayerMessageSentEvent evt, string key)
        {
            // 클라 프로세스 내에서 일어나는 일
            try
            {
                // 새 질문을 하면 이전 카드를 치운다
                _recommendUI?.HideRecommend();
                _thinkingPub.Publish(new NPCThinkingStartedEvent(key)); // UI에서 ...(생각신호) 말풍선을 띄우는 용도
        
                var (response, recommended) = await GenerateResponseAsync(evt.Text, key, evt.CT);
                Debug.Log($"[NPCBrain] '{name}' LLM 응답 완료: {response}");

                // 1. 대화 히스토리 즉시 반영 및 영구 저장
                _memory?.AddHistoryEntry(evt.Text, response);
                NpcMemoryClient.Append(UniqueId, evt.Text, response); // NpcMemoryClient.Append 서버에 대화 한 쌍을 올리라고 요청
                UploadSummary();   // 이번 응답으로 늘어난 conversationCount 등을 올린다

                // 2. 응답 이벤트 발행 (UI 업데이트)
                _responsePub.Publish(new NPCResponseReceivedEvent(
                        key, _systemPrompt?.NpcName ?? name, response)); // NPC 고유키, 이름, 응답 텍스트 (내 화면에 대사 표시, 대화 상태 처리)
                _dialoguePub.Publish(new NPCDialogueEvent { NpcName = key, Message = response }); // NPC 이름(키), 메시지 (TODO : 다른 플레이어에게 대사 공유 (파티 전투용))

                // 추천 아이템이 있는 경우
                if (recommended != null && _interactable != null)
                    _recommendUI?.ShowRecommend(recommended, _interactable.ShopNetId);

                // 3. 대화창을 닫기 전에도 새 팩트가 실시간으로 쌓이도록 즉시 백그라운드 팩트 추출 구동
                ExtractAndSaveFactAsync(new List<(string user, string npc)> { (evt.Text, response) }, destroyCancellationToken).Forget();
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[NPCBrain] '{name}' 대화 생성이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NPCBrain] '{name}' 대화 생성 중 예외 발생: {ex}");
            }
        }

        // 판매 목록 섹션. LLM이 없는 상품을 지어내지 못하게 실제 목록을 번호와 함께 준다
        private string BuildShopSection()
        {
            _recommendable.Clear();
            var catalog = _interactable != null ? _interactable.ShopCatalog : null;
            if (catalog == null) return "";

            foreach (var item in catalog)
                if (item != null && (_appearance == null || _appearance.IsItemMatchingGender(item)))
                    _recommendable.Add(item);
            if (_recommendable.Count == 0) return "";

            using var sb = ZString.CreateStringBuilder();
            var parts = new List<string>();   // 이 상점이 파는 부위 (중복 없이, 목록 순서대로)
            sb.AppendLine("당신이 파는 물건 목록 (이 목록에 있는 것만 추천할 수 있다):");
            for (int i = 0; i < _recommendable.Count; i++)
            {
                var e = _recommendable[i];
                string part = e.Restriction != null ? e.Restriction.Title : e.Category.ToString();
                if (!parts.Contains(part)) parts.Add(part);
                sb.Append(i + 1); sb.Append(". "); sb.Append(e.Title);
                sb.Append(" / 부위: "); sb.Append(part);
                foreach (var s in e.Stats) { sb.Append(" / "); sb.Append(s.Type.ToString()); sb.Append(' '); sb.Append(s.Value); }
                sb.AppendLine();
            }
            sb.Append("이 상점이 파는 부위: "); sb.AppendLine(string.Join(", ", parts));
            sb.AppendLine(_rules.ShopRules);
            return sb.ToString();
        }

        // 시스템 프롬프트 빌드
        // 프롬프트는 Start()와 HandleMemoryLoaded(메뉴를 열 때)에서 다시 만들어져서,
        // 성별을 바꿔도 대화를 시작하기 전에 목록이 갱신된다.
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
            string shop = BuildShopSection();
            if (shop.Length > 0) { sb.AppendLine(); sb.Append(shop); }
            sb.AppendLine();
            sb.AppendLine(_rules.LanguageRule);
            return sb.ToString().Trim();
        }

        // LLM에 넘길 이전 대화 쌍 수. 늘리면 이어짐은 좋아지지만 입력 토큰·429 위험이 커진다
        private const int LlmHistoryPairs = 6;

        // _memory.DialogueHistory(오래된 것이 앞)의 최근 N쌍을 (user, npc) 튜플로 복사한다
        // 튜플로 바꾸는 이유: AI 인터페이스가 DialogueHistoryEntry 타입에 의존하지 않게 하려는 것
        private List<(string user, string npc)> BuildLlmHistory()
        {
            var list = new List<(string user, string npc)>();
            if (_memory == null) return list;

            var h = _memory.DialogueHistory;
            // 저장된 쌍 중 가장 최근 LlmHistoryPairs개만 쓴다 (시작 인덱스 = Count - N)
            // '첫 대화 직후'처럼 쌍이 N개보다 적으면 Count - N이 음수가 되어 h[-1]로 예외가 나므로,
            // Math.Max로 0 아래로 내려가지 않게 한다 (이때는 있는 쌍 전부를 쓴다)
            for (int i = Math.Max(0, h.Count - LlmHistoryPairs); i < h.Count; i++)
                list.Add((h[i].userText, h[i].npcText));
            return list;
        }


        // 추천 태그 [[추천:번호]]. 번호는 판매 목록의 1부터 시작하는 번호
        private static readonly Regex RecommendTag = new(@"\[\[추천:(\d+)\]\]");
        // max_tokens에 잘려 닫히지 않은 태그("[[추천:3" 등)가 본문에 남지 않게 하는 용도
        private static readonly Regex BrokenTag = new(@"\[\[[^\]]*$");


        // LLM 원문: "...추천해요! [[추천:3]]"
        //│
        //├ ExtractRecommend(원문)
        //│    ├ 태그 있고 번호 정상 → pick = 2           (끝, FindMentionedItem은 안 부름)
        //│    └ 태그 없거나 번호 이상 → pick = -1, clean = 태그 뺀 본문
        //│
        //└ pick == -1 일 때만 → FindMentionedItem(clean)
        //      본문에 "사자문장 전사의 상의"가 있으면 그 상품, 없으면 -1 (카드 없음)
        //

        // 응답에서 태그를 빼 본문(clean)을 만들고, 추천 번호를 목록 인덱스(0부터)로 돌려준다
        // 태그가 없거나 번호가 1..count 밖이면 -1 → 카드는 안 띄우고 대화만 정상 진행
        private static int ExtractRecommend(string text, int count, out string clean)
        { 
            var m = RecommendTag.Match(text);
            clean = BrokenTag.Replace(RecommendTag.Replace(text, ""), "").Trim();
            if (!m.Success) return -1;
            if (!int.TryParse(m.Groups[1].Value, out int n)) return -1; // 너무 큰 수 방어
            return n >= 1 && n <= count ? n - 1 : -1;
        }

        // 태그가 없어도 응답에 판매 목록의 상품 이름이 들어 있으면 그 상품을 추천한 것으로 본다.
        // 여러 개가 나오면 응답에서 가장 먼저 나온 것, 같은 위치면 이름이 더 긴 것(짧은 이름이 긴 이름에 먹히지 않게)
        private static int FindMentionedItem(string text, List<EquipmentItem> list)
        {
            int best = -1, bestIdx = int.MaxValue, bestLen = 0;
            for (int i = 0; i < list.Count; i++)
            {
                string t = list[i].Title;
                if (string.IsNullOrEmpty(t)) continue;
                int idx = text.IndexOf(t, StringComparison.Ordinal);
                if (idx < 0) continue;
                if (idx < bestIdx || (idx == bestIdx && t.Length > bestLen))
                {
                    best = i;
                    bestIdx = idx;
                    bestLen = t.Length;
                }
            }
            return best;
        }

        // 응답 생성 — 팩트 추출 없이 응답만 반환해 대기 시간 최소화
        // sessionKey: TTS 이벤트 라우팅용(SessionKey). 영구 저장은 이 메서드 내에서도 항상 UniqueId를 쓴다.
        private async UniTask<(string text, EquipmentItem recommended)> GenerateResponseAsync(
            string userInput, string sessionKey, CancellationToken ct = default)
        {
            if (_llmService == null || !_llmService.IsReady)
            {
                Debug.LogWarning("[NPCBrain] LLM 서비스가 아직 준비되지 않았습니다.");
                return (string.Empty, null);
            }

            float t0 = Time.realtimeSinceStartup;
            string lastToken = "";
            int lastSentEnd = 0;
            Action<string> onToken = token =>
            {
                // 추천 태그 "[[추천:n]]"가 TTS로 읽히지 않게, 태그가 시작되는 곳부터 잘라 낸다
                // 로컬 LLM은 글자가 조금씩 오므로 "[["가 완성되기 전의 "[" 한 글자는 걸러지지 않지만 문장 끝 검사에 걸리지 않아 읽히지 않는다
                int tagStart = token.IndexOf("[[", StringComparison.Ordinal);
                if (tagStart >= 0) token = token[..tagStart];
                if (token.Length == 0) return;

                lastToken = token; // 누적 전체 텍스트 갱신
                                   // 마지막 문자가 문장 끝이면 token[lastSentEnd..]만 TTS로 발행하고 커서 전진
                string lastchar = token[token.Length - 1].ToString();
                if (lastchar == "." || lastchar == "!" || lastchar == "?" || lastchar == "\n")
                {
                    string sentence = token[lastSentEnd..];
                    if (sentence.Any(char.IsLetterOrDigit))
                    {
                        // token[lastSentEnd..] : C# 범위 연산자, lastSentEnd부터 끝까지(새로 추가된 부분만) 슬라이스
                        _ttsPub.Publish(new NPCTTSSentenceEvent(sessionKey, token[lastSentEnd..]));
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

            var listAtRequest = new List<EquipmentItem>(_recommendable);   // 이 요청의 프롬프트가 쓴 목록을 고정해 둔다
            string response = await _llmService.ChatAsync(userInput, BuildLlmHistory(), onToken, ct);

            // 마침표 없이 끝나는 경우 대비
            // ex)"안녕하세요. 저는 의사입니다. 무엇을 도와드릴까요"
            // "무엇을 도와드릴까요" 출력
            if (lastSentEnd < lastToken.Length)
            {
                string sentence = lastToken[lastSentEnd..];
                if (sentence.Any(char.IsLetterOrDigit))
                {
                    _ttsPub.Publish(new NPCTTSSentenceEvent(sessionKey, lastToken[lastSentEnd..]));
                }
            }

            Debug.Log($"[{_llmService.GetType().Name}] 추론 시간: {Time.realtimeSinceStartup - t0:F2}s");

            if (this == null)
                return (string.Empty, null);

            _memory?.IncrementConversationCount();

            // 태그 있고 번호 정상 → pick = 2 
            // 태그 없거나 번호 이상 → pick = -1, clean = 태그 뺀 본문
            int pick = ExtractRecommend(response ?? "", listAtRequest.Count, out string clean);
            // pick == -1 일 때(태그가 없으면) → FindMentionedItem로 분기하여, 본문에서 '아이템 이름'으로 추천템을 찾는다.
            if (pick < 0) 
                pick = FindMentionedItem(clean, listAtRequest);
            // 유저의 "상점 관심" 키워드
            if (!_rules.HasShopIntent(userInput)) pick = -1;

            return (clean, pick >= 0 ? listAtRequest[pick] : null);   // 번호 → 아이템으로 바꿔서 돌려줌
        }
        #endregion

        #region 팩트 추출 및 메모리 관리

        // 대화 메뉴를 열 때 호출 — 서버에서 이 NPC와의 대화 기록을 받아 온다
        public void PrefetchMemory() => NpcMemoryClient.Request(UniqueId);

        private void HandleMemoryLoaded(NpcMemorySave m)
        {
            if (m.npcKey != UniqueId || _memory == null) return;

            // 호감도는 서버만 바꾸는 값이라 로컬에 미확인 변경이 있을 수 없다 — 아래 HasPending 검사보다 먼저 항상 반영한다
            // (값이 프롬프트에 들어가는 건 이 메서드 끝의 프롬프트 재생성 때. 아직 호감도를 바꾸는 서버 로직이 없어 지금은 항상 0)
            _memory.SyncFactionFromServer(m.factionRelation);

            // 서버가 아직 못 받은 로컬 변경이 있으면 로컬이 더 새것이다 — 요약·대화 기록은 덮어쓰지 않는다
            if (NpcMemoryClient.HasPending(m.npcKey)) return;
            _memory.ApplySummary(m.relationScore, m.questStage, m.questGiven,
                                 m.firstMetDate, m.firstMetLocation, m.conversationCount, m.knownFacts);

            var entries = new List<DialogueHistoryEntry>(m.dialogue?.Length ?? 0);
            if (m.dialogue != null)
            {
                foreach (var p in m.dialogue)
                    entries.Add(new DialogueHistoryEntry(p.userText, p.npcText, p.timestamp));
            }
            // 서버가 준 대화 기록으로 통째로 교체 
            _memory.ReplaceHistory(entries);

            // 첫 만남 기록 (서버에 기억이 없던 NPC)
            if (string.IsNullOrEmpty(_memory.FirstMetDate))
            {
                string location = SystemPrompt != null ? _systemPrompt.NpcLocation : "알 수 없음";
                _memory.SetFirstMet(System.DateTime.Now.ToString("yyyy-MM-dd"), location);
                UploadSummary();
            }

            // 팩트가 프롬프트에 들어가므로 기억이 도착하면 다시 만든다
            _llmService?.SetSystemPrompt(BuildSystemPromptWithFacts(), _systemPrompt?.MaxResponseTokens ?? 40);
        }

        // 요약 필드를 서버로 올린다 (대화 기록은 Append로 따로 올라감)
        private void UploadSummary()
        {
            if (_memory == null) return;

            NpcMemoryClient.SendSummary(new NpcMemorySave
            {
                npcKey = UniqueId,
                relationScore = _memory.RelationScore,
                questStage = _memory.QuestStage,
                questGiven = _memory.QuestGiven,
                firstMetDate = _memory.FirstMetDate,
                firstMetLocation = _memory.FirstMetLocation,
                conversationCount = _memory.ConversationCount,
                knownFacts = _memory.KnownFacts.ToArray()
            });
        }

        private async UniTaskVoid ExtractAndSaveFactAsync(
            List<(string user, string npc)> transcript, CancellationToken ct)
        {
            if(_llmService == null || !_llmService.IsReady || _memory == null) return;

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
                $"아래는 {playerName}과 나눈 대화 전체다.\n" +
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
                    UploadSummary();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ResetMemory()
        {
            _memory?.ResetAll();
            _llmService?.ClearHistory(); // 대화 히스토리도 같이 초기화
        }
        #endregion
    }
}