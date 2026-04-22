using MySecondWorld.Network;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private Toggle _robbyToggle;
    [Inject] private LoginUI _loginUI;

    private void OnEnable()
    {
        if (_robbyToggle != null)
            _robbyToggle.isOn = false;
    }

    private void Start()
    {
        if (_robbyToggle != null)
            _robbyToggle.onValueChanged.AddListener(OnToggleRobby);
    }

    private void OnDestroy()
    {
        if (_robbyToggle != null)
            _robbyToggle.onValueChanged.RemoveListener(OnToggleRobby);
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
