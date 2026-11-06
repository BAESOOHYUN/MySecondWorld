using UnityEngine;
using MySecondWorld.Network.Player;
using MySecondWorld.Utils;

namespace MySecondWorld.Network
{
    public class SharedScreenOuterZone : MonoBehaviour
    {
        [SerializeField] private SharedScreenController _controller;
        private bool _inside;

        private void Awake()
        {
            _controller = GetComponentInParent<SharedScreenController>();
        }
        private void OnEnable() => NetworkPlayerController.LocalTeleported += ForceExit;
        private void OnDisable() => NetworkPlayerController.LocalTeleported -= ForceExit;


        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player) && player.isLocalPlayer)
            {
                if (_controller == null)
                {
                    Debug.LogError("_controller( SharedScreenController) 컴포넌트가 없습니다.");
                    return;
                }

                _inside = true;
                _controller.CmdEnterOuterZone();
            }
        } 
        
        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player) && player.isLocalPlayer)
            {
                _inside = false;
                _controller.CmdExitOuterZone();
            }
        }

        private void OnDrawGizmos() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, Color.yellow, 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, Color.yellow, 0.35f);

        // Trigger 존 안에서 바로 순간이동으로 다른 장소로 간 사람들의 UI가 꺼지지 않고 켜진 경우 방지
        private void ForceExit()
        {
            if (!_inside) return;    // 이 존에 없던 사람은 무시

            _inside = false;        // 이제 존 밖으로 나왔음
            _controller.CmdExitOuterZone();
        }
    }
}