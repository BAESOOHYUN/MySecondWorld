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
using System;

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

        // 다른 어셈블리(Combat 등)가 구독할 수 있는 static 이벤트 선언
        public static event Action<Scene> OnDestinationSceneActivated;
        public static event Action<Scene> OnAdditiveSceneUnloading; // 언로드 직전 대피 신호

        // Domain Reload OFF — static 이벤트는 Play 세션을 넘어 구독자가 그대로 남는다.
        // 이전 세션에서 파괴된 인스턴스가 계속 구독돼 있으면, 다음 씬 로드 시점에
        // 죽은 인스턴스를 호출하다 예외가 터져서 그 아래 ACK 전송 코드까지 통째로 스킵된다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnDestinationSceneActivated = null;
            OnAdditiveSceneUnloading = null;
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
            // 1. 최초 진입 시에는 '첫 번째로 로드된 씬'을 공식 목적지로 확정 잠금
            // 2. 이후 주변에 프리로드되는 배경 씬들은 절대로 통과하지 못함
            // ─────────────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(_primaryTargetScene))
            {
                _primaryTargetScene = sceneName; // 최초 씬(예: Entrance_Stairs 또는 FieldScene)으로 목적지 잠금
                Debug.Log($"[Client] 최초 목적지 씬 잠금: {_primaryTargetScene}");
            }
            // 씬 이름(파일명)이 현재 잠긴 목적지 씬과 정확히 일치할 때만 ActiveScene 지정
            string loadedSceneName = System.IO.Path.GetFileNameWithoutExtension(sceneName);
            string primaryName = System.IO.Path.GetFileNameWithoutExtension(_primaryTargetScene);
            if (loadedSceneName == primaryName && loadedScene.IsValid() && loadedScene.isLoaded)
                ActivateDestinationScene(loadedScene);
            else
                Debug.Log($"[Client] 배경 프리로드 씬 로드 완료 (ActiveScene 유지): {loadedSceneName}");

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
            var mainScene = SceneManager.GetSceneAt(0);

            if (activeScene.IsValid() &&
                (activeScene.path == sceneName ||
                 activeScene.name == System.IO.Path.GetFileNameWithoutExtension(sceneName)))
            {
                var otherHandle = _sceneHandles.FirstOrDefault(kvp => kvp.Key != sceneName &&
                                                                    kvp.Value.IsValid() &&
                                                                    kvp.Value.IsDone &&
                                                                    kvp.Value.Result.Scene.isLoaded);
                if (otherHandle.Value.IsValid())
                    SceneManager.SetActiveScene(otherHandle.Value.Result.Scene);
                else if (mainScene.IsValid() && mainScene.isLoaded)
                        SceneManager.SetActiveScene(mainScene);
            }

            // 씬 언로드 + 핸들 제거 먼저
            if (_sceneHandles.TryGetValue(sceneName, out var handle))
            {
                // [대피 가드] [안전 대피 1] 플레이어가 언로드 대상 씬에 남아있으면 MainScene으로 피신
                try
                {
                    // [안전 대피 1] 플레이어가 언로드 대상 씬에 남아있으면 MainScene으로 피신
                    // Addressables 씬은 .path가 비어있을 수 있으므로 Scene 구조체 직접 비교
                    Scene unloadingScene = handle.Result.Scene;
                    if (unloadingScene.IsValid() && unloadingScene.isLoaded)
                    {
                        bool canEvacuate = mainScene.IsValid() && mainScene.isLoaded && mainScene != unloadingScene;
                        bool playerInScene = NetworkClient.localPlayer != null && NetworkClient.localPlayer.gameObject != null
                                                && NetworkClient.localPlayer.gameObject.scene == unloadingScene;
                        {
                            if (playerInScene && canEvacuate)
                            {
                                var playerGo = NetworkClient.localPlayer.gameObject;
                                if (playerGo.transform.parent != null)
                                    playerGo.transform.SetParent(null);
                                SceneManager.MoveGameObjectToScene(playerGo, mainScene);
                                Debug.LogWarning($"[Client] '{sceneName}' 언로드 직전 로컬 플레이어를 MainScene으로 긴급 대피 — 정상 경로였다면 이미 새 씬으로 옮겨져 있어야 함");
                            }
                            else if (playerInScene)
                            {
                                Debug.LogError($"[Client] '{sceneName}' 언로드 직전 대피 실패 — 안전한 영구 씬을 찾지 못함");
                            }
                            
                            // [안전 대피 3] Mirror 스폰 오브젝트(몬스터·타 플레이어)도 같이 대피시킨다.
                            // 지금까지 플레이어/컴패니언에만 대피 코드가 있어서 몬스터만 씬과 함께 파괴됐다.
                            if (canEvacuate)
                                RelocateSpawnedObjects(mainScene, unloadingScene);

                            // [안전 대피 2] 컴패니언에게 씬 언로드 전 대피 알림 발행
                            OnAdditiveSceneUnloading?.Invoke(unloadingScene);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Client] '{sceneName}' 대피 가드 처리 중 예외 — 언로드는 계속 진행: {e}");
                }

                // 실제 언로드는 대피 가드와 완전히 별개의 try/catch/finally.
                // 대피 가드가 어떻게 실패하든 이 블록은 반드시 실행되고, 장부 정리도 반드시 보장된다.
                try
                {
                    await Addressables.UnloadSceneAsync(handle).ToUniTask();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Client] '{sceneName}' Addressables 언로드 실패: {e}");
                }
                finally
                {
                    _sceneHandles.Remove(sceneName); // 성공/실패 무관 무조건 정리 — 핸들 누수 방지
                }
            }

            //  라벨 해제
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


        // [C2-c] 서버가 확정한 소속 씬 통보 수신.
        // 서브씬 사이를 걸어서 이동할 때 클라의 ActiveScene을 서버에 맞추는 유일한 경로다.
        // 기존에는 목적지가 "최초 1회"와 "포탈 전환" 때만 정해져서, 던전 안을 걸어다니면
        // ActiveScene이 처음 밟은 씬에 잠긴 채 남았다.
        public void OnPlayerSceneChangedMessage(PlayerSceneChangedMessage msg)
        {
            _primaryTargetScene = msg.sceneName;

            // 이미 로드돼 있으면 즉시 적용. 아직이면 LoadAdditiveScene이 완료 시점에 적용한다.
            Scene scene = FindLoadedScene(msg.sceneName);
            if (scene.IsValid() && scene.isLoaded)
                ActivateDestinationScene(scene);
            else
                Debug.Log($"[Client] 소속 씬 통보 '{msg.sceneName}' — 아직 미로드, 로드 완료 시 적용된다");
        }

        // 로드된 씬 조회. Addressables 씬은 scene.path가 비어 있어 GetSceneByPath로 못 찾으므로
        // 핸들을 먼저 보고, 없으면 파일명으로 대조한다.
        private Scene FindLoadedScene(string scenePath)
        {
            if (_sceneHandles.TryGetValue(scenePath, out var handle)
                && handle.IsValid() && handle.IsDone)
                return handle.Result.Scene;

            string targetName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == targetName) return s;
            }
            return default;
        }

        // 목적지 씬 확정 시 공통 처리 — ActiveScene 지정 + 캐릭터/스폰 오브젝트 이사 + 외부 알림.
        // LoadAdditiveScene(로드가 나중에 끝남)과 OnPlayerSceneChangedMessage(이미 로드됨) 양쪽에서 부른다.
        private void ActivateDestinationScene(Scene scene)
        {
            SceneManager.SetActiveScene(scene);
            Debug.Log($"[Client] 공식 목적지 씬 ActiveScene 지정: {scene.name}");

            // 1. 로컬 플레이어 씬 이동
            if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.gameObject != null)
            {
                var playerGo = NetworkClient.localPlayer.gameObject;
                if (playerGo.scene != scene)
                {
                    if (playerGo.transform.parent != null)
                        playerGo.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(playerGo, scene);
                }
            }

            // 2. Mirror 스폰 오브젝트(몬스터·타 플레이어)도 같은 씬으로 모은다
            RelocateSpawnedObjects(scene, null);

            // 3. 컴패니언 등 외부 시스템에 "목적지 씬 전환 완료" 알림
            OnDestinationSceneActivated?.Invoke(scene);
        }

        /// <summary>
        /// Mirror가 스폰한 동적 오브젝트를 target 씬으로 옮긴다.
        ///
        /// Mirror의 클라 스폰은 그냥 Instantiate라 항상 그 시점의 ActiveScene에 들어간다.
        /// 그 씬이 언로드되면 오브젝트가 통째로 파괴되는데, 서버는 옵저버 목록이 그대로라
        /// 스폰 메시지를 다시 보내주지 않는다 → "서버엔 살아서 때리는데 화면엔 없는" 유령이 된다.
        ///
        /// sourceFilter를 주면 그 씬에 있는 것만 옮긴다(언로드 직전 대피용).
        /// sceneId != 0인 씬 배치 오브젝트는 원래 자기 씬에 있어야 하므로 건드리지 않는다.
        /// </summary>
        private static void RelocateSpawnedObjects(Scene target, Scene? sourceFilter)
        {
            if (!target.IsValid() || !target.isLoaded) return;

            foreach (var identity in NetworkClient.spawned.Values)
            {
                if (identity == null) continue;
                if (identity.sceneId != 0) continue;          // 씬 배치 오브젝트는 제자리에
                GameObject go = identity.gameObject;
                // MoveGameObjectToScene은 루트 오브젝트만 받는다. 자식은 부모가 옮겨질 때 따라가므로 건너뛰어도 된다
                if (go.transform.parent != null) continue;
                if (go.scene == target) continue;
                if (sourceFilter.HasValue && go.scene != sourceFilter.Value) continue;

                SceneManager.MoveGameObjectToScene(go, target);
            }
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
                // 포탈 이동 시작: 이전 목적지 잠금을 풀어서,
                // 포탈 타고 도착할 새 씬(Entrance_Stairs 또는 FieldScene)이 새 목적지로 등록되게 함
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

        public async UniTask UnloadAllScenesAsync()
        {
            var handles = _sceneHandles.Values.ToList();
            _sceneHandles.Clear();

            foreach (var handle in handles)
            {
                if (handle.IsValid())
                {
                    try
                    {
                        await Addressables.UnloadSceneAsync(handle, true).ToUniTask();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Client] UnloadSceneAsync 예외 (무시): {e.Message}");
                    }
                }
            }

            _primaryTargetScene = "";
            _expectedSceneLoads = 0;
            _completedSceneLoads = 0;
        }
    }
}