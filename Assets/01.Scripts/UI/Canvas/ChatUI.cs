using System;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

namespace MySecondWorld.Network.Chat
{
    public class ChatUI : MonoBehaviour
    {
        public event Action<string> OnSubmitListener;

        private UnityEngine.Events.UnityAction<string> _onSelectHandler;

        [SerializeField] private GameObject _scrollView;  // ScrollView
        [SerializeField] private Transform _messageContainer;  // ScrollView > Viewport > Content
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private TMP_Text _messageTextPrefab;
        [SerializeField] private Button _exitButton;

        private void Start()
        {
            _messageInput.interactable = false;
            _scrollView.SetActive(false);
            _exitButton.onClick.AddListener(CloseChatScrollView);
            _onSelectHandler = _ => _scrollView.SetActive(true);
            _messageInput.onSelect.AddListener(_onSelectHandler);
        }

        private void OnDestroy()
        {
            if (_messageInput != null)
            {
                _messageInput.onSubmit.RemoveListener(OnSubmit);
                if (_onSelectHandler != null)
                    _messageInput.onSelect.RemoveListener(_onSelectHandler);
            }
            if (_exitButton != null)
                _exitButton.onClick.RemoveListener(CloseChatScrollView);
        }

        public void OnLocalPlayerReady()
        {
            if (_messageInput == null) return;
            _messageInput.interactable = true;
            _messageInput.onSubmit.AddListener(OnSubmit);
        }

        public void OnLocalPlayerStopped()
        {
            if (_messageInput == null) return;
            _messageInput.interactable = false;
            _messageInput.onSubmit.RemoveListener(OnSubmit);
        }

        private void OnSubmit(string msg)
        {
            msg = msg.Trim();
            if (string.IsNullOrWhiteSpace(msg)) return;

            OnSubmitListener?.Invoke(msg);
            _messageInput.text = string.Empty;
            ReactivateInputFieldAsync().Forget();
        }

        // TMP InputField는 Enter 제출 후 자체 deselect → 다음 프레임에 재활성화해야 이동 입력 차단 유지
        private async UniTaskVoid ReactivateInputFieldAsync()
        {
            await UniTask.NextFrame();
            if (_messageInput != null && _messageInput.interactable)
                _messageInput.ActivateInputField();
        }

        public void CreateMessagePrefab(string playerName, string message)
        {
            if (_messageTextPrefab == null || _messageContainer == null)
            {

                Debug.LogWarning("prefab or container is null");
                return;
            }

            TMP_Text entry = Instantiate(_messageTextPrefab, _messageContainer);

            string color = playerName == PlayerSession.LocalPlayerName
                ? (NetworkServer.active ? "red" : "blue")
                : "green";

            using var sb = ZString.CreateStringBuilder();
            sb.Append("<color=");
            sb.Append(color);
            sb.Append(">");
            sb.Append(playerName);
            sb.Append(":</color> ");
            sb.Append(message);
            entry.text = sb.ToString();
        }

        public void CloseChatScrollView()
        {
            _scrollView.SetActive(!_scrollView.activeSelf);
        }

    }
}