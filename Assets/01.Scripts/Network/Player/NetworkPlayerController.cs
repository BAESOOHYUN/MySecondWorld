using Mirror;
using MySecondWorld.Move;
using UnityEngine;

namespace MySecondWorld.Network.Player
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private MovementController _movementController;
        [SerializeField] private ThirdPersonCameraController _cameraController;

        private void Awake()
        {
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false;
        }

        public override void OnStartAuthority() // "이 오브젝트의 조종권을 얻었을 때"
        {
            _movementController.enabled = true;
            _cameraController.enabled = true;
            _cameraController.ActiveCinemachine(true);
            _movementController.GetCameraYAngle = () => _cameraController.CameraYAngle;
        }

        public override void OnStopAuthority() // "이 오브젝트의 조종권을 잃었을 때"
        {
            _movementController.GetCameraYAngle = null;
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false;
        }

        public override void OnStartLocalPlayer() // "이 오브젝트가 내 플레이어 캐릭터"일 때 딱 한 번
        {
        }
    }
}