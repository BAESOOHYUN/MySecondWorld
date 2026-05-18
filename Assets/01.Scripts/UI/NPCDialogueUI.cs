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
    /// STT/TTS 버튼은 비활성 상태로 자리 확보.
    /// </summary>
    public class NPCDialogueUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _canvas;
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private TextMeshProUGUI _npcResponseText;
        [SerializeField] private TMP_InputField _playerInputField;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Future: Voice")]
        [SerializeField] private Button _sttButton;
        [SerializeField] private Button _ttsButton;

        private IPublisher<NPCStopTTSEvent> _stopTTSPub;
        private ISTTService _stt;
        private CancellationTokenSource _sttCts;

        // R3 구독
        public Observable<string> OnSubmit => _submitButton.OnClickAsObservable()
            .Select(_ => _playerInputField.text.Trim())
            .Where(t => t.Length > 0);
        public Observable<Unit> OnClose => _closeButton.OnClickAsObservable().AsUnitObservable();

        private string _currentNpcGuid;

        [Inject]
        private void Construct(
            ISubscriber<NPCResponseReceivedEvent> responseSub,
            ISubscriber<NPCThinkingStartedEvent> thinkingSub,
            ISubscriber<NPCTTSSentenceEvent> ttsSentenceSub,
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

            // 스트리밍 완료 — 입력만 활성화
            responseSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid)
                        return;
                    if (_npcNameText != null)
                        _npcNameText.text = evt.NpcName;
                    SetInputEnabled(true);
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
            if (_playerInputField == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: playerInputField가 연결되지 않았습니다.");
                valid = false;
            }
            if (_npcResponseText == null)
            {
                Debug.LogError($"[NPCDialogueUI] {name}: npcResponseText가 연결되지 않았습니다.");
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
            _npcNameText.text = string.Empty;
            _npcResponseText.text = string.Empty;
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
            if (enabled) 
                _playerInputField.ActivateInputField();
        }

        public void ClearInput()
        {
            _playerInputField.text = string.Empty;
        }
        #endregion

        #region TTS 응답 표시
        public void ShowThinking()
        {
            _npcResponseText.text = "...";
        }

        // TTS 문장 도착 시 즉시 누적 표시 — TTS 재생과 동기화
        private void AppendSentence(string sentence)
        {
            if (_npcResponseText.text == "...")
                _npcResponseText.text = sentence;
            else
                _npcResponseText.text += sentence;
            ScrollToBottom();
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
                _playerInputField.ActivateInputField();
            }
            _sttCts?.Dispose();
            _sttCts = null;
        }
        #endregion
    }
}
