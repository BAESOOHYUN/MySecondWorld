using UnityEngine;

namespace MySecondWorld.Network
{
    public class SharedScreenOuterZone : MonoBehaviour
    {
        [SerializeField] private SharedScreenController _controller;

        private void Awake()
        {
            _controller = GetComponent<SharedScreenController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Player.NetworkPlayerController player))
            {
                if (_controller == null)
                {
                    Debug.LogError("SharedScreenController 컴포넌트가 없습니다.");
                    return;
                }

                _controller.CmdEnterOuterZone();
            }
        } 
        
        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Player.NetworkPlayerController player))
                _controller.CmdExitOuterZone();
        }
    }
}