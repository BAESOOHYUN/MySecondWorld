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
        private readonly GameNetworkManager _manager;
        private readonly ZoneAddressableLoader _zoneLoader;
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _sceneHandles = new();

        public ClientSceneSyncService(GameNetworkManager manager, ZoneAddressableLoader zoneLoader)
        {
            _manager = manager;
            _zoneLoader = zoneLoader;
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
        // TODO: 데디케이티드 서버 전환 시 변경 필요
        private async UniTask LoadAdditiveScene(string sceneName)
        {
            Debug.Log($"[Client] LoadAdditiveScene: {sceneName}");

            // Host 모드: Addressables.LoadSceneAsync 호출하면 서버가 프리로드한 씬을 중복 로드 → 스킵
            // 단, _completedSceneLoads++ 카운터와 ACK는 Host도 실행해야 함
            if (!NetworkServer.active)
            {
                // 이미 로드된 씬은 스킵 (AdditiveSceneMessage 중복 수신 대비)
                if (!_sceneHandles.ContainsKey(sceneName))
                {
                    // Addressables 초기화 완료 대기 (이미 됐으면 즉시 반환)
                    await Addressables.InitializeAsync().ToUniTask();

                    var handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    _sceneHandles[sceneName] = handle;
                    await handle.ToUniTask();
                }
            }
            _completedSceneLoads++; // ACK 판정용: 씬이 몇 개나 로드되었나?

            Debug.Log($"[Client] 씬 로드 완료: {sceneName}, completed={_completedSceneLoads}");
            CheckAndSendSpawnAck();
        }

        // [C2-b] 씬 언로드
        private async UniTask UnloadAdditiveScene(string sceneName)
        {
            // Host 모드: 같은 프로세스라서 언로드하면 서버 씬까지 날아감 → 스킵
            if (NetworkServer.active)
                return;

            // 씬 언로드 + 핸들 제거 먼저
            if (_sceneHandles.TryGetValue(sceneName, out var handle))
            {
                await Addressables.UnloadSceneAsync(handle).ToUniTask();
                _sceneHandles.Remove(sceneName);  // ← 여기서 제거해야 anyRemaining 체크에서 빠짐
            }

            // 1. 언로드 된 씬의 레이블 찾기
            var label = _manager.zones.FirstOrDefault(z => z.sceneName == sceneName)?.ZoneAssetLabel?.labelString;
            // 2. 같은 label의 씬이 _sceneHandles에 아직 있는지 확인
            bool anyRemaining = _manager.zones
                .Where(z => z.ZoneAssetLabel?.labelString == label) // 같은 레이블인 것만 필터
                .Any(z => _sceneHandles.ContainsKey(z.sceneName));  // 그 중 _sceneHandles에 있는 게 하나라도 있나?

            // 3 - Yes : ReleaseZone 호출 안 함 (아직 다른 씬 있음)
            // 3 - No  : ReleaseZone 호출 (모두 언로드됨)
            if (!string.IsNullOrEmpty(label) && !anyRemaining)
                _zoneLoader.ReleaseZone(label);
        }

        // [C3] 서버가 보낸 기대 씬 수 저장
        public void OnSpawnReadyMessage(SpawnReadyMessage msg)
        {
            Debug.Log($"[Client] OnSpawnReadyMessage sceneCount={msg.sceneCount}");
            _expectedSceneLoads = msg.sceneCount;
            CheckAndSendSpawnAck();
        }

        // [C4] 조건 충족 시 ACK 전송 → S5로 이어짐
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
    }
}