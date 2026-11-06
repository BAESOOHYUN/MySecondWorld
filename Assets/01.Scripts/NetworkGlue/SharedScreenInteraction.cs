using UnityEngine;
using VContainer;
using VContainer.Unity;
using MySecondWorld.Network.Player;
using MySecondWorld.Utils;

namespace MySecondWorld.Network
{
    // TODO : 차후 인터랙티브 아이템을 늘리며 확대 예정
    /// <summary>
    /// 플레이어가 월드 스크린에 다가가서 인터랙션하면 UI를 띄우는 컴포넌트
    /// 안쪽 존(InnerZone) 역할 — 클라 전용으로, 네트워크 호출 없이 UI만 켜고 끈다. (바깥 존: SharedScreenOuterZone)
    /// </summary>
    public class SharedScreenInteraction : MonoBehaviour
    {
        [SerializeField] private GameObject _screenUI;
        private bool _inside;
        private SharedScreenUI _screenUIComponent;
        private SharedScreenController _controller;
        [Inject] private IUIManager _uiManager;

        private void Awake()
        {
            _controller = GetComponent<SharedScreenController>();
            LifetimeScope.Find<ProjectLifetimeScope>().Container.Inject(this);
        }
        private void OnEnable() => NetworkPlayerController.LocalTeleported += ForceExit;
        private void OnDisable() => NetworkPlayerController.LocalTeleported -= ForceExit;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player) && player.isLocalPlayer)
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

                _inside = true;
                _screenUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player) && player.isLocalPlayer)
            {
                _inside = false;
                if (_screenUI != null)
                    _screenUI.SetActive(false);
            }
        }

        private void OnDrawGizmos() => GizmoUtils.DrawCollider(GetComponent<BoxCollider>(), transform, Color.cyan, 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<BoxCollider>(), transform, Color.cyan, 0.35f);

        // Trigger 존 안에서 바로 순간이동으로 다른 장소로 간 사람들의 UI가 꺼지지 않고 켜진 경우 방지
        private void ForceExit()
        {
            if(!_inside) return;    // 이 존에 없던 사람은 무시

            _inside = false;        // 이제 존 밖으로 나왔음
            if (_screenUI != null) 
                _screenUI.SetActive(false);
        }
    }
}
