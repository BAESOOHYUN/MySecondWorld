using MySecondWorld.Network.Player;
using UnityEngine;

namespace MySecondWorld.Network
{
    // TODO : 차후 인터랙티브 아이템을 늘리며 확대 예정
    /// <summary>
    /// 플레이어가 월드 스크린에 다가가서 인터랙션하면 UI를 띄우는 컴포넌트
    /// </summary>
    public class SharedScreenInteraction : MonoBehaviour
    {
        [SerializeField] private GameObject _screenUI;     // Canvas UI 패널
        private SharedScreenUI _screenUIComponent;
        private SharedScreenController _controller;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player))
            {
                if (_screenUI == null)
                    _screenUI = UIManager.Instance.GetOrCreateDynamicCanvas<SharedScreenUI>();

                if (_screenUIComponent == null)
                    _screenUIComponent = _screenUI.GetComponent<SharedScreenUI>();

                if (_controller == null)
                    _controller = GetComponentInChildren<SharedScreenController>();
                
                if (_screenUIComponent != null)
                    _screenUIComponent.Init(_controller);

                _screenUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player))
            {
                // 발표자가 나가면 자동 해제
                if (_controller != null)
                    _controller.CmdReleasePresenter();

                if (_screenUI != null)
                    _screenUI.SetActive(false);
            }
        }
    }
}