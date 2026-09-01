using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    public class LoginUI : MonoBehaviour
    {
        private const string PrefKeyServerIp = "lastServerIp";

        [SerializeField] private TMP_InputField _localPlayerNameInput;
        [SerializeField] private TMP_InputField _serverIpInput;
        [SerializeField] private Toggle _hostToggle;
        [SerializeField] private Toggle _clientToggle;
        [SerializeField] private Button _playerPrefsDeleteAll;
        [SerializeField] private MonoBehaviour _networkManagerRaw;
        private IGameNetworkManager _networkManager => _networkManagerRaw as IGameNetworkManager;

        private void Start()
        {
            _localPlayerNameInput.onValueChanged.AddListener(OnNameChanged);

            if (_serverIpInput != null)
            {
                _serverIpInput.text = PlayerPrefs.GetString(PrefKeyServerIp, "127.0.0.1");
                _serverIpInput.onValueChanged.AddListener(OnServerIpChanged);
            }

            if (_hostToggle != null)
                _hostToggle.onValueChanged.AddListener(OnToggleHost);

            if (_clientToggle != null)
                _clientToggle.onValueChanged.AddListener(OnToggleClient);

            if (_playerPrefsDeleteAll != null)
                _playerPrefsDeleteAll.onClick.AddListener(PlayerPrefsDeleteAll);

            if (_networkManager != null)
                _networkManager.OnClientStopped += ResetClientUI;

            // [플랫폼별 UI 및 기본 이름 분기]
#if UNITY_EDITOR
            _localPlayerNameInput.text = "payu 1";
            if (_hostToggle != null) _hostToggle.gameObject.SetActive(true);
            if (_clientToggle != null) _clientToggle.gameObject.SetActive(false); // 에디터는 Host만 노출
#elif UNITY_ANDROID || UNITY_IOS
            _localPlayerNameInput.text = "payu 2";
            if (_hostToggle != null) _hostToggle.gameObject.SetActive(false);     // 모바일은 Host 숨김
            if (_clientToggle != null) _clientToggle.gameObject.SetActive(true);  // 모바일은 Client만 노출
#else
            if (_hostToggle != null) _hostToggle.gameObject.SetActive(false);
            if (_clientToggle != null) _clientToggle.gameObject.SetActive(true);
#endif
        }

        private void OnDestroy()
        {
            if (_localPlayerNameInput != null) _localPlayerNameInput.onValueChanged.RemoveListener(OnNameChanged);
            if (_serverIpInput != null) _serverIpInput.onValueChanged.RemoveListener(OnServerIpChanged);
            if (_hostToggle != null) _hostToggle.onValueChanged.RemoveListener(OnToggleHost);
            if (_clientToggle != null) _clientToggle.onValueChanged.RemoveListener(OnToggleClient);
            if (_playerPrefsDeleteAll != null) _playerPrefsDeleteAll.onClick.RemoveListener(PlayerPrefsDeleteAll);
            if (_networkManager != null) _networkManager.OnClientStopped -= ResetClientUI;
        }

        private void OnServerIpChanged(string ip)
        {
            if (!string.IsNullOrWhiteSpace(ip) && _networkManager != null)
            {
                _networkManager.SetServerAddress(ip.Trim());
            }
        }

        private void ResetClientUI()
        {
            if (_clientToggle != null)
                _clientToggle.SetIsOnWithoutNotify(false);
            bool hasName = !string.IsNullOrEmpty(_localPlayerNameInput.text);
            if (_hostToggle != null) _hostToggle.interactable = hasName;
            if (_clientToggle != null) _clientToggle.interactable = hasName;
            _localPlayerNameInput.interactable = true;
            if (_serverIpInput != null) _serverIpInput.interactable = true;
        }

        private void OnNameChanged(string name)
        {
            bool hasName = !string.IsNullOrEmpty(name);
            if (_hostToggle != null) _hostToggle.interactable = hasName;
            if (_clientToggle != null) _clientToggle.interactable = hasName;
        }

        private void OnToggleHost(bool isOn)
        {
            if (isOn)
                SetPlayerName(_localPlayerNameInput.text);

            _networkManager.ToggleHost(isOn);
            if (_clientToggle != null) _clientToggle.interactable = false;
        }

        private void OnToggleClient(bool isOn)
        {
            if (isOn)
            {
                SetPlayerName(_localPlayerNameInput.text);

                if (_serverIpInput != null && !string.IsNullOrWhiteSpace(_serverIpInput.text))
                {
                    string targetIp = _serverIpInput.text.Trim();
                    PlayerPrefs.SetString(PrefKeyServerIp, targetIp);
                    PlayerPrefs.Save();
                    _networkManager.SetServerAddress(targetIp);
                    _serverIpInput.interactable = false;
                }
            }
            else
            {
                if (_serverIpInput != null) _serverIpInput.interactable = true;
            }

            _networkManager.ToggleClient(isOn);
            if (_hostToggle != null) _hostToggle.interactable = false;
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
