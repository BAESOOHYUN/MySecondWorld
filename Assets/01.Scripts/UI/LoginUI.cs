using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    public class LoginUI : MonoBehaviour
    {
        // ── 서버 IP 상수 ──
        private const string CloudServerIp = "32.236.54.192";    // AWS 시드니 서버
        private const string LocalPcIp     = "192.168.219.109";  // 내 컴퓨터 로컬 IP

        [Header("입력 및 버튼")]
        [SerializeField] private TMP_InputField _localPlayerNameInput;
        [SerializeField] private TMP_InputField _localPlayerPasswordInput;
        [SerializeField] private Toggle _hostToggle;         // Host (에디터 전용)
        [SerializeField] private Toggle _cloudClientToggle;  // AWS 클라우드 접속
        [SerializeField] private Toggle _localClientToggle;  // 내 PC 로컬 접속
        [SerializeField] private Button _deletePlayerPrefsBtn;
        [SerializeField] private Button _restartServerBtn;
        [SerializeField] private MonoBehaviour _networkManagerRaw;
        private IGameNetworkManager _networkManager => _networkManagerRaw as IGameNetworkManager;

        private void Start()
        {
            _localPlayerNameInput.onValueChanged.AddListener(OnNameChanged);

            if (_hostToggle != null)
                _hostToggle.onValueChanged.AddListener(OnToggleHost);

            if (_cloudClientToggle != null)
                _cloudClientToggle.onValueChanged.AddListener(isOn => OnToggleClient(isOn, CloudServerIp));

            if (_localClientToggle != null)
                _localClientToggle.onValueChanged.AddListener(isOn => OnToggleClient(isOn, LocalPcIp));

            if (_restartServerBtn != null)
                _restartServerBtn.onClick.AddListener(RestartServer);

            if (_networkManager != null)
                _networkManager.OnClientStopped += ResetClientUI;

            // [플랫폼별 버튼 노출 제어]
#if UNITY_EDITOR
            _localPlayerNameInput.text = "payu1";
            if (_hostToggle != null) _hostToggle.gameObject.SetActive(true);               // 에디터: Host 노출
            if (_cloudClientToggle != null) _cloudClientToggle.gameObject.SetActive(true); // 에디터: AWS 노출
            if (_localClientToggle != null) _localClientToggle.gameObject.SetActive(false); // 에디터: 로컬접속 불필요
#elif UNITY_ANDROID || UNITY_IOS
            //_localPlayerNameInput.text = "payu2";
            if (_hostToggle != null) _hostToggle.gameObject.SetActive(false);              // 모바일: Host 숨김
            if (_cloudClientToggle != null) _cloudClientToggle.gameObject.SetActive(true); // 모바일: AWS 노출
            if (_localClientToggle != null) _localClientToggle.gameObject.SetActive(true); // 모바일: 내 PC 노출
#endif
            // 저장된 이름/비밀번호가 있으면 자동 입력 (위 테스트용 하드코딩 값보다 우선)
            string savedName = PlayerPrefs.GetString("playerName", "");
            if (!string.IsNullOrEmpty(savedName))
                _localPlayerNameInput.text = savedName;

            string savedPassword = PlayerPrefs.GetString("playerPassword", "");
            if (!string.IsNullOrEmpty(savedPassword))
                _localPlayerPasswordInput.text = savedPassword;

            OnNameChanged(_localPlayerNameInput.text);
        }

        private void OnDestroy()
        {
            if (_localPlayerNameInput != null) _localPlayerNameInput.onValueChanged.RemoveListener(OnNameChanged);
            if (_hostToggle != null) _hostToggle.onValueChanged.RemoveListener(OnToggleHost);
            if (_restartServerBtn != null) _restartServerBtn.onClick.RemoveListener(RestartServer);
            if (_networkManager != null) _networkManager.OnClientStopped -= ResetClientUI;
        }

        private void ResetClientUI()
        {
            if (_cloudClientToggle != null) _cloudClientToggle.SetIsOnWithoutNotify(false);
            if (_localClientToggle != null) _localClientToggle.SetIsOnWithoutNotify(false);

            bool hasName = !string.IsNullOrEmpty(_localPlayerNameInput.text);
            if (_hostToggle != null) _hostToggle.interactable = hasName;
            if (_cloudClientToggle != null) _cloudClientToggle.interactable = hasName;
            if (_localClientToggle != null) _localClientToggle.interactable = hasName;
            _localPlayerNameInput.interactable = true;
        }

        private void OnNameChanged(string name)
        {
            bool hasName = !string.IsNullOrEmpty(name);
            if (_hostToggle != null) _hostToggle.interactable = hasName;
            if (_cloudClientToggle != null) _cloudClientToggle.interactable = hasName;
            if (_localClientToggle != null) _localClientToggle.interactable = hasName;
        }

        private void OnToggleHost(bool isOn)
        {
            if (isOn)
            {
                SetPlayerName(_localPlayerNameInput.text);
                SetPlayerPassword(_localPlayerPasswordInput.text);
            }

            _networkManager.ToggleHost(isOn);
            if (_cloudClientToggle != null) _cloudClientToggle.interactable = !isOn;
            if (_localClientToggle != null) _localClientToggle.interactable = !isOn;
        }

        private void OnToggleClient(bool isOn, string targetIp)
        {
            if (isOn)
            {
                SetPlayerName(_localPlayerNameInput.text);
                SetPlayerPassword(_localPlayerPasswordInput.text);
                _networkManager.SetServerAddress(targetIp);
                Debug.Log($"[Login] Connecting to '{targetIp}'...");
            }

            _networkManager.ToggleClient(isOn);

            if (_hostToggle != null) _hostToggle.interactable = !isOn;
            if (_cloudClientToggle != null && targetIp != CloudServerIp) _cloudClientToggle.interactable = !isOn;
            if (_localClientToggle != null && targetIp != LocalPcIp) _localClientToggle.interactable = !isOn;
        }

        public void SetPlayerName(string username)
        {
            PlayerPrefs.SetString("playerName", username);
            PlayerSession.LocalPlayerName = username;
            _localPlayerNameInput.interactable = false;
            Debug.Log($"[Login] SetPlayerName: '{PlayerSession.LocalPlayerName}'");
        }

        public void SetPlayerPassword(string password)
        {
            PlayerPrefs.SetString("playerPassword", password);
            PlayerSession.LocalPlayerPassword = password;
            _localPlayerPasswordInput.interactable = false;
        }

        public void DeleteAllPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
        }

        public void RestartServer()
        {
#if UNITY_EDITOR
            var toolType = System.Type.GetType("MySecondWorld.Editor.ServerBuildTool, Assembly-CSharp-Editor");
            if (toolType != null)
            {
                var method = toolType.GetMethod("RestartEC2ServerOnly", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                    method.Invoke(null, null);
                else
                    Debug.LogError("[Login] RestartEC2ServerOnly 메서드를 찾을 수 없습니다.");
            }
            else
                Debug.LogError("[Login] ServerBuildTool 타입을 찾을 수 없습니다.");
#else
            Debug.LogWarning("[Login] RestartServer는 에디터에서만 사용 가능합니다.");
#endif
        }
    }
}
