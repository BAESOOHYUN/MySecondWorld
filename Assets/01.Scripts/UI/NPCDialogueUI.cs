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

namespace MySecondWorld.UI
{
    /// <summary>
    /// NPC 대화창 UI.
    /// TTS 문장 단위 텍스트 동기화, 입력 필드, ESC/닫기 버튼 지원.
    /// </summary>
    public class NPCDialogueUI : MonoBehaviour, INPCDialogueUI
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

        [Header("Left Panel - Memory & History")]
        [SerializeField] private GameObject _leftPanel;
        [SerializeField] private Button _tabMemoryBtn;
        [SerializeField] private Button _tabHistoryBtn;
        [SerializeField] private GameObject _memoryView;
        [SerializeField] private GameObject _historyView;

        [Header("Memory Elements")]
        [SerializeField] private Slider _relationSlider;
        [SerializeField] private TextMeshProUGUI _relationText;
        [SerializeField] private TextMeshProUGUI _firstMetText;
        [SerializeField] private TextMeshProUGUI _conversationCountText;
        [SerializeField] private Transform _knownFactsContent;
        [SerializeField] private GameObject _knownFactItemPrefab;

        [Header("History Elements")]
        [SerializeField] private Transform _historyContent;
        [SerializeField] private GameObject _historyItemPrefab;
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

        // 이번 질문에 대한 LLM 응답 전체가 도착했는가. 제출 시 false, responseSub에서 true.
        // 응답이 오는 도중 발행된 TTS "말 끝남"으로 입력이 열리는 것을 막는 데 쓴다.
        private bool _llmResponseCompleted;
        private string _lastPlayerText; // Stores last player input for memory logging
        #endregion

        #region DI
        private IPublisher<NPCStopTTSEvent> _stopTTSPub;
        private ISTTService _stt;

        private readonly Subject<Unit> _closeSubject = new();

        // R3 구독
        public Observable<string> OnSubmit => _submitButton.OnClickAsObservable()
            .Select(_ => _playerInputField.text.Trim())
            .Where(t => t.Length > 0);
        public Observable<Unit> OnClose => _backdropButton != null
            ? Observable.Merge(_closeButton.OnClickAsObservable().AsUnitObservable(), _backdropButton.OnClickAsObservable().AsUnitObservable(), _closeSubject)
            : Observable.Merge(_closeButton.OnClickAsObservable().AsUnitObservable(), _closeSubject);

        [Inject]
        private void Construct(
            ISubscriber<NPCResponseReceivedEvent> responseSub,
            ISubscriber<NPCThinkingStartedEvent> thinkingSub,
            ISubscriber<NPCSpeakFinishedEvent> speakFinishedSub,
            ISubscriber<NPCSentenceStartedEvent> sentenceStartedSub,
            IPublisher<NPCStopTTSEvent> stopTTSPub,
            ISTTService stt
            )
        {
            // 스트리밍 중 문장 단위로 텍스트 누적 — TTS와 동시에 표시
            sentenceStartedSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    _ttsSentenceStarted = true; // 문장 수신 확인!
                    AppendSentence(evt.Text);    // 음성 타이밍에 맞춰 문장 추가
                })
                .AddTo(destroyCancellationToken);

            // NPCSpeakFinishedEvent = NPCVoicePlayer의 TTS 재생 큐가 비고 마지막 클립 재생이 끝났을 때(또는 정지 버튼으로 취소됐을 때) 발행된다.
            // LLM이 문장을 아직 보내는 중에 큐가 잠깐 비어도 발행되므로, 이 신호만으로는 "대답이 끝났다"고 볼 수 없다.
            // 응답 전체가 도착한 뒤(_llmResponseCompleted)에 온 신호일 때만 입력창을 다시 연다.
            speakFinishedSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    if (!_llmResponseCompleted) return;   // 응답이 아직 오는 중의 "TTS 재생 큐 비었음"은 무시
                    SetInputEnabled(true);
                })
                .AddTo(destroyCancellationToken);

            // responseSub: LLM 응답이 끝까지 도착했을 때 발행된다. 도착 즉시 _llmResponseCompleted를 true로 하고 화면에 대화를 표시한다.
            // TTS 장애 안전망: 도착 후 2초가 지나도 TTS 문장이 시작되지 않으면 텍스트를 즉시 출력하고 입력을 연다(FallbackEnsureDialogueAsync)
            responseSub
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
                    UpdateHistoryView(_currentMemory);
                })
                .AddTo(destroyCancellationToken);

            thinkingSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid) return;
                    ShowThinking();
                    SetInputEnabled(false);
                })
                .AddTo(destroyCancellationToken);

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

            _stopTTSPub = stopTTSPub;
            if (_ttsStopButton != null)
                _ttsStopButton.OnClickAsObservable()
                        .Subscribe(_ => _stopTTSPub.Publish(new NPCStopTTSEvent(_currentNpcGuid))) // NPCVoicePlayer가 구독
                        .AddTo(destroyCancellationToken);

            // Enter로 바로 전송
            _playerInputField.onSubmit.AddListener(_ =>
            {
                if (_submitButton.interactable) // 응답 생성 중(입력 비활성)에는 무시
                    _submitButton.onClick.Invoke();
            });

            _stt = stt;
            if (_sttButton != null)
            {
                var trigger = _sttButton.GetComponent<EventTrigger>() ?? _sttButton.gameObject.AddComponent<EventTrigger>();
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => StartSTT());
                trigger.triggers.Add(down);
                var up = new EventTrigger.Entry {  eventID= EventTriggerType.PointerUp };
                up.callback.AddListener(_ => stt.StopListening());
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

            if (_tabMemoryBtn != null)
                _tabMemoryBtn.onClick.AddListener(() => ShowTab(true));
            if (_tabHistoryBtn != null)
                _tabHistoryBtn.onClick.AddListener(() => ShowTab(false));

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

            // 같은 npc → 히스토리 유지, 다른 NPC → 초기화
            if (npcGuid != _lastNpcGuid)
            {
                ClearChat();
                if (_npcNameText != null)
                    _npcNameText.text = systemPrompt != null ? systemPrompt.NpcName : string.Empty;
                _lastNpcGuid = npcGuid;
            }
            else if (_npcNameText != null && systemPrompt != null)
            {
                _npcNameText.text = systemPrompt.NpcName;
            }

            UpdateMemoryView(memory, systemPrompt);
            UpdateHistoryView(memory);
            ShowTab(true);

            _playerInputField.ActivateInputField();
        }

        public void Close()
        {
            if (_currentMemory != null)
                _currentMemory.OnMemoryChanged -= OnMemoryStateChanged;

            _closeSubject.OnNext(Unit.Default);
            DialogueCameraController.Instance?.ReleaseFocus();
            if (_canvas != null)
                _canvas.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnMemoryStateChanged()
        {
            UpdateMemoryView(_currentMemory, _currentSystemPrompt);
            UpdateHistoryView(_currentMemory);
        }
        #endregion

        #region 기억 및 히스토리 패널
        public void ShowTab(bool showMemory)
        {
            if (_memoryView != null) _memoryView.SetActive(showMemory);
            if (_historyView != null) _historyView.SetActive(!showMemory);

            if (_tabMemoryBtn != null && _tabMemoryBtn.image != null)
            {
                _tabMemoryBtn.image.color = showMemory ? Color.white : new Color(0.55f, 0.6f, 0.75f, 0.6f);
                var memTmp = _tabMemoryBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (memTmp != null) memTmp.color = showMemory ? Color.white : new Color(0.7f, 0.75f, 0.85f, 0.7f);
            }
            if (_tabHistoryBtn != null && _tabHistoryBtn.image != null)
            {
                _tabHistoryBtn.image.color = !showMemory ? Color.white : new Color(0.55f, 0.6f, 0.75f, 0.6f);
                var hisTmp = _tabHistoryBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (hisTmp != null) hisTmp.color = !showMemory ? Color.white : new Color(0.7f, 0.75f, 0.85f, 0.7f);
            }
        }

        public void UpdateMemoryView(NPCMemorySO memory, NPCSystemPromptSO systemPrompt)
        {
            if (memory == null) return;

            if (_relationSlider != null)
            {
                _relationSlider.minValue = -100f;
                _relationSlider.maxValue = 100f;
                _relationSlider.value = memory.RelationScore;
            }

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
                _relationText.text = $"{relDesc} ({memory.RelationScore:F0})";
            }

            if (_firstMetText != null)
            {
                string date = string.IsNullOrEmpty(memory.FirstMetDate) ? "최근" : memory.FirstMetDate;
                string loc = string.IsNullOrEmpty(memory.FirstMetLocation) ? "마을 광장" : memory.FirstMetLocation;
                _firstMetText.text = $"{loc} ({date})";
            }

            if (_conversationCountText != null)
            {
                _conversationCountText.text = $"{memory.ConversationCount}회";
            }

            if (_knownFactsContent != null)
            {
                foreach (Transform child in _knownFactsContent)
                    Destroy(child.gameObject);

                if (memory.KnownFacts != null && memory.KnownFacts.Count > 0)
                {
                    foreach (var fact in memory.KnownFacts)
                    {
                        if (_knownFactItemPrefab != null)
                        {
                            var item = Instantiate(_knownFactItemPrefab, _knownFactsContent);
                            var tmp = item.GetComponentInChildren<TextMeshProUGUI>();
                            if (tmp != null) tmp.text = $"• {fact}";
                        }
                        else
                        {
                            var go = new GameObject("FactItem", typeof(RectTransform));
                            go.transform.SetParent(_knownFactsContent, false);
                            var tmp = go.AddComponent<TextMeshProUGUI>();
                            tmp.text = $"• {fact}";
                            tmp.fontSize = 15;
                            tmp.color = new Color(0.9f, 0.9f, 0.95f, 0.9f);
                        }
                    }
                }
                else
                {
                    var go = new GameObject("EmptyFactItem", typeof(RectTransform));
                    go.transform.SetParent(_knownFactsContent, false);
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.text = "아직 기억하고 있는 특별한 사실이 없습니다.";
                    tmp.fontSize = 14;
                    tmp.fontStyle = FontStyles.Italic;
                    tmp.color = new Color(0.6f, 0.6f, 0.7f, 0.7f);
                }
            }
        }

        public void UpdateHistoryView(NPCMemorySO memory)
        {
            if (_historyContent == null) return;

            foreach (Transform child in _historyContent)
                Destroy(child.gameObject);

            if (memory != null && memory.DialogueHistory != null && memory.DialogueHistory.Count > 0)
            {
                for (int i = memory.DialogueHistory.Count - 1; i >= 0; i--)
                {
                    var entry = memory.DialogueHistory[i];
                    if (_historyItemPrefab != null)
                    {
                        var item = Instantiate(_historyItemPrefab, _historyContent);
                        var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                        if (texts.Length > 1)
                        {
                            texts[0].text = $"나: {entry.userText}";
                            texts[1].text = $"NPC: {entry.npcText}";
                        }
                        else if (texts.Length == 1)
                        {
                            texts[0].text = $"[{entry.timestamp}]\n나: {entry.userText}\nNPC: {entry.npcText}";
                        }
                    }
                    else
                    {
                        var go = new GameObject($"HistoryItem_{i}", typeof(RectTransform));
                        go.transform.SetParent(_historyContent, false);
                        var tmp = go.AddComponent<TextMeshProUGUI>();
                        tmp.text = $"<color=#818cf8><b>나:</b></color> {entry.userText}\n<color=#f59e0b><b>NPC:</b></color> {entry.npcText}";
                        tmp.fontSize = 14;
                        tmp.color = Color.white;
                    }
                }
            }
            else
            {
                var go = new GameObject("EmptyHistoryItem", typeof(RectTransform));
                go.transform.SetParent(_historyContent, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = "이전 대화 기록이 없습니다.";
                tmp.fontSize = 14;
                tmp.fontStyle = FontStyles.Italic;
                tmp.color = new Color(0.6f, 0.6f, 0.7f, 0.7f);
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
            // 2초 동안 TTS 음성 문장이 단 한 번도 도착하지 않은 경우 (TTS 장애/무음)
            if (!_ttsSentenceStarted)
            {
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
        #endregion
    }
}
