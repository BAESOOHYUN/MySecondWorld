using System.Collections.Generic;
using UnityEngine;
using MySecondWorld.Network;

namespace MySecondWorld.UI
{
    public class UIManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetSingleton()
        {
            Instance = null; // Domain Reload OFF 환경에서도 매번 초기화
        }
        public static UIManager Instance { get; private set; }

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
    }
}