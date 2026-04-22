using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using R3;
using VContainer;
using MessagePipe;
using MySecondWorld.AI;

namespace MySecondWorld.UI
{
    // 대화창, 타이핑 효과, STT/TTS 버튼 자리 확보

    /// <summary>
    /// NPC 대화창 UI.
    /// 스트리밍 타이핑 효과, 입력 필드, ESC/닫기 버튼 지원.
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
        [SerializeField] private Button _sttButton;   // 비활성 — 추후 연결
        [SerializeField] private Button _ttsButton;   // 비활성 — 추후 연결

        [Header("Typing Effect")]
        public float typingSpeed = 0.03f;

        // CancellationTokenSource 비동기 작업 취소 : 실행 중인 작업을 취소하거나 중지. 장기 실행 작업이나 무한 루프를 안전하게 중단하는 데 사용
        private CancellationTokenSource _typingCts;

        // R3 구독
        public Observable<string> OnSubmit => _submitButton.OnClickAsObservable()
            .Select(_ => _playerInputField.text.Trim())
            .Where(t => t.Length > 0);
        public Observable<Unit> OnClose => _closeButton.OnClickAsObservable().AsUnitObservable();

        private string _currentNpcGuid;
        private IDisposable _subscriptions;

        [Inject]
        private void Construct(
            ISubscriber<NPCResponseReceivedEvent> responseSub,
            ISubscriber<NPCThinkingStartedEvent> thinkingSub)
        {
            responseSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != _currentNpcGuid)
                        return;
                    ShowResponse(evt.NpcName, evt.Text);
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
            //Debug.Log($"[NPCDialogueUI] Open 호출, _dialoguePanel={_dialoguePanel}");
            _currentNpcGuid = npcGuid;
            _canvas.SetActive(true);
            _playerInputField.text = string.Empty;
            _npcNameText.text = string.Empty;
            _npcResponseText.text = string.Empty;
            _playerInputField.ActivateInputField();
            // STT/TTS 비활성화 (자리만 확보)
            _sttButton?.gameObject.SetActive(false);
            _ttsButton?.gameObject.SetActive(false);
        }

        public void Close()
        {
            StopTyping();
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

        #region 응답 표시
        public void ShowThinking()
        {
            StopTyping();
            _npcResponseText.text = "...";
        }

        public void ShowResponse(string npcName, string response)
        {
            if (_npcNameText != null) 
                _npcNameText.text = npcName;

            StopTyping();
            // 타이핑 시작할 때
            _typingCts = new CancellationTokenSource();
            TypeTextAsync(response, _typingCts.Token).Forget();
        }

        private async UniTask TypeTextAsync(string text, CancellationToken ct)
        {
            _npcResponseText.text = string.Empty;
            using var sb = ZString.CreateStringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i]);
                _npcResponseText.text = sb.ToString();
                ScrollToBottom();
                await UniTask.Delay(TimeSpan.FromSeconds(typingSpeed), cancellationToken: ct);
            }
        }

        private void StopTyping()
        {
            // 타이핑 멈춰야 할 때.
            _typingCts?.Cancel();
            _typingCts?.Dispose();
            _typingCts = null;
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }
        #endregion
    }
}
