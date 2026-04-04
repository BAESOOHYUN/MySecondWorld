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
            if (other.TryGetComponent(out NetworkPlayerController component))
                _isPlayerNear = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController component))
            _isPlayerNear = false;
        }

        private void Update()
        {
            if (_isPlayerNear && Input.GetKeyDown(_interactKey))
                    _screenUI.SetActive(!_screenUI.activeSelf);
        }
    }
}