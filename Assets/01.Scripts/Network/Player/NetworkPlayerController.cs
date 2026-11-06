using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Mirror;
using MySecondWorld.Move;
using MySecondWorld.CameraSystem;
using MySecondWorld.Bundle;
using MySecondWorld.Customization;

namespace MySecondWorld.Network.Player
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private MovementController _movementController;
        [SerializeField] private ThirdPersonCameraController _cameraController;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Mirror.NetworkTransformBase _netTransform; // 구조 시 스냅샷 버퍼 리셋용

        private CharacterSaveService _saveService;
        private NetworkCharacterAppearance _networkAppearance;
        private AddressableLabelLoader _assetLoader;
        private GameNetworkManager _networkManager;

        [Inject]
        public void Construct(CharacterSaveService saveService,
                              NetworkCharacterAppearance networkAppearance,
                              AddressableLabelLoader assetLoader,
                              GameNetworkManager networkManager
            )
        {
            _saveService = saveService;
            _networkAppearance = networkAppearance;
            _assetLoader = assetLoader;
            _networkManager = networkManager;
        }
        
        private void Awake()
        {
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false; 
            _characterController.enabled = false;
            PreloadPartsAsync().Forget();
        }

        public override void OnStartAuthority() // "이 캐릭터 네 꺼야" → 조종 활성화
        {
            // 씬 로드 완료를 기다린 뒤 이동/중력 활성화 (아래 메서드)
            WaitForSceneThenEnableControl().Forget();
            // 소유자 클라 전용. Awake() 시점엔 권한 미확정이라 CmdSetAppearance가 씹힐 수 있어
            // 권한 확정 후 안전하게 재호출.
            PreloadPartsAsync().Forget();
        }

        public override void OnStopAuthority() // "더 이상 네 꺼 아니야" → 조종 비활성화
        {
            _movementController.GetCameraYAngle = null;
            _movementController.enabled = false;
            _cameraController.ActiveCinemachine(false);
            _cameraController.enabled = false;
            _characterController.enabled = false;
        }

        private async UniTaskVoid PreloadPartsAsync() => await _assetLoader.LoadLabelAsync("character_parts", this.GetCancellationTokenOnDestroy());
       
        // FieldScene(defaultSubScene)이 실제로 로드 완료될 때까지 이동/중력 활성화를 지연 —
        // 씬 로드보다 먼저 중력이 켜져서 지형 없는 허공으로 떨어지는 것을 방지
        private async UniTaskVoid WaitForSceneThenEnableControl()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                const float timeout = 10f;
                float elapsed = 0f;
                string targetScene = ResolveSpawnScene();
                // 타겟 씬(FieldScene 등)이 메모리에 로드될 때까지 대기
                while (!SceneManager.GetSceneByPath(targetScene).isLoaded && elapsed < timeout)
                {
                    await UniTask.Delay(100, cancellationToken: ct);
                    elapsed += 0.1f;
                }

                // 플레이어 자신이 서 있는 씬(Entrance_Stairs)으로만 1회 안전하게 쏙 들어감
                if (!NetworkServer.active)
                {
                    var subScene = SceneManager.GetSceneByPath(targetScene);
                    if (subScene.IsValid() && subScene.isLoaded && gameObject.scene != subScene)
                    {
                        if (transform.parent != null)
                            transform.SetParent(null);
                        SceneManager.MoveGameObjectToScene(gameObject, subScene);
                        Debug.Log($"[NetPlayerController] 클라이언트 플레이어 GameObject를 {subScene.name}으로 이동 완료");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return; // 오브젝트 파괴됨 — 정상 흐름
            }

            _characterController.enabled = true;
            _movementController.enabled = true;
            _cameraController.enabled = true;
            _cameraController.ActiveCinemachine(true);
            _movementController.GetCameraYAngle = () => _cameraController.CameraYAngle;// 카메라 각도 함수 주입
        }

        // 스폰 위치가 속한 존의 씬을 찾음 — 재접속 시 던전 등 필드가 아닌 곳에서 스폰될 수 있음
        private string ResolveSpawnScene()
        {
            // 신규 스폰의 흔한 케이스 우선: 필드씬 bounds에 들어가면 그대로 확정 —
            // 겹치는 던전 bounds 때문에 엉뚱한 씬을 고르는 것 방지
            var defaultZone = _networkManager.zones.FirstOrDefault(z => z.sceneName == _networkManager.defaultSubScene);
            if (defaultZone != null && defaultZone.bounds.Contains(transform.position))
                return _networkManager.defaultSubScene;

            foreach (var zone in _networkManager.zones)
            {
                if (zone.bounds.Contains(transform.position))
                    return zone.sceneName;
            }
            return _networkManager.defaultSubScene; // 못 찾으면 기본값 폴백
        }

        // 서버가 낙하 구조를 지시하는 진입점 (ServerZoneService.RecoverFromFall에서 호출)
        [Server]
        public void ServerRescueTo(Vector3 position)
        {
            ApplyRescue(position);

            // 보간 버퍼를 비워야 다른 클라가 "먼 거리를 미끄러져 이동"하는 것으로 보지 않는다
            if (_netTransform != null)
                _netTransform.ServerTeleport(position, transform.rotation);
            // 구조가 필요한 건 CharacterController를 켜고 있는 소유 클라 하나뿐.
            // ClientRpc로 보내면 같은 방의 다른 클라까지 받아서 버리게 된다.
            TargetRescueTo(position);
        }

        [TargetRpc]
        private void TargetRescueTo(Vector3 position)
        {
            if (!isOwned) return; // CharacterController가 켜져 있는 건 소유 클라뿐
            ApplyRescue(position);
        }

        // CharacterController는 내부에 자기 위치를 캐싱해서,
        // 켜진 채로 transform.position을 대입하면 되돌려버린다. 껐다 켜야 한다.
        // Host 모드에선 서버 사본이 곧 소유 클라라 켜져 있을 수 있으므로 상태를 가정하지 않고 보존한다.
        private void ApplyRescue(Vector3 position)
        {
            bool wasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            transform.position = position;
            Physics.SyncTransforms(); // PhysX 좌표 즉시 갱신
            _characterController.enabled = wasEnabled;

            // 위치만 옮기면 누적된 낙하 속도가 남아 다음 프레임에 지형을 뚫고 박힌다.
            if (_movementController != null)
                _movementController.ResetVerticalVelocity();
        }
    }
}