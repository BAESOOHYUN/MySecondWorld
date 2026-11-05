using UnityEngine;

namespace MySecondWorld.Network
{
    // 바깥 존(OuterZone) — 진입/퇴장을 서버에 알려 파일 수신 대상(_outerObservers)으로 등록·해제한다. (안쪽 존: SharedScreenInteraction)
    public class SharedScreenOuterZone : MonoBehaviour
    {
        [SerializeField] private SharedScreenController _controller;

        private void Awake()
        {
            _controller = GetComponentInParent<SharedScreenController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Player.NetworkPlayerController player))
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
            if (other.TryGetComponent(out Player.NetworkPlayerController player))
                _controller.CmdExitOuterZone();
        }
    }
}