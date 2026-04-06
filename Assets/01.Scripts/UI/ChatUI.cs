using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

namespace MySecondWorld.Network.Chat
{
    public class ChatUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
        public static ChatUI Instance { get; private set; }

        public event Action<string> OnSubmitListener;

        private UnityEngine.Events.UnityAction<string> _onSelectHandler;

        [SerializeField] private GameObject _scrollView;  // ScrollView
        [SerializeField] private Transform _messageContainer;  // ScrollView > Viewport > Content
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private TMP_Text _messageTextPrefab;
        [SerializeField] private Button _exitButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                return;
            Instance = this;
        }

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
        }

        public void CloseChatScrollView()
        {
            _scrollView.SetActive(!_scrollView.activeSelf);
        }

    }
}