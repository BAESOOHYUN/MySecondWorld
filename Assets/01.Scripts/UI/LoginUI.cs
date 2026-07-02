using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    public class LoginUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _localPlayerNameInput;
        [SerializeField] private Toggle _hostToggle;
        [SerializeField] private Toggle _clientToggle;
        [SerializeField] private Button _playerPrefsDeleteAll;
        [SerializeField] private MonoBehaviour _networkManagerRaw;
        private IGameNetworkManager _networkManager => _networkManagerRaw as IGameNetworkManager;

        private void Start()
        {
            _localPlayerNameInput.onValueChanged.AddListener(OnNameChanged);
            _hostToggle.onValueChanged.AddListener(OnToggleHost);
            _clientToggle.onValueChanged.AddListener(OnToggleClient);
            _playerPrefsDeleteAll.onClick.AddListener(PlayerPrefsDeleteAll);
            _networkManager.OnClientStopped += ResetClientUI;
#if UNITY_EDITOR
            _localPlayerNameInput.text = "payu 1";
#elif UNITY_ANDROID || UNITY_IOS
    _localPlayerNameInput.text = "payu 2";
#endif
        }

        private void OnDestroy()
        {
            if (_localPlayerNameInput != null) _localPlayerNameInput.onValueChanged.RemoveListener(OnNameChanged);
            if (_hostToggle != null) _hostToggle.onValueChanged.RemoveListener(OnToggleHost);
            if (_clientToggle != null) _clientToggle.onValueChanged.RemoveListener(OnToggleClient);
            if (_playerPrefsDeleteAll != null) _playerPrefsDeleteAll.onClick.RemoveListener(PlayerPrefsDeleteAll);
            if (_networkManager != null) _networkManager.OnClientStopped -= ResetClientUI;
        }

        private void ResetClientUI()
        {
            _clientToggle.SetIsOnWithoutNotify(false);
            bool hasName = !string.IsNullOrEmpty(_localPlayerNameInput.text);
            _hostToggle.interactable = hasName;
            _clientToggle.interactable = hasName;
            _localPlayerNameInput.interactable = true;
        }

        private void OnNameChanged(string name)
        {
            bool hasName = !string.IsNullOrEmpty(name);
            _hostToggle.interactable = hasName;
            _clientToggle.interactable = hasName;
        }

        private void OnToggleHost(bool isOn)
        {
            if (isOn)
                SetPlayerName(_localPlayerNameInput.text);

            _networkManager.ToggleHost(isOn);
            _clientToggle.interactable = false;
        }

        private void OnToggleClient(bool isOn)
        {
            if (isOn)
                SetPlayerName(_localPlayerNameInput.text);

            _networkManager.ToggleClient(isOn);
            _hostToggle.interactable = false;
        }

        public void SetPlayerName(string username)
        {
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
