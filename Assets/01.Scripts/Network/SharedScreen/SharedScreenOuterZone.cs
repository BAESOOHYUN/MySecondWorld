using UnityEngine;
using MySecondWorld.Network.Player;

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
    }
}