using MySecondWorld.Network.Player;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MySecondWorld.Network
{
    // TODO : 차후 인터랙티브 아이템을 늘리며 확대 예정
    /// <summary>
    /// 플레이어가 월드 스크린에 다가가서 인터랙션하면 UI를 띄우는 컴포넌트
    /// </summary>
    public class SharedScreenInteraction : MonoBehaviour
    {
        [SerializeField] private GameObject _screenUI;
        private SharedScreenUI _screenUIComponent;
        private SharedScreenController _controller;
        [Inject] private IUIManager _uiManager;

        private void Awake()
        {
            _controller = GetComponent<SharedScreenController>();
            LifetimeScope.Find<ProjectLifetimeScope>().Container.Inject(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player))
            {
                if (_controller == null)
                {
                    Debug.LogError("SharedScreenController 컴포넌트가 없습니다.");
                    return;
                }

                if (_screenUI == null)
                    _screenUI = _uiManager.GetOrCreateDynamicCanvas<SharedScreenUI>();

                if (_screenUIComponent == null)
                    _screenUIComponent = _screenUI.GetComponent<SharedScreenUI>();

                if (_screenUIComponent != null)
                    _screenUIComponent.Init(_controller);

                _screenUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player))
            {
                if (_screenUI != null)
                    _screenUI.SetActive(false);
            }
        }
    }
}
