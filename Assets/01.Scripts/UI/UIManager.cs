using System;
using System.Collections.Generic;
using UnityEngine;
using MessagePipe;
using VContainer;
using MySecondWorld.Network;
using MySecondWorld.Bundle;
using VContainer.Unity;

namespace MySecondWorld.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("상시 캔버스")]
        [SerializeField] private GameObject _menuCanvas;
        [SerializeField] private GameObject _loginCanvas;
        [SerializeField] private GameObject _chatCanvas;
        [SerializeField] private GameObject _joystickCanvas;
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
            SetLobbyState();
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

        private void ResetToLobbyState()
        {
            foreach (var kvp in _dynamicCanvasInstances)
            {
                if (kvp.Value != null)
                    kvp.Value.SetActive(false);
            }
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
