using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.UI;
using System;

namespace MySecondWorld.Network.Chat
{
    public class ChatUI : MonoBehaviour
    {
        public static ChatUI Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private GameObject _scrollView;  // ScrollView
        [SerializeField] private Transform _messageContainer;  // ScrollView > Viewport > Content
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private TMP_InputField _localPlayerNameInput;
        [SerializeField] private TMP_Text _messageTextPrefab;
        [SerializeField] private Toggle _hostToggle;
        [SerializeField] private Toggle _clientToggle;
        [SerializeField] private Button _exitButton;

        public string LocalPlayerName { get; private set; }

        public event Action<string> OnSubmitListener;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);  // ChatCanvas만 파괴, UIManager는 무사
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _messageInput.interactable = false;
            _hostToggle.interactable = false;
            _clientToggle.interactable = false;
            _scrollView.SetActive(false);

            // PlayerPrefs 임시
            string savedName = PlayerPrefs.GetString("playerName", "");
            if (!string.IsNullOrEmpty(savedName))
            {
                LocalPlayerName = savedName;
                _localPlayerNameInput.text = savedName;
                _localPlayerNameInput.onSubmit.AddListener(SetPlayerName);
                _hostToggle.onValueChanged.AddListener(OnToggleHost);
                _clientToggle.onValueChanged.AddListener(OnToggleClient);
            }
            _exitButton.onClick.AddListener(CloseChatScrollView); 
            _messageInput.onSelect.AddListener(_ => _scrollView.SetActive(true));

          
            if (PlayerPrefs.GetString("playerName", "") != null)
            {
                LocalPlayerName = PlayerPrefs.GetString("playerName", "");
                _localPlayerNameInput.text = LocalPlayerName;
            }
        }

        private void OnDestroy()
        {
            _localPlayerNameInput.onSubmit.RemoveListener(SetPlayerName);
            _messageInput.onSubmit.RemoveListener(OnSubmit);
            _hostToggle.onValueChanged.RemoveListener(OnToggleHost);
            _clientToggle.onValueChanged.RemoveListener(OnToggleClient);
            _exitButton.onClick.RemoveListener(CloseChatScrollView);
        }

        public void OnLocalPlayerReady()
        {
            _messageInput.interactable = true;
            _hostToggle.interactable = false;
            _clientToggle.interactable = false;
            _messageInput.onSubmit.AddListener(OnSubmit);
        }

        public void OnLocalPlayerStopped()
        {
            _localPlayerNameInput.interactable = true;
            _messageInput.interactable = false;
            _hostToggle.interactable = false;
            _clientToggle.interactable = false;
            _messageInput.onSubmit.RemoveListener(OnSubmit);
        }

        private void OnToggleHost(bool isOn)
        {
            ((GameNetworkManager)NetworkManager.singleton).ToggleHost(isOn);
        }

        private void OnToggleClient(bool isOn)
        {
            ((GameNetworkManager)NetworkManager.singleton).ToggleClient(isOn);
        }


        // 지금은 InputField에서 받아와 쓰고,
        // TODO : username을 관리할 로그인단, 서버 구상
        public void SetPlayerName(string username)
        {
            // PlayerPrefs 임시
            PlayerPrefs.SetString("playerName", username);

            LocalPlayerName = username;
            _localPlayerNameInput.interactable = false;
            _hostToggle.interactable = true;
            _clientToggle.interactable = true;
            Debug.Log($"[Chat] SetPlayerName: '{LocalPlayerName}'");
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

            if (playerName == LocalPlayerName && NetworkServer.active == true) // "HOST"일때
            {
                entry.text = $"<color=red>{playerName}:</color> {message}";
            }
            else if (playerName == LocalPlayerName) // "나"일때
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
            _messageContainer.gameObject.SetActive(!_scrollView.activeSelf);
        }
    }
}