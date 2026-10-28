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

    [Inject] private CharacterCustomizeUI _characterCustomizeUI;
    [Inject] private InventoryUI _inventoryUI;

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

    private void OnEscapeClicked()
    {
        PlayerHealthState.Local?.CmdRequestEscape();
    }
    
    private void OnNpcTeleportClicked()
    {
        PlayerHealthState.Local?.CmdTeleportToNpc();
    }
}
