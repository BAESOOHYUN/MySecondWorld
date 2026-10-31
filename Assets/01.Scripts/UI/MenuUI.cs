using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Cysharp.Threading.Tasks;
using MySecondWorld.Network;
using MySecondWorld.UI;
using MySecondWorld.Combat.Player;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private Button _logoutButton;
    [SerializeField] private Button _escapeButton;
    [SerializeField] private Button _npcTeleportButton;
    [SerializeField] private Button _customizerButton;
    [SerializeField] private Button _inventoryButton;

    [Inject] private IUIManager _uiManager;
    [Inject] private CharacterCustomizeUI _characterCustomizeUI;
    [Inject] private InventoryUI _inventoryUI;
    private ConfirmPanelUI _confirmPanel;

    private void Start()
    {
        if (_logoutButton != null)
            _logoutButton.onClick.AddListener(OnLogoutClicked);
        if (_escapeButton != null)
            _escapeButton.onClick.AddListener(OnEscapeClicked);
        if (_npcTeleportButton != null)
            _npcTeleportButton.onClick.AddListener(OnNpcTeleportClicked);
        if (_customizerButton != null)
            _customizerButton.onClick.AddListener(() => _characterCustomizeUI.Open());
        if (_inventoryButton != null)
            _inventoryButton.onClick.AddListener(() => _inventoryUI.Open());

        _confirmPanel = _uiManager?.GetOrCreateDynamicCanvas<ConfirmPanelUI>()?.GetComponent<ConfirmPanelUI>();
    }

    private void OnDestroy()
    {
        if (_logoutButton != null)
            _logoutButton.onClick.RemoveAllListeners();
        if (_escapeButton != null)
            _escapeButton.onClick.RemoveAllListeners();
        if (_npcTeleportButton != null)
            _npcTeleportButton.onClick.RemoveAllListeners();
        if (_customizerButton != null)
            _customizerButton.onClick.RemoveAllListeners();
        if (_inventoryButton != null)
            _inventoryButton.onClick.RemoveAllListeners();
    }

    private void OnLogoutClicked()
    {
        if (_confirmPanel == null) // 팝업이 없으면 예전처럼 바로 로그아웃(안전망)
        {
            DoLogout();
            return;
        }

        _confirmPanel.Show("로그아웃", "로그아웃하시겠습니까?", "로그아웃", DoLogout);
    }

    private void OnEscapeClicked()
    {
        PlayerHealthState.Local?.CmdRequestEscape();
    }

    private void OnNpcTeleportClicked()
    {
        PlayerHealthState.Local?.CmdTeleportToNpc();
    }

    private void DoLogout()
    {
        if (Mirror.NetworkManager.singleton is GameNetworkManager gnm)
        {
            gnm.CleanupAndReturnToRobbyAsync().Forget();
        }
        else
        {
            if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
                Mirror.NetworkManager.singleton.StopHost();
            else if (Mirror.NetworkClient.isConnected || Mirror.NetworkClient.active)
                Mirror.NetworkManager.singleton.StopClient();

            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("OpenWorldRobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}