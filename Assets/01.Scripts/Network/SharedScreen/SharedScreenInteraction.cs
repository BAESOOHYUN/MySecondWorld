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
        [SerializeField] private KeyCode _interactKey = KeyCode.A; // TODO : 인풋시스템에서 액션키 연결
        private bool _isPlayerNear;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player))
            {
                _isPlayerNear = true;

                if (_screenUI == null)
                {
                    var ui = FindObjectOfType<SharedScreenUI>(true);
                    if (ui != null)
                        _screenUI = ui.gameObject;
                }

                var controller = GetComponentInChildren<SharedScreenController>();
                _screenUI.GetComponent<SharedScreenUI>().Init(controller);
                _screenUI.SetActive(true);
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player))
            {
                _isPlayerNear = false;

                // 발표자가 나가면 자동 해제
                var controller = GetComponentInChildren<SharedScreenController>();
                controller.CmdReleasePresenter();

                if (_screenUI != null)
                    _screenUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isPlayerNear && Input.GetKeyDown(_interactKey))
                    _screenUI.SetActive(!_screenUI.activeSelf);
        }
    }
}