using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

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
