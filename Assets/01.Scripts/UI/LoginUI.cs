using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    public class LoginUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
        public static LoginUI Instance { get; private set; }

        [SerializeField] private TMP_InputField _localPlayerNameInput;
        [SerializeField] private Toggle _hostToggle;
        [SerializeField] private Toggle _clientToggle;
        [SerializeField] private Button _playerPrefsDeleteAll; 
        private UnityAction<string> _onNameChanged;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
                return;
            Instance = this;
        }

        private void Start()
        {
            _onNameChanged = name =>
            {
                bool hasName = !string.IsNullOrEmpty(name);
                _hostToggle.interactable = hasName;
                _clientToggle.interactable = hasName;
            };

            _localPlayerNameInput.onValueChanged.AddListener(_onNameChanged);
            _hostToggle.onValueChanged.AddListener(OnToggleHost);
            _clientToggle.onValueChanged.AddListener(OnToggleClient);
            _playerPrefsDeleteAll.onClick.AddListener(PlayerPrefsDeleteAll);
        }
        private void OnDestroy()
        {
            if (_localPlayerNameInput != null && _onNameChanged != null)
                _localPlayerNameInput.onValueChanged.RemoveListener(_onNameChanged);
            if (_hostToggle != null)
                _hostToggle.onValueChanged.RemoveListener(OnToggleHost);
            if (_clientToggle != null)
                _clientToggle.onValueChanged.RemoveListener(OnToggleClient);
            if (_playerPrefsDeleteAll != null)
                _playerPrefsDeleteAll.onClick.RemoveListener(PlayerPrefsDeleteAll);
        }

        private void OnToggleHost(bool isOn)
        {
            if (isOn) 
                SetPlayerName(_localPlayerNameInput.text);
            
            ((GameNetworkManager)NetworkManager.singleton).ToggleHost(isOn);
            _clientToggle.interactable = false;
        }

        private void OnToggleClient(bool isOn)
        {
            if (isOn) 
                SetPlayerName(_localPlayerNameInput.text);

            ((GameNetworkManager)NetworkManager.singleton).ToggleClient(isOn);
            _hostToggle.interactable = false;
        }

        // 지금은 InputField에서 받아와 쓰고,
        // TODO : username을 관리할 로그인단, 서버 구상
        public void SetPlayerName(string username)
        {
            // PlayerPrefs 임시
            PlayerPrefs.SetString("playerName", username);
            PlayerSession.LocalPlayerName = username;
            _localPlayerNameInput.interactable = false;
            Debug.Log($"[Login] SetPlayerName: '{PlayerSession.LocalPlayerName}'");
        }

        public void PlayerPrefsDeleteAll()
        {
            PlayerPrefs.DeleteAll();
        }
    }
}