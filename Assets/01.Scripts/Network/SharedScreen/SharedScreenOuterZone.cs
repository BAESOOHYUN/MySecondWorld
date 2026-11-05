using UnityEngine;
using MySecondWorld.Network.Player;
using MySecondWorld.Utils;

namespace MySecondWorld.Network
{
    public class SharedScreenOuterZone : MonoBehaviour
    {
        [SerializeField] private SharedScreenController _controller;

        private void Awake()
        {
            _controller = GetComponentInParent<SharedScreenController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player) && player.isLocalPlayer)
            {
                if (_controller == null)
                {
                    Debug.LogError("_controller( SharedScreenController) 컴포넌트가 없습니다.");
                    return;
                }

                _controller.CmdEnterOuterZone();
            }
        } 
        
        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out NetworkPlayerController player) && player.isLocalPlayer)
                _controller.CmdExitOuterZone();
        }

        private void OnDrawGizmos() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, Color.yellow, 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, Color.yellow, 0.35f);
    }
}