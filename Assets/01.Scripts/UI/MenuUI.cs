using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Network;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private Toggle _robbyToggle;
    public static MenuUI Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (_robbyToggle != null)
            _robbyToggle.isOn = false;
    }

    private void Start()
    {
        _robbyToggle.onValueChanged.AddListener(OnToggleRobby);
    }

    private void OnDestroy()
    {

        _robbyToggle.onValueChanged.RemoveListener(OnToggleRobby);
    }

    private void OnToggleRobby(bool isOn)
    {
        if (LoginUI.Instance == null)
        {
            Debug.LogWarning("[MenuUI] LoginUI.Instance가 null임. 인스펙터에서 확인");
            return;
        }

        LoginUI.Instance.gameObject.SetActive(isOn);
    }
}