using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.Network;

public class UIManager : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetSingleton()
    {
        Instance = null; // Domain Reload OFF 환경에서도 매번 초기화
    }
    public static UIManager Instance { get; private set; }

    [SerializeField] private List<Transform> uiRoots = new List<Transform>();
    [SerializeField] private float alphaThreshold = 0.5f;

    [Header("상시 캔버스")]
    [SerializeField] private GameObject _menuCanvas;
    [SerializeField] private GameObject _loginCanvas;
    [SerializeField] private GameObject _chatCanvas;
    [SerializeField] private GameObject _joystickCanvas;
    [Header("동적 캔버스 프리팹")]
    [SerializeField] private List<GameObject> _dynamicCanvasPrefabs = new List<GameObject>();
    private readonly Dictionary<System.Type, GameObject> _dynamicCanvasInstances = new Dictionary<System.Type, GameObject>();


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyUIClickSettingsToAllRoots();
        SetLobbyState();
    }

    // 이벤트 구독
    private void OnEnable()
    {
        PlayerAuthenticator.OnAuthenticated += SetGameState;
        PlayerAuthenticator.OnDisconnected += ResetToLobbyState;
    }

    private void OnDisable()
    {
        PlayerAuthenticator.OnAuthenticated -= SetGameState;
        PlayerAuthenticator.OnDisconnected -= ResetToLobbyState;
    }

    // 상태 전환 메서드들
    private void SetLobbyState()
    {
        if (_menuCanvas != null) _menuCanvas.SetActive(false);
        if (_loginCanvas != null) _loginCanvas.SetActive(true);
        if (_chatCanvas != null) _chatCanvas.SetActive(false);
        if (_joystickCanvas != null) _joystickCanvas.SetActive(false);
    }

    private void SetGameState()
    {
        if (_menuCanvas != null) _menuCanvas.SetActive(true);
        if (_loginCanvas != null) _loginCanvas.SetActive(false);
        if (_chatCanvas != null) _chatCanvas.SetActive(true);
        if (_joystickCanvas != null) _joystickCanvas.SetActive(true);
    }

    public GameObject GetOrCreateDynamicCanvas<T>() where T : Component
    {
        var type = typeof(T);

        // 이미 인스턴스가 있으면 재활용
        if (_dynamicCanvasInstances.TryGetValue(type, out var existing) && existing != null)
            return existing;

        // 프리팹 리스트에서 해당 컴포넌트가 붙은 프리팹 찾기
        foreach (var prefab in _dynamicCanvasPrefabs)
        {
            if (prefab.GetComponent<T>() != null)
            {
                var instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                _dynamicCanvasInstances[type] = instance;
                return instance;
            }
        }

        Debug.LogWarning($"[UIManager] {type.Name} 프리팹을 찾을 수 없습니다.");
        return null;
    }
    private void ResetToLobbyState()
    {
        foreach (var kvp in _dynamicCanvasInstances)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }

        SetLobbyState();
    }

    public void ApplyUIClickSettingsToAllRoots()
    {
        foreach (var root in uiRoots)
        {
            if (root != null)
                ApplyUIClickSettings(root.gameObject);
        }
    }

    public void ApplyUIClickSettings(GameObject root)
    {
        // Image 처리
        // ※ 모든 Image 에셋에서 Advanced > Read/Write 옵션을 체크할 것!
        // 모든 Image에 threshold 적용 (UI 투명한 부분 클릭해도 반응X)
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            image.alphaHitTestMinimumThreshold = alphaThreshold;
        }

        // TMP 처리 (RaycastTarget이 켜져 있는 경우만)
        // 권장: 버튼은 Image +TMP 라벨 구조
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            tmp.raycastTarget = false; //글자는 클릭 불가로 만들기
        }
    }

    // 지정한 프리팹을 특정 UI Root에 생성 (팩토리 패턴)
    // 사용 예시
    // 0번 Canvas에 인벤토리 슬롯 생성
    // var slot = UIManager.Instance.CreateUI(inventorySlotPrefab, 0);
    // 1번 Canvas에 채팅 메시지 생성
    // var chatMsg = UIManager.Instance.CreateUI(chatMessagePrefab, 1);
    public GameObject CreateUI(GameObject prefab, int rootIndex = 0)
    {
        if (uiRoots.Count == 0)
        {
            Debug.LogError("UI Roots가 설정되지 않았습니다!");
            return null;
        }

        if (rootIndex < 0 || rootIndex >= uiRoots.Count)
        {
            Debug.LogWarning("잘못된 rootIndex, 기본 0번 루트 사용");
            rootIndex = 0;
        }

        GameObject instance = Instantiate(prefab, uiRoots[rootIndex]);
        ApplyUIClickSettings(instance);

        return instance;
    }
}
