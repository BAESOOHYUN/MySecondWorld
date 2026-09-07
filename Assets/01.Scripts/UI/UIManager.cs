using System;
using System.Collections.Generic;
using UnityEngine;
using MessagePipe;
using VContainer;
using MySecondWorld.Network;

namespace MySecondWorld.UI
{
    public class UIManager : MonoBehaviour, IUIManager
    {
        [Header("상시 캔버스")]
        [SerializeField] private GameObject _menuCanvas;
        [SerializeField] private GameObject _loginCanvas;
        [SerializeField] private GameObject _chatCanvas;
        [SerializeField] private GameObject _joystickCanvas;
        [SerializeField] private GameObject _combatHUDCanvas;
        [SerializeField] private GameObject _debugCanvas;
        [Header("동적 캔버스 프리팹")]
        [SerializeField] private List<GameObject> _dynamicCanvasPrefabs = new List<GameObject>();

        private readonly Dictionary<Type, GameObject> _dynamicCanvasInstances = new Dictionary<Type, GameObject>();
        private IDisposable _authSubscription;

        [Inject]
        private void Construct(
            ISubscriber<AuthStateChangedEvent> authSub,
            IObjectResolver container)
        {
            _authSubscription = authSub.Subscribe(OnAuthStateChanged);
        }

        private void Awake()
        {
            EnsureCanvasReferences();
            ResetToLobbyState();
        }

        private void EnsureCanvasReferences()
        {
            if (_combatHUDCanvas == null)
            {
                var t = transform.Find("CombatHUDCanvas");
                if (t != null) _combatHUDCanvas = t.gameObject;
            }
            if (_debugCanvas == null)
            {
                var t = transform.Find("DebugCanvas");
                if (t != null) _debugCanvas = t.gameObject;
            }
        }

        private void OnDestroy()
        {
            _authSubscription?.Dispose();
        }

        private void OnAuthStateChanged(AuthStateChangedEvent evt)
        {
            if (evt.IsAuthenticated) 
                SetGameState();
            else 
                ResetToLobbyState();
        }

        private void SetCanvasActive(GameObject go, bool active)
        {
            if (go == null) return;
            go.SetActive(active);
            if (active)
            {
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null && !canvas.enabled)
                {
                    canvas.enabled = true;
                }
            }
        }

        private void SetLobbyState()
        {
            SetCanvasActive(_loginCanvas, true);
            SetCanvasActive(_menuCanvas, false);
            SetCanvasActive(_chatCanvas, false);
            SetCanvasActive(_joystickCanvas, false);
            SetCanvasActive(_combatHUDCanvas, false);
            SetCanvasActive(_debugCanvas, false);
        }

        private void SetGameState()
        {
            SetCanvasActive(_loginCanvas, false);
            SetCanvasActive(_menuCanvas, true);
            SetCanvasActive(_chatCanvas, true);
            SetCanvasActive(_joystickCanvas, true);
            SetCanvasActive(_combatHUDCanvas, true);
            SetCanvasActive(_debugCanvas, true);
        }

        public void ResetToLobbyState()
        {
            // 1. 동적 생성된 캔버스 인스턴스(AI 다운로더, NPC 대화창 등) 정리
            foreach (var kvp in _dynamicCanvasInstances)
            {
                if (kvp.Value != null)
                    kvp.Value.SetActive(false);
            }

            // 2. UIManager 하위의 서브 캔버스(상점, NPC메뉴 등) 정리 및 Canvas.enabled 정상화
            foreach (Transform child in transform)
            {
                if (child.name.Contains("ScreenFade"))
                    continue;

                // ShopUI, NPCMenuUI는 자체적으로 Canvas.enabled를 제어하므로 Close() 호출
                var shop = child.GetComponent<ShopUI>();
                if (shop != null)
                {
                    shop.Close();
                    continue;
                }

                var npcMenu = child.GetComponent<NPCMenuUI>();
                if (npcMenu != null)
                {
                    npcMenu.Close();
                    continue;
                }

                // 일반 캔버스들은 Canvas 컴포넌트가 반드시 활성화(enabled=true) 상태여야
                // 차후 GameObject.SetActive(true) 시 정상 렌더링됨
                var canvas = child.GetComponent<Canvas>();
                if (canvas != null && !canvas.enabled)
                {
                    canvas.enabled = true;
                }

                if (child.gameObject == _loginCanvas)
                {
                    child.gameObject.SetActive(true);
                    continue;
                }

                // 상시 캔버스 외 서브 캔버스(인벤토리, 커스터마이징, 사망창 등) 비활성화
                if (child.gameObject != _menuCanvas &&
                    child.gameObject != _chatCanvas &&
                    child.gameObject != _joystickCanvas &&
                    child.gameObject != _combatHUDCanvas &&
                    child.gameObject != _debugCanvas)
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 3. 상시 캔버스 상태 확정 (LoginCanvas ON, 나머지 상시 캔버스 OFF)
            SetLobbyState();
        }

        public GameObject GetOrCreateDynamicCanvas<T>() where T : Component
        {
            var type = typeof(T);

            if (_dynamicCanvasInstances.TryGetValue(type, out var existing) && existing != null)
                return existing;

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
    }
}
