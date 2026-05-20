using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using VContainer;
using MessagePipe;
using MySecondWorld.AI;
using MySecondWorld.AI.Voice;
using System.Threading;
using UnityEngine.EventSystems;

namespace MySecondWorld.UI
{
    /// <summary>
    /// NPC 대화창 UI.
    /// TTS 문장 단위 텍스트 동기화, 입력 필드, ESC/닫기 버튼 지원.
    /// </summary>
    public class NPCDialogueUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _canvas;
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private Transform _chatContent;
        [SerializeField] private TMP_InputField _playerInputField;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Chat Bubble Prefabs")]
        [SerializeField] private GameObject _npcBubblePrefab;
        [SerializeField] private GameObject _playerBubblePrefab;
        [SerializeField] private GameObject _thinkingBubblePrefab;

        [Header("Future: Voice")]
        [SerializeField] private Button _sttButton;
        [SerializeField] private Button _ttsButton;

        private IPublisher<NPCStopTTSEvent> _stopTTSPub;
        private ISTTService _stt;
        private CancellationTokenSource _sttCts;

        private ChatBubble _thinkingBubble;
        private CancellationTokenSource _thinkingAnimCts;
        private string _currentNpcGuid;
        private string _lastNpcGuid;

        // R3 구독
        public Observable<string> OnSubmit => _submitButton.OnClickAsObservable()
            .Select(_ => _playerInputField.text.Trim())
            .Where(t => t.Length > 0);
        public Observable<Unit> OnClose => _closeButton.OnClickAsObservable().AsUnitObservable();

        [Inject]
        private void Construct(
            ISubscriber<NPCResponseReceivedEvent> responseSub,
            ISubscriber<NPCThinkingStartedEvent> thinkingSub,
            ISubscriber<NPCTTSSentenceEvent> ttsSentenceSub, 
            ISubscriber<NPCSpeakFinishedEvent> speakFinishedSub,
            IPublisher<NPCStopTTSEvent> stopTTSPub,
            ISTTService stt
            )
        {
            // 스트리밍 중 문장 단위로 텍스트 누적 — TTS와 동시에 표시
            ttsSentenceSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid)
                        return;
                    AppendSentence(evt.Text);
                })
                .AddTo(destroyCancellationToken);

            // TTS 마지막 문장 재생 완료 → speakFinishedSub 발행 → SetInputEnabled(true) ← 적절한 타이밍
            // TTS 재생 완료 → 입력창 활성화
            speakFinishedSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid)
                        return;
                    SetInputEnabled(true);
                })
                .AddTo(destroyCancellationToken);

            // TTS 서버 꺼진 경우 안전망: 5초 후 강제 활성화
            responseSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid)
                        return;
                    if (_npcNameText != null)
                        _npcNameText.text = evt.NpcName;
                    FallbackEnableInputAsync(destroyCancellationToken).Forget();
                })
                .AddTo(destroyCancellationToken);

            thinkingSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid)
                        return;
                    ShowThinking();
                    SetInputEnabled(false);
                })
                .AddTo(destroyCancellationToken);

            _submitButton.OnClickAsObservable()
                .Select(_ => _playerInputField.text.Trim())
                .Where(t => t.Length > 0)
                .Subscribe(t => ShowPlayerBubble(t))
                .AddTo(destroyCancellationToken);

            _stopTTSPub = stopTTSPub;
            if (_ttsButton != null)
                _ttsButton.OnClickAsObservable()
                        .Subscribe(_ => _stopTTSPub.Publish(new NPCStopTTSEvent(_currentNpcGuid))) // NPCVoicePlayer가 구독
                        .AddTo(destroyCancellationToken);

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

            if (!valid)
            {
                enabled = false;
            }
        }
        #endregion

        #region 열기 / 닫기
        public void Open(string npcGuid)
        {
            _currentNpcGuid = npcGuid;
            _canvas.SetActive(true);
            _playerInputField.text = string.Empty;

            // 같은 npc → 히스토리 유지, 다른 NPC → 초기화
            if(npcGuid != _lastNpcGuid)
            {
                ClearChat();
                if (_npcNameText != null)
                    _npcNameText.text = string.Empty;
                _lastNpcGuid = npcGuid;
            }
            _playerInputField.ActivateInputField();
        }

        public void Close()
        {
            _canvas.SetActive(false);
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
        #endregion

        #region STT 헬퍼 메서드
        private void StartSTT()
        {
            _sttCts?.Cancel(); // 작업 멈춤
            _sttCts?.Dispose(); // 리소스 해제
            // CreateLinkedTokenSource : 두 개의 CancellationToken을 하나로 묶는 것
            // _sttCts.Cancel() 사용자가 대화창 닫음
            // destroyCancellationToken GameObject가 파괴됨 (씬 전환 등)
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
                _submitButton.onClick.Invoke();
            }
            _sttCts?.Dispose();
            _sttCts = null;
        }
        #endregion

        private async UniTaskVoid FallbackEnableInputAsync(CancellationToken ct)
        {
            // ResponseSub가 5초 fallback이 된 이유:
            // TTS 서버가 꺼져 있으면 speakFinishedSub가 영원히 안 와서 입력창이 영원히 잠겨버린다.
            // LLM 응답 완료 → responseSub → 5초 대기 → SetInputEnabled(true)  ← 서버 꺼진 경우 구제
            //                                ↑
            //         정상 상황에선 speakFinishedSub가 먼저 활성화시키고,
            //         5초 후에 한 번 더 호출돼도 이미 활성화된 상태라 무해함
            await UniTask.Delay(5000, cancellationToken: ct);
            SetInputEnabled(true);
        }
    }
}
