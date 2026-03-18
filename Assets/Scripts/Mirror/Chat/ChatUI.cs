using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Mirror.Chat
{
    /// <summary>
    /// 채팅 UI 바인딩 담당.
    /// InputField + ScrollView + 전송 버튼 연결.
    /// 네트워크 로직은 ChatManager에 위임.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        [SerializeField] private ChatManager _chatManager;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Transform _messageContainer;  // ScrollView > Viewport > Content
        [SerializeField] private TMP_Text _messageTextPrefab;

        private void Awake()
        {
            if (_chatManager == null)
            {
                _chatManager = FindObjectOfType<ChatManager>();
                if (_chatManager == null)
                    Debug.LogError("[ChatUI] ChatManager를 씬에서 찾지 못함!", this);
                else
                    Debug.Log("[ChatUI] ChatManager를 FindObjectOfType으로 찾음");
            }

            if (_inputField == null)
                Debug.LogError("[ChatUI] InputField 참조 없음.", this);

            if (_sendButton == null)
                Debug.LogError("[ChatUI] SendButton 참조 없음.", this);

            if (_messageContainer == null)
                Debug.LogError("[ChatUI] MessageContainer 참조 없음.", this);

            if (_messageTextPrefab == null)
                Debug.LogError("[ChatUI] MessageTextPrefab 참조 없음.", this);
        }

        private void OnEnable()
        {
            Debug.Log($"[ChatUI] OnEnable — sendButton={_sendButton}");
            ChatManager.OnMessageReceived += AppendMessage;

            if (_sendButton != null)
                _sendButton.onClick.AddListener(OnSendClicked);
        }

        private void OnDisable()
        {
            ChatManager.OnMessageReceived -= AppendMessage;

            if (_sendButton != null)
                _sendButton.onClick.RemoveListener(OnSendClicked);
        }

        private void OnSendClicked()
        {
            Debug.Log($"[ChatUI] OnSendClicked — chatManager={_chatManager}, text='{_inputField?.text}'");
            if (_chatManager == null)
                return;

            string message = _inputField.text.Trim();
            if (string.IsNullOrWhiteSpace(message))
                return;

            _chatManager.TrySendMessage(message);
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();
        }

        private void AppendMessage(string message)
        {
            Debug.Log($"[ChatUI] AppendMessage — '{message}', prefab={_messageTextPrefab}, container={_messageContainer}");
            if (_messageTextPrefab == null || _messageContainer == null)
                return;

            TMP_Text entry = Instantiate(_messageTextPrefab, _messageContainer);
            entry.text = message;
        }
    }
}
