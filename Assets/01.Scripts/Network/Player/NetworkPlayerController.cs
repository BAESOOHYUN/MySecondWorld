using UnityEngine;
using Mirror;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;

namespace MySecondWorld.Network.Player
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private MovementController _movementController;
        [SerializeField] private ThirdPersonCameraController _cameraController;
        [SerializeField] private CharacterController _characterController;

        private void Awake()
        {
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false; 
            _characterController.enabled = false;
        }

        public override void OnStartAuthority() // "이 캐릭터 네 꺼야" → 조종 활성화
        {
            _characterController.enabled = true;
            _movementController.enabled = true;
            _cameraController.enabled = true;
            _cameraController.ActiveCinemachine(true);
            _movementController.GetCameraYAngle = () => _cameraController.CameraYAngle; //카메라 각도 함수 주입
        }

        public override void OnStopAuthority() // "더 이상 네 꺼 아니야" → 조종 비활성화
        {
            _movementController.GetCameraYAngle = null;
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false;
            _characterController.enabled = false;
        }
    }
}