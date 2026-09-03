using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Mirror;
using MySecondWorld.Bundle;

namespace MySecondWorld.Network
{
    // 클라이언트 측 씬 로드/언로드 + ACK 전송 담당.
    public class ClientSceneSyncService
    {
        private int _expectedSceneLoads;  // 클라이언트: 로드해야 할 씬 수
        private int _completedSceneLoads; // 클라이언트: 로드 완료된 씬 수
        private string _primaryTargetScene = ""; // 현재 플레이어가 공식 진입한 메인 목적지 씬 (기본값: FieldScene)
        private readonly GameNetworkManager _manager;
        private readonly AddressableLabelLoader _zoneLoader;
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _sceneHandles = new();

        public ClientSceneSyncService(GameNetworkManager manager, AddressableLabelLoader zoneLoader)
        {
            _manager = manager;
            _zoneLoader = zoneLoader;
        }

        public void SetPrimaryTargetScene(string sceneName)
        {
            _primaryTargetScene = sceneName;
        }

        // [C2] 서버의 씬 로드/언로드 명령 수신 → 코루틴 분기
        public void OnAdditiveSceneMessage(AdditiveSceneMessage msg)
        {
            if (msg.load)
                LoadAdditiveScene(msg.sceneName).Forget();
            else
                UnloadAdditiveScene(msg.sceneName).Forget();
        }

        // 번들 로드:  PlayerEnteredZone(Zone 최초 진입 시 1회)
        // 씬 로드:    preloadMargin 안에 들어올 때마다(세밀한 단위)
        // 씬 언로드:  preloadMargin 벗어날 때마다
        // 번들 해제:  PlayerLeftZone(Zone 완전 이탈 시 1회)

        // [C2-a] 씬 로드 + 완료 카운터 증가
        private async UniTask LoadAdditiveScene(string sceneName)
        {
            Debug.Log($"[Client] LoadAdditiveScene: {sceneName}");

            // Host 모드: Addressables.LoadSceneAsync 호출하면 서버가 프리로드한 씬을 중복 로드하므로 스킵
            // (단, _completedSceneLoads++ 카운터와 ACK는 Host도 실행해야 함)
            // Cloud Client(순수 클라) 모드: 실제로 Addressables를 통해 씬을 Additive로 로드

            Scene loadedScene = default;
            if (!NetworkServer.active)
            {
                // 이미 로드된 씬은 스킵 (AdditiveSceneMessage 중복 수신 대비)
                if (!_sceneHandles.ContainsKey(sceneName))
                {
                    // Addressables 초기화 완료 대기 (이미 됐으면 즉시 반환)
                    await Addressables.InitializeAsync().ToUniTask();

                    var handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    _sceneHandles[sceneName] = handle;
                    var sceneInstance = await handle.ToUniTask();
                    loadedScene = sceneInstance.Scene;
                }
                else
                {
                    loadedScene = _sceneHandles[sceneName].Result.Scene;
                }
            }
            else
            {
                loadedScene = SceneManager.GetSceneByPath(sceneName);
            }
            // ─────────────────────────────────────────────────────────────
            // 1. 최초 진입 시에는 '첫 번째로 로드된 씬'을 목적지로 확정 잠금!
            // 2. 이후 주변에 프리로드되는 배경 씬들은 절대로 통과하지 못함!
            // ─────────────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(_primaryTargetScene))
            {
                _primaryTargetScene = sceneName; // 최초 씬(예: Entrance_Stairs 또는 FieldScene)으로 목적지 잠금!
                Debug.Log($"[Client] 최초 목적지 씬 잠금: {_primaryTargetScene}");
            }
            // 씬 이름(파일명)이 현재 잠긴 목적지 씬과 정확히 일치할 때만 ActiveScene 지정!
            string loadedSceneName = System.IO.Path.GetFileNameWithoutExtension(sceneName);
            string primaryName = System.IO.Path.GetFileNameWithoutExtension(_primaryTargetScene);
            if (loadedSceneName == primaryName && loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                Debug.Log($"[Client] 공식 목적지 씬 ActiveScene 지정: {loadedScene.name}");
                if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.gameObject != null)
                {
                    var playerGo = NetworkClient.localPlayer.gameObject;
                    if (playerGo.scene != loadedScene)
                    {
                        if (playerGo.transform.parent != null)
                            playerGo.transform.SetParent(null);
                        SceneManager.MoveGameObjectToScene(playerGo, loadedScene);
                    }
                }
            }
            else
            {
                Debug.Log($"[Client] 배경 프리로드 씬 로드 완료 (ActiveScene 유지): {loadedSceneName}");
            }

            _completedSceneLoads++; // ACK 판정용: 씬이 몇 개나 로드되었나?

            Debug.Log($"[Client] 씬 로드 완료: {sceneName}, completed={_completedSceneLoads}");
            NetworkClient.Send(new PortalSceneLoadedAckMessage { sceneName = sceneName }); // 포탈 전환 ack — 기존 스폰 카운터 로직과 별개로 항상 전송
            CheckAndSendSpawnAck(); // 초기 스폰용 ack 체크
        }

        // [C2-b] 씬 언로드
        private async UniTask UnloadAdditiveScene(string sceneName)
        {
            // Host 모드: 같은 프로세스라 언로드하면 서버 씬까지 날아가므로 스킵
            if (NetworkServer.active)
                return;

            // [안전장치] 만약 언로드하려는 씬이 현재 ActiveScene이라면,
            // 남아있는 다른 서브씬이나 MainScene으로 ActiveScene 복구
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                (activeScene.path == sceneName ||
                 activeScene.name == System.IO.Path.GetFileNameWithoutExtension(sceneName)))
            {
                var otherHandle = _sceneHandles.FirstOrDefault(kvp => kvp.Key != sceneName &&
                                                                    kvp.Value.IsValid() &&
                                                                    kvp.Value.Result.Scene.isLoaded);
                if (otherHandle.Value.IsValid())
                {
                    SceneManager.SetActiveScene(otherHandle.Value.Result.Scene);
                }
                else
                {
                    var mainScene = SceneManager.GetSceneAt(0);
                    if (mainScene.IsValid() && mainScene.isLoaded)
                        SceneManager.SetActiveScene(mainScene);
                }
            }

            // 씬 언로드 + 핸들 제거 먼저
            if (_sceneHandles.TryGetValue(sceneName, out var handle))
            {
                await Addressables.UnloadSceneAsync(handle).ToUniTask();
                _sceneHandles.Remove(sceneName);  // ← 여기서 제거해야 anyRemaining 체크에서 빠짐
            }

            // 1. 언로드된 씬의 에셋번들(ZoneAssetLabel) 찾기
            var label = _manager.zones.FirstOrDefault(z => z.sceneName == sceneName)?.ZoneAssetLabel?.labelString;
            // 2. 같은 label의 씬이 _sceneHandles에 아직 있는지 확인
            bool anyRemaining = _manager.zones
                .Where(z => z.ZoneAssetLabel?.labelString == label) // 같은 레이블인 것만 필터
                .Any(z => _sceneHandles.ContainsKey(z.sceneName));  // 그 중 _sceneHandles에 있는 게 하나라도 있나?
            // 3. 모두 언로드되었으면 에셋번들 메모리 해제
            // Yes : ReleaseLabel 호출 안 함 (아직 다른 씬 있음)
            // No  : ReleaseLabel 호출 (모두 언로드됨)
            if (!string.IsNullOrEmpty(label) && !anyRemaining)
                _zoneLoader.ReleaseLabel(label);
        }

        // [C3] 서버가 보낸 기대 씬 수 저장
        public void OnSpawnReadyMessage(SpawnReadyMessage msg)
        {
            Debug.Log($"[Client] OnSpawnReadyMessage sceneCount={msg.sceneCount}");
            _expectedSceneLoads = msg.sceneCount;
            CheckAndSendSpawnAck();
        }

        // [C4] 조건 충족 시 ACK 전송 → S5로 이어짐 (서버가 SpawnPlayer 진행)
        public void CheckAndSendSpawnAck()
        {
            Debug.Log($"[Client] CheckAndSendSpawnAck expected={_expectedSceneLoads}, completed={_completedSceneLoads}");
            if (_expectedSceneLoads > 0 && _completedSceneLoads >= _expectedSceneLoads)
            {
                NetworkClient.Send(new SceneLoadedAckMessage());
                _expectedSceneLoads = 0;
                _completedSceneLoads = 0;
            }
        }

        // [C5] 서버 지시로 화면 페이드 재생 (포탈 전환용)
        public void OnScreenFadeMessage(ScreenFadeMessage msg)
        {
            if (msg.fadeOut)
            {
                // ★ 포탈 이동 시작: 이전 목적지 잠금을 풀어서, 
                // 포탈 타고 도착할 새 씬(Entrance_Stairs 또는 FieldScene)이 새 목적지로 등록되게 함!
                _primaryTargetScene = null;
            }

            if (_manager.ScreenFadeUI == null)
            {
                Debug.LogWarning("[Client] ScreenFadeUI 미연결 — 페이드 스킵 (씬 배치 확인 필요)");
                return;
            }

            if (msg.fadeOut)
                _manager.ScreenFadeUI.FadeOut(msg.duration).Forget();
            else
                _manager.ScreenFadeUI.FadeIn(msg.duration).Forget();
        }

        public void OnPortalTransitionStarted(string targetSceneName)
        {
            _primaryTargetScene = targetSceneName; // 새 포탈 목적지 씬으로 교체
        }
    }
}