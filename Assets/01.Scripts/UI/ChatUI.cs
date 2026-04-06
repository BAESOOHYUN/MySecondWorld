using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

namespace MySecondWorld.Network.Chat
{
    public class ChatUI : MonoBehaviour
    {
        public event Action<string> OnSubmitListener;

        [SerializeField] private GameObject _scrollView;  // ScrollView
        [SerializeField] private Transform _messageContainer;  // ScrollView > Viewport > Content
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private TMP_Text _messageTextPrefab;
        [SerializeField] private Button _exitButton;
        public static ChatUI Instance { get; private set; }
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _messageInput.interactable = false;
            _scrollView.SetActive(false);
            _exitButton.onClick.AddListener(CloseChatScrollView);
            _messageInput.onSelect.AddListener(_ => _scrollView.SetActive(true));
        }

        private void OnDestroy()
        {
            _messageInput.onSubmit.RemoveListener(OnSubmit);
            _exitButton.onClick.RemoveListener(CloseChatScrollView);
        }

        public void OnLocalPlayerReady()
        {
            _messageInput.interactable = true;
            _messageInput.onSubmit.AddListener(OnSubmit);
        }

        public void OnLocalPlayerStopped()
        {
            _messageInput.interactable = false;
            _messageInput.onSubmit.RemoveListener(OnSubmit);
        }

        private void OnSubmit(string msg)
        {
            msg = msg.Trim();
            if (string.IsNullOrWhiteSpace(msg)) return;

            OnSubmitListener?.Invoke(msg);
            _messageInput.text = string.Empty;
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

            if (playerName == PlayerSession.LocalPlayerName && NetworkServer.active == true) // "HOST"일때
            {
                entry.text = $"<color=red>{playerName}:</color> {message}";
            }
            else if (playerName == PlayerSession.LocalPlayerName) // "나"일때
            {
                entry.text = $"<color=blue>{playerName}:</color> {message}";
            }
            else // "다른 클라이언트"
            {
                entry.text = $"<color=green>{playerName}:</color> {message}";
            }
            Debug.Log($"entry   {entry != null}");
        }

        public void CloseChatScrollView()
        {
            _scrollView.SetActive(!_scrollView.activeSelf);
        }

    }
}