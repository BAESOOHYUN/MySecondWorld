using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using VContainer;
using MessagePipe;
using MySecondWorld.AI;
using MySecondWorld.AI.Voice;
using MySecondWorld.CameraSystem;
using System.Threading;
using UnityEngine.EventSystems;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.UI
{
    /// <summary>
    /// NPC 대화창 UI.
    /// TTS 문장 단위 텍스트 동기화, 입력 필드, ESC/닫기 버튼 지원.
    /// </summary>
    public class NPCDialogueUI : MonoBehaviour, INPCDialogueUI, IItemRecommendUI
    {
        #region 인스펙터
        [Header("UI References")]
        [SerializeField] private GameObject _canvas;
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private Transform _chatContent;
        [SerializeField] private TMP_InputField _playerInputField;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Chat Bubble Prefabs")]
        [SerializeField] private GameObject _npcBubblePrefab;
        [SerializeField] private GameObject _playerBubblePrefab;
        [SerializeField] private GameObject _thinkingBubblePrefab;

        [Header("Future: Voice")]
        [SerializeField] private Button _sttButton;
        [SerializeField] private Button _ttsStopButton;

        [Header("Tabs & Sections")]
        [SerializeField] private Button _tabHistoryBtn;      // 왼쪽 탭: 대화 기록
        [SerializeField] private Button _tabMemoryBtn;       // 오른쪽 탭: 기억
        [SerializeField] private GameObject _chatSection;    // 채팅창 + 하단 입력 영역 전체
        [SerializeField] private GameObject _memorySection;  // 기억 정보 화면
        [Header("Memory Details")]
        [SerializeField] private TextMeshProUGUI _relationText;
        [SerializeField] private TextMeshProUGUI _firstMetText;
        [SerializeField] private TextMeshProUGUI _knownFactsText;

        [Header("추천 카드")]
        [SerializeField] private GameObject _recommendCard;      // 카드 루트 (프리팹에서 꺼 둔다)
        [SerializeField] private Image _recommendIcon;
        [SerializeField] private TextMeshProUGUI _recommendNameText;
        [SerializeField] private Button _recommendBuyButton;
        [SerializeField] private Button _recommendCloseButton;

        #endregion

        #region 필드 — 내부 상태
        private string _lastNpcGuid;
        private string _currentNpcGuid;
        private NPCMemorySO _currentMemory;
        private NPCSystemPromptSO _currentSystemPrompt;
        private ChatBubble _thinkingBubble;
        private CancellationTokenSource _thinkingAnimCts;
        private CancellationTokenSource _sttCts;
        // 이번 응답에서 TTS 음성 문장이 한 번이라도 시작됐는가. ShowThinking()에서 false, sentenceStartedSub에서 true.
        // 응답 도착 후 2초가 지나도 false이면 TTS 장애로 보고 텍스트를 즉시 출력한다(FallbackEnsureDialogueAsync).
        private bool _ttsSentenceStarted = false;

        // 2초 안전망(FallbackEnsureDialogueAsync)이 이미 전체 답을 말풍선으로 띄웠는가.
        // true인 동안은 뒤늦게 도착한 TTS 문장을 다시 말풍선으로 만들지 않는다 (같은 말이 두 번 나오지 않게)
        private bool _fallbackShown;

        // 이번 질문에 대한 LLM 응답 전체가 도착했는가. 제출 시 false, responseSub에서 true.
        // 응답이 오는 도중 발행된 TTS "말 끝남"으로 입력이 열리는 것을 막는 데 쓴다.
        private bool _llmResponseCompleted;

        private IShopUI _shopUI;                 // 구매는 ShopUI의 확인 팝업 흐름을 재사용
        private EquipmentItem _recommendItem;
        private uint _recommendShopNetId;
        #endregion

        #region DI
        private IPublisher<NPCStopTTSEvent> _stopTTSPub;
        private ISTTService _stt;

        private readonly Subject<Unit> _closeSubject = new();
        private CompositeDisposable _scopeSubs;   // 접속(스코프)마다 새로 거는 MessagePipe 구독 — 다음 Construct 때 통째로 해제
        private bool _uiWired;                    // 버튼·입력창·STT 연결은 객체당 한 번만

        // R3 구독
        public Observable<string> OnSubmit => _submitButton.OnClickAsObservable()
            .Select(_ => _playerInputField.text.Trim())
            .Where(t => t.Length > 0);
        public Observable<Unit> OnClose => _backdropButton != null
            ? Observable.Merge(_closeButton.OnClickAsObservable().AsUnitObservable(), _backdropButton.OnClickAsObservable().AsUnitObservable(), _closeSubject)
            : Observable.Merge(_closeButton.OnClickAsObservable().AsUnitObservable(), _closeSubject);

        private static int _dbgConstructCount;   // 임시 진단: Construct가 몇 번 불리는지 센다

        [Inject]
        private void Construct(
            ISubscriber<NPCResponseReceivedEvent> responseSub,
            ISubscriber<NPCThinkingStartedEvent> thinkingSub,
            ISubscriber<NPCSpeakFinishedEvent> speakFinishedSub,
            ISubscriber<NPCSentenceStartedEvent> sentenceStartedSub,
            IPublisher<NPCStopTTSEvent> stopTTSPub,
            ISTTService stt,
            IShopUI shopUI
            )
        {
            Debug.Log($"[NPCDialogueUI] Construct 호출(임시 진단) 누적 {++_dbgConstructCount}회, instance={GetInstanceID()}");

            // 재접속 때마다 새 NPCLifetimeScope가 같은 UI에 다시 주입한다 → 이전 접속의 구독을 먼저 끊는다.
            // (destroyCancellationToken에 묶으면 UI가 파괴되지 않아 접속 횟수만큼 구독이 쌓였다: 말풍선 N개 버그)
            _scopeSubs?.Dispose();
            _scopeSubs = new CompositeDisposable();

            // 스트리밍 중 문장 단위로 텍스트 누적 — TTS와 동시에 표시
            _scopeSubs.Add(sentenceStartedSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    _ttsSentenceStarted = true; // 문장 수신 확인!
                    Debug.Log($"[NPCDialogueUI] TTS 문장 시작(임시 진단) t={Time.realtimeSinceStartup:F2}: {evt.Text}");
                    if (!_fallbackShown)            // 안전망이 이미 띄웠다면 건너뜀 (음성은 그대로 재생된다)
                        AppendSentence(evt.Text);   // 음성 타이밍에 맞춰 문장 추가
                }));

            // NPCSpeakFinishedEvent = NPCVoicePlayer의 TTS 재생 큐가 비고 마지막 클립 재생이 끝났을 때(또는 정지 버튼으로 취소됐을 때) 발행된다.
            // LLM이 문장을 아직 보내는 중에 큐가 잠깐 비어도 발행되므로, 이 신호만으로는 "대답이 끝났다"고 볼 수 없다.
            // 응답 전체가 도착한 뒤(_llmResponseCompleted)에 온 신호일 때만 입력창을 다시 연다.
            _scopeSubs.Add(speakFinishedSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    if (!_llmResponseCompleted) return;   // 응답이 아직 오는 중의 "TTS 재생 큐 비었음"은 무시
                    SetInputEnabled(true);
                }));

            // responseSub: LLM 응답이 끝까지 도착했을 때 발행된다. 도착 즉시 _llmResponseCompleted를 true로 하고 화면에 대화를 표시한다.
            // TTS 장애 안전망: 도착 후 2초가 지나도 TTS 문장이 시작되지 않으면 텍스트를 즉시 출력하고 입력을 연다(FallbackEnsureDialogueAsync)
            _scopeSubs.Add(responseSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    _llmResponseCompleted = true;
                    if (_npcNameText != null) _npcNameText.text = evt.NpcName;

                    if (string.IsNullOrWhiteSpace(evt.Text))
                    {
                        // 실패/빈 응답 시
                        AppendSentence("죄송합니다, 잘 듣지 못했어요. 다시 말씀해 주시겠어요?");
                        SetInputEnabled(true);
                    }
                    else
                    {

                        // TTS 음성이 안 와서 문장이 한 번도 안 떴을 경우를 대비한 안전망 (2초 대기)
                        FallbackEnsureDialogueAsync(evt.Text, destroyCancellationToken).Forget();
                    }

                    UpdateMemoryView(_currentMemory, _currentSystemPrompt);
                }));

            _scopeSubs.Add(thinkingSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    ShowThinking();
                    SetInputEnabled(false);
                }));

            _stopTTSPub = stopTTSPub;
            _stt = stt;
            _shopUI = shopUI;

            // 버튼 연결은 객체당 한 번만 — 이 UI는 파괴되지 않고 재접속 때마다 Construct가 다시 불린다
            if (!_uiWired)
            {
                WireUIOnce();
                _uiWired = true;
            }
        }

        // 버튼·입력창·STT 트리거 연결. Construct가 여러 번 불려도 한 번만 실행된다(_uiWired).
        // 클릭 시점에 필드(_stt, _stopTTSPub)를 읽으므로 재접속 후 새로 주입된 서비스가 그대로 쓰인다.
        private void WireUIOnce()
        {
            _submitButton.OnClickAsObservable()
                .Select(_ => _playerInputField.text.Trim())
                .Where(t => t.Length > 0)
                .Subscribe(t =>
                            {
                                ShowPlayerBubble(t);
                                _llmResponseCompleted = false;  // 새 질문이므로 응답 대기 상태로
                                SetInputEnabled(false);         // 이벤트를 기다리지 않고 누르자마자 바로 잠근다
                            })
                .AddTo(destroyCancellationToken);

            if (_ttsStopButton != null)
                _ttsStopButton.OnClickAsObservable()
                        .Subscribe(_ => _stopTTSPub?.Publish(new NPCStopTTSEvent(_currentNpcGuid))) // NPCVoicePlayer가 구독
                        .AddTo(destroyCancellationToken);

            // Enter로 바로 전송
            _playerInputField.onSubmit.AddListener(_ =>
            {
                if (_submitButton.interactable) // 응답 생성 중(입력 비활성)에는 무시
                    _submitButton.onClick.Invoke();
            });

            if (_sttButton != null)
            {
                var trigger = _sttButton.GetComponent<EventTrigger>() ?? _sttButton.gameObject.AddComponent<EventTrigger>();
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => StartSTT());
                trigger.triggers.Add(down);
                var up = new EventTrigger.Entry {  eventID= EventTriggerType.PointerUp };
                up.callback.AddListener(_ => _stt?.StopListening());
                trigger.triggers.Add(up);
            }
        }

        #endregion

        #region 필수 참조 검증

        private void Awake()
        {
            bool valid = true;

            if (_canvas == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: dialoguePanel이 연결되지 않았습니다.");
                valid = false;
            }
            if (_chatContent == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: _chatContent가 연결되지 않았습니다.");
                valid = false;
            }
            if (_playerInputField == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: playerInputField가 연결되지 않았습니다.");
                valid = false;
            }
            if (_submitButton == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: submitButton이 연결되지 않았습니다.");
                valid = false;
            }
            if (_closeButton == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: closeButton이 연결되지 않았습니다.");
                valid = false;
            }

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Close);

            if (_tabHistoryBtn != null)
                _tabHistoryBtn.onClick.AddListener(() => SwitchTab(isMemoryTab: false));
            if (_tabMemoryBtn != null)
                _tabMemoryBtn.onClick.AddListener(() => SwitchTab(isMemoryTab: true));

            if (_recommendBuyButton != null && _recommendCloseButton != null)
            {
                _recommendBuyButton.onClick.AddListener(() =>
                {
                    if (_recommendItem != null) 
                        _shopUI?.RequestPurchase(_recommendItem, _recommendShopNetId);
                });
                _recommendCloseButton.onClick.AddListener(HideRecommend);
            }

            if (!valid)
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_currentMemory != null)
                _currentMemory.OnMemoryChanged -= OnMemoryStateChanged;
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
            if (_backdropButton != null)
                _backdropButton.onClick.RemoveListener(Close);
            _closeSubject?.Dispose();
            _scopeSubs?.Dispose();
        }

        private void Update()
        {
            if ((_canvas != null && _canvas.activeSelf || gameObject.activeSelf) && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }
        #endregion

        #region 열기 / 닫기
        public void Open(string npcGuid) => Open(npcGuid, null, null);

        public void Open(string npcGuid, NPCMemorySO memory = null, NPCSystemPromptSO systemPrompt = null)
        {
            HideRecommend();   // 이전 대화의 카드가 남지 않게

            if (_currentMemory != null)
                _currentMemory.OnMemoryChanged -= OnMemoryStateChanged;

            _currentNpcGuid = npcGuid;
            _currentMemory = memory;
            _currentSystemPrompt = systemPrompt;

            if (_currentMemory != null)
                _currentMemory.OnMemoryChanged += OnMemoryStateChanged;

            gameObject.SetActive(true);
            if (_canvas != null)
                _canvas.SetActive(true);
            _playerInputField.text = string.Empty;

            // 같은 npc → 히스토리 유지, 다른 NPC → 초기화 후 이전 대화 기록 복원
            if (npcGuid != _lastNpcGuid)
            {
                ClearChat();
                if (_npcNameText != null)
                    _npcNameText.text = systemPrompt != null ? systemPrompt.NpcName : string.Empty;
                _lastNpcGuid = npcGuid;

                // 새 NPC 대화 시작 시 과거 대화 기록을 채팅 버블로 복원
                RestoreHistoryToChat(memory);
            }
            else if (_npcNameText != null && systemPrompt != null)
            {
                _npcNameText.text = systemPrompt.NpcName;
            }

            UpdateMemoryView(memory, systemPrompt);
            SwitchTab(isMemoryTab: false); // 대화창 열릴 때 기본으로 [대화 기록] 탭 켜기
            _playerInputField.ActivateInputField();
        }

        public void Close()
        {
            HideRecommend();   // 이전 대화의 카드가 남지 않게

            if (_currentMemory != null)
                _currentMemory.OnMemoryChanged -= OnMemoryStateChanged;

            _closeSubject.OnNext(Unit.Default);
            DialogueCameraController.Instance?.ReleaseFocus();
            if (_canvas != null)
                _canvas.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnMemoryStateChanged() => UpdateMemoryView(_currentMemory, _currentSystemPrompt);

        #endregion

        #region 기억 및 히스토리 패널
       
        public void UpdateMemoryView(NPCMemorySO memory, NPCSystemPromptSO systemPrompt)
        {
            if (memory == null) return;

            // 1. 호감도
            if (_relationText != null)
            {
                string relDesc = memory.RelationScore switch
                {
                    > 50 => "매우 친밀",
                    > 20 => "호감",
                    > -20 => "보통",
                    > -50 => "경계",
                    _ => "적대적"
                };
                _relationText.text = $"호감도 : {(memory.RelationScore >= 0 ? "+" : "")}{memory.RelationScore:F0} ({relDesc})";
            }
            // 2. 첫 만남 위치 및 일시
            if (_firstMetText != null)
            {
                string date = string.IsNullOrEmpty(memory.FirstMetDate) ? "최근" : memory.FirstMetDate;
                string loc = string.IsNullOrEmpty(memory.FirstMetLocation) ? "마을 광장" : memory.FirstMetLocation;
                _firstMetText.text = $"첫 만남 : {loc} ({date}) · 누적 대화 {memory.ConversationCount}회";
            }
            // 3. 알고 있는 사실 (Known Facts)
            if (_knownFactsText != null)
            {
                if (memory.KnownFacts != null && memory.KnownFacts.Count > 0)
                {
                    // 불릿 기호로 깔끔하게 여러 사실 나열
                    _knownFactsText.text = "기억하고 있는 사실 :\n• " + string.Join("\n• ", memory.KnownFacts);
                }
                else
                {
                    _knownFactsText.text = "기억하고 있는 사실 :\n• 아직 기록된 특별한 사실이 없습니다.";
                }
            }
        }
        
        #endregion

        #region 입력 제어
        public void SetInputEnabled(bool enabled)
        {
            _playerInputField.interactable = enabled;
            _submitButton.interactable = enabled;
        }

        public void ClearInput()
        {
            _playerInputField.text = string.Empty;
        }
        #endregion

        #region 추천 카드 (IItemRecommendUI)

        // 이 NPC가 추천한 상품을 대화창 위에 카드로 띄운다. [구매]는 ShopUI의 확인 팝업 흐름을 재사용한다
        public void ShowRecommend(EquipmentItem item, uint shopNetId)
        {
            if (item == null || _recommendCard == null) return;

            _recommendItem = item;
            _recommendShopNetId = shopNetId;
            _recommendIcon.sprite = item.UiIcon;
            _recommendNameText.text = item.Title;
            _recommendCard.SetActive(true);
        }

        public void HideRecommend()
        {
            _recommendItem = null;
            if (_recommendCard != null)
                _recommendCard.SetActive(false);
        }

        #endregion

        #region 채팅 버블
        private void ShowPlayerBubble(string text)
        {
            if (_playerBubblePrefab == null || _chatContent == null)
                return;
            var go = Instantiate(_playerBubblePrefab, _chatContent);
            go.GetComponent<ChatBubble>().SetText(text);
            ScrollToBottom();
        }

        public void ShowThinking()
        {
            _ttsSentenceStarted = false; // 플래그 초기화
            _fallbackShown = false;
            if (_thinkingBubblePrefab == null || _chatContent == null) return;

            // 이전 생각중 버블 정리 (방어코드)
            // ex) 플레이어가 Submit을 빠르게 두 번 눌렀을 때
            // ex) 네트워크 재시도로 NPCThinkingStartedEvent가 두 번 왔을 때
            StopThinking();

            var go = Instantiate(_thinkingBubblePrefab, _chatContent);
            _thinkingBubble = go.GetComponent<ChatBubble>();
            _thinkingAnimCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            AnimateThinking(_thinkingBubble, _thinkingAnimCts.Token).Forget();
            ScrollToBottom();
        }

        private void StopThinking()
        {
            _thinkingAnimCts?.Cancel();
            _thinkingAnimCts?.Dispose();
            _thinkingAnimCts = null;

            if (_thinkingBubble != null)
            {
                Destroy(_thinkingBubble.gameObject);
                _thinkingBubble = null;
            }
        }

        private async UniTaskVoid AnimateThinking(ChatBubble bubble, CancellationToken ct)
        {
            string[] frames = { ".", "..", "..." };
            int i = 0;
            while (!ct.IsCancellationRequested)
            {
                if (bubble == null) return;
                bubble.SetText(frames[i % 3]);
                i++;
                await UniTask.Delay(400, cancellationToken: ct);
            }
        }

        // TTS 문장 도착 시 즉시 누적 표시 — TTS 재생과 동기화
        private void AppendSentence(string sentence)
        {
            StopThinking();
            if (_npcBubblePrefab == null || _chatContent == null) return;
            var go = Instantiate(_npcBubblePrefab, _chatContent);
            go.GetComponent<ChatBubble>().SetText(sentence);
            ScrollToBottom();
        }

        private void ClearChat()
        {
            StopThinking();
            foreach (Transform child in _chatContent)
                Destroy(child.gameObject);
        }

        /// <summary>
        /// NPC 기억(MemorySO)에 남아있는 이전 대화 목록을 메인 채팅 스크롤뷰에 시간순으로 복원합니다.
        /// </summary>
        private void RestoreHistoryToChat(NPCMemorySO memory)
        {
            if (memory == null || memory.DialogueHistory == null || memory.DialogueHistory.Count == 0) return;
            if (_chatContent == null) return;
            foreach (var entry in memory.DialogueHistory)
            {
                if (!string.IsNullOrEmpty(entry.userText) && _playerBubblePrefab != null)
                {
                    var userGo = Instantiate(_playerBubblePrefab, _chatContent);
                    userGo.GetComponent<ChatBubble>()?.SetText(entry.userText);
                }
                if (!string.IsNullOrEmpty(entry.npcText) && _npcBubblePrefab != null)
                {
                    var npcGo = Instantiate(_npcBubblePrefab, _chatContent);
                    npcGo.GetComponent<ChatBubble>()?.SetText(entry.npcText);
                }
            }

            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }

        // 안전망: TTS가 정상 작동하면 _ttsSentenceStarted가 true가 되어 아무것도 안 하고,
        // TTS가 죽어서 문장이 안 오면 2초 뒤 전체 답변을 띄워줌!
        private async UniTaskVoid FallbackEnsureDialogueAsync(string fullText, CancellationToken ct)
        {
            await UniTask.Delay(2000, cancellationToken: ct);
            Debug.Log($"[NPCDialogueUI] 폴백 검사(임시 진단): _ttsSentenceStarted={_ttsSentenceStarted} t={Time.realtimeSinceStartup:F2}");
            // 2초 동안 TTS 음성 문장이 단 한 번도 도착하지 않은 경우 (TTS 장애/무음)
            if (!_ttsSentenceStarted)
            {
                _fallbackShown = true;
                AppendSentence(fullText); // 전체 텍스트 즉시 출력 & '...' 버블 닫기
                SetInputEnabled(true);
            }
        }

        #endregion

        #region STT 헬퍼 메서드
        private void StartSTT()
        {
            if (_stt.IsListening) return;   // 이미 녹음 중이면 두 번째 호출은 무시 (PointerDown이 중복 전달돼도 안전)
            if (!_submitButton.interactable) return; // 응답 생성·재생 중에는 녹음 시작 안 함. 버튼을 회색으로 만들어 막음
            _sttCts?.Cancel(); // 작업 멈춤
            _sttCts?.Dispose(); // 리소스 해제
            // CreateLinkedTokenSource : 두 개의 CancellationToken을 하나로 묶는 것
            //  - _sttCts.Cancel() 사용자가 대화창 닫음
            //  - destroyCancellationToken GameObject가 파괴됨 (씬 전환 등)
            // 둘 중 하나라도 취소되면 _sttCts.Token(ListenAsync의 ct)이 취소
            _sttCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RecordAndFill(_sttCts.Token).Forget();
        }

        private async UniTaskVoid RecordAndFill(CancellationToken ct)
        {
            var text = await _stt.ListenAsync(ct);
            if (!string.IsNullOrEmpty(text) && !ct.IsCancellationRequested)
            {
                _playerInputField.text = text;
                // 제출 직전에 한 번 더 버튼 확인 (녹음 중에 응답이 시작된 경우 대비)
                if (!_submitButton.interactable) return;
                _submitButton.onClick.Invoke();
            }
            _sttCts?.Dispose();
            _sttCts = null;
        }

        /// <summary>
        /// 왼쪽 [대화 기록] 탭과 오른쪽 [기억] 탭을 전환합니다.
        /// </summary>
        public void SwitchTab(bool isMemoryTab)
        {
            // 화면 노출 제어
            if (_chatSection != null) _chatSection.SetActive(!isMemoryTab);
            if (_memorySection != null) _memorySection.SetActive(isMemoryTab);

            // 탭 버튼 비주얼 (피그마 스타일: 활성 탭 골드/선명한 텍스트, 비활성 탭 다크 톤)
            if (_tabHistoryBtn != null)
            {
                if (_tabHistoryBtn.image != null)
                    _tabHistoryBtn.image.color = !isMemoryTab ? Color.white : new Color(0.5f, 0.55f, 0.65f, 0.75f);
                var txt = _tabHistoryBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.color = !isMemoryTab ? new Color(1f, 0.92f, 0.55f, 1f) : new Color(0.65f, 0.72f, 0.80f, 0.8f);
            }

            if (_tabMemoryBtn != null)
            {
                if (_tabMemoryBtn.image != null)
                    _tabMemoryBtn.image.color = isMemoryTab ? Color.white : new Color(0.5f, 0.55f, 0.65f, 0.75f);
                var txt = _tabMemoryBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.color = isMemoryTab ? new Color(1f, 0.92f, 0.55f, 1f) : new Color(0.65f, 0.72f, 0.80f, 0.8f);
            }
        }
        #endregion
    }
}
