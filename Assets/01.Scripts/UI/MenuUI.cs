using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Network;

public class MenuUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }
    public static MenuUI Instance { get; private set; }


    [SerializeField] private Toggle _robbyToggle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;
        Instance = this;
    }

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
        if (LoginUI.Instance == null)
        {
            Debug.LogWarning("[MenuUI] LoginUI.Instance가 null임. 인스펙터에서 확인");
            return;
        }

        LoginUI.Instance.gameObject.SetActive(isOn);
    }
}