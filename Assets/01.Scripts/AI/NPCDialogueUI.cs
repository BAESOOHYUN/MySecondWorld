using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.AI
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
        [SerializeField] private GameObject _dialoguePanel;
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

        private Action<string> _onSubmit;
        private Action _onClose;
        private Coroutine _typingCoroutine;

        // ── 필수 참조 검증 ───────────────────────────────────────────────────────
        private void Awake()
        {
            bool valid = true;

            if (_dialoguePanel == null)
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

        // ── 열기 / 닫기 ──────────────────────────────────────────────────────────
        public void Open(Action<string> onSubmit, Action onClose)
        {
            _onSubmit = onSubmit;
            _onClose = onClose;

            _dialoguePanel.SetActive(true);
            _playerInputField.text = string.Empty;
            _npcResponseText.text = string.Empty;
            _playerInputField.ActivateInputField();

            _submitButton.onClick.RemoveAllListeners();
            _submitButton.onClick.AddListener(Submit);

            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => _onClose?.Invoke());

            // STT/TTS 비활성화 (자리만 확보)
            _sttButton?.gameObject.SetActive(false);
            _ttsButton?.gameObject.SetActive(false);
        }

        public void Close()
        {
            StopTyping();
            _dialoguePanel.SetActive(false);
        }

        // ── 입력 제어 ────────────────────────────────────────────────────────────
        public void SetInputEnabled(bool enabled)
        {
            _playerInputField.interactable = enabled;
            _submitButton.interactable = enabled;
            if (enabled) _playerInputField.ActivateInputField();
        }

        // ── 응답 표시 ────────────────────────────────────────────────────────────
        public void ShowThinking()
        {
            StopTyping();
            _npcResponseText.text = "...";
        }

        public void ShowResponse(string npcName, string response)
        {
            if (_npcNameText != null) _npcNameText.text = npcName;
            StopTyping();
            _typingCoroutine = StartCoroutine(TypeText(response));
        }

        private IEnumerator TypeText(string text)
        {
            _npcResponseText.text = string.Empty;
            var sb = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                sb.Append(c);
                _npcResponseText.text = sb.ToString();
                ScrollToBottom();
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        private void StopTyping()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }

        // ── 제출 ─────────────────────────────────────────────────────────────────
        private void Submit()
        {
            string input = _playerInputField.text.Trim();
            if (string.IsNullOrEmpty(input)) return;
            _playerInputField.text = string.Empty;
            _onSubmit?.Invoke(input);
        }

        // ── ESC 단축키 ───────────────────────────────────────────────────────────
        private void Update()
        {
            // TODO: New Input System의 cancelAction으로 교체 필요 (현재 레거시 Input 혼용)
            // if (Input.GetKeyDown(KeyCode.Escape)) _onClose?.Invoke();
        }
    }
}
