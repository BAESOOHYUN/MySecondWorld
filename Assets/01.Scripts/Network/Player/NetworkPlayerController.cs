using UnityEngine;
using Mirror;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;
using MySecondWorld.Save;
using MySecondWorld.Bundle;
using VContainer;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Network.Player
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private MovementController _movementController;
        [SerializeField] private ThirdPersonCameraController _cameraController;
        [SerializeField] private CharacterController _characterController;

        private CharacterSaveService _saveService;
        private NetworkCharacterAppearance _networkAppearance;
        private AddressableLabelLoader _zoneLoader;

        [Inject]
        public void Construct(CharacterSaveService saveService,
                              NetworkCharacterAppearance networkAppearance,
                              AddressableLabelLoader zoneLoader)
        {
            _saveService = saveService;
            _networkAppearance = networkAppearance;
            _zoneLoader = zoneLoader;
        }
        
        private void Awake()
        {
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false; 
            _characterController.enabled = false;

            // Mirror 스폰 시 보이는 모든 캐릭터(내 캐릭터 + 다른 플레이어 캐릭터 전부)에서 실행
            // 진짜 필요한 건 1번 줄 — character_parts 애셋 존을 로드하는 것. 이건 소유권과 무관하게,
            // 화면에 그 캐릭터를 렌더링하려면(SyncVar로 외형이 내려왔을 때 파츠를 인스턴스화하려면) 누구든 미리 로드해둬야 함
            InitializeAppearanceAsync().Forget();
        }

        public override void OnStartAuthority() // "이 캐릭터 네 꺼야" → 조종 활성화
        {
            _characterController.enabled = true;
            _movementController.enabled = true;
            _cameraController.enabled = true;
            _cameraController.ActiveCinemachine(true);
            _movementController.GetCameraYAngle = () => _cameraController.CameraYAngle; //카메라 각도 함수 주입

            // 오직 소유자 클라이언트에서만 실행
            // Mirror의 스폰 순서상 Awake()는 권한이 아직 부여되기 전에 먼저 실행될 수 있어서,
            // 그 시점엔 로컬 플레이어여도 CmdSetAppearance 전송이 타이밍상 씹힐 가능성이 있다.
            // OnStartAuthority()가 "권한 확정" 시점을 확실히 보장해주는 훅이라,
            // 여기서 한 번 더 호출해서 저장된 외형 데이터가 서버로 확실히 전달되게 하는 안전장치
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
            await _zoneLoader.LoadLabelAsync("character_parts", ct);
            // 2. PlayerPrefs에서 JSON 꺼냄 → CharacterAppearanceData로 역직렬화
            var data = _saveService.Load();
            // 3. [Command] → 서버로 전송 _appearance = data
            _networkAppearance.CmdSetAppearance(data);
            // 4. [SyncVar hook] → 변경 감지 → OnAppearanceChanged 실행
        }
    }
}