using MySecondWorld.Network;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private Toggle _robbyToggle;
    [SerializeField] private Toggle _settingsToggle;
    [SerializeField] private GameObject _settingsPanel;
    [Inject] private LoginUI _loginUI;

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
    }

    private void OnDestroy()
    {
        if (_robbyToggle != null)
            _robbyToggle.onValueChanged.RemoveListener(OnToggleRobby);
        if (_settingsToggle != null)
            _settingsToggle.onValueChanged.RemoveAllListeners();
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
