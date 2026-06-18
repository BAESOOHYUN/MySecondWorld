using UnityEngine;
using Mirror;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;
using MySecondWorld.Save;
using MySecondWorld.Bundle;
using VContainer;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MySecondWorld.Customization;

namespace MySecondWorld.Network.Player
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private MovementController _movementController;
        [SerializeField] private ThirdPersonCameraController _cameraController;
        [SerializeField] private CharacterController _characterController;

        private CharacterSaveService _saveService;
        private NetworkCharacterAppearance _networkAppearance;
        private ZoneAddressableLoader _zoneLoader;

        private IPublisher<CharacterCustomizeOpenEvent> _openPublisher;

        [Inject]
        public void Construct(CharacterSaveService saveService,
                              NetworkCharacterAppearance networkAppearance,
                              ZoneAddressableLoader zoneLoader,
                              IPublisher<CharacterCustomizeOpenEvent> openPublisher)
        {
            _saveService = saveService;
            _networkAppearance = networkAppearance;
            _zoneLoader = zoneLoader;
            _openPublisher = openPublisher;
        }
        
        private void Awake()
        {
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false; 
            _characterController.enabled = false;

            InitializeAppearanceAsync().Forget();
        }

        public override void OnStartAuthority() // "이 캐릭터 네 꺼야" → 조종 활성화
        {
            _characterController.enabled = true;
            _movementController.enabled = true;
            _cameraController.enabled = true;
            _cameraController.ActiveCinemachine(true);
            _movementController.GetCameraYAngle = () => _cameraController.CameraYAngle; //카메라 각도 함수 주입

            InitializeAppearanceAsync().Forget();
        }

        public override void OnStopAuthority() // "더 이상 네 꺼 아니야" → 조종 비활성화
        {
            _movementController.GetCameraYAngle = null;
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false;
            _characterController.enabled = false;
        }

        private async UniTaskVoid InitializeAppearanceAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            // 1. character_parts 레이블의 720개 파츠 프리팹을 메모리에 올림
            await _zoneLoader.LoadZoneAsync("character_parts", ct);
            // 2. PlayerPrefs에서 JSON 꺼냄 → CharacterAppearanceData로 역직렬화
            var data = _saveService.Load();
            // 3. [Command] → 서버로 전송 _appearance = data
            _networkAppearance.CmdSetAppearance(data);
            // 4. [SyncVar hook] → 변경 감지 → OnAppearanceChanged 실행

            if (_saveService.IsFirstLogin())
                _openPublisher.Publish(new CharacterCustomizeOpenEvent());
        }
    }
}