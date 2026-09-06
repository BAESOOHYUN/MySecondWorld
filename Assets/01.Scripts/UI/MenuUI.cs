using UnityEngine;
using UnityEngine.UI;
using VContainer;
using MySecondWorld.Network;
using MySecondWorld.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private Toggle _robbyToggle;
    [SerializeField] private Toggle _settingsToggle;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private Button _settingsBackdropButton;
    [SerializeField] private Button _customizerButton;
    [SerializeField] private Button _inventoryButton;

    [Inject] private LoginUI _loginUI; 
    [Inject] private CharacterCustomizeUI _characterCustomizeUI;
    [Inject] private InventoryUI _inventoryUI;

    private void OnEnable()
    {
        if (_robbyToggle != null)
            _robbyToggle.isOn = false;
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    private void Start()
    {
        if (_robbyToggle != null)
            _robbyToggle.onValueChanged.AddListener(OnToggleRobby);
        if (_settingsToggle != null)
            _settingsToggle.onValueChanged.AddListener(isOn => _settingsPanel.SetActive(isOn));
        if (_settingsBackdropButton != null)
            _settingsBackdropButton.onClick.AddListener(() =>
            {
                if (_settingsToggle != null) _settingsToggle.isOn = false;
                else if (_settingsPanel != null) _settingsPanel.SetActive(false);
            });
        if (_customizerButton != null)
            _customizerButton.onClick.AddListener(() => _characterCustomizeUI.Open());
        if (_inventoryButton != null)
            _inventoryButton.onClick.AddListener(() => _inventoryUI.Open());
    }

    private void Update()
    {
        if (_settingsPanel != null && _settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            if (_settingsToggle != null) _settingsToggle.isOn = false;
            else _settingsPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_robbyToggle != null)
            _robbyToggle.onValueChanged.RemoveListener(OnToggleRobby);
        if (_settingsToggle != null)
            _settingsToggle.onValueChanged.RemoveAllListeners();
        if (_settingsBackdropButton != null)
            _settingsBackdropButton.onClick.RemoveAllListeners();
        if (_customizerButton != null)
            _customizerButton.onClick.RemoveAllListeners();
        if (_inventoryButton != null)
            _inventoryButton.onClick.RemoveAllListeners();
    }

    private void OnToggleRobby(bool isOn)
    {
        if (_loginUI == null)
        {
            Debug.LogWarning("[MenuUI] LoginUI가 null임. ProjectLifetimeScope 연결 확인");
            return;
        }

        _loginUI.gameObject.SetActive(isOn);
    }
}
