using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        // [C2-a] 씬 로드 + 완료 카운터 증가
        // TODO: 데디케이티드 서버 전환 시 변경 필요
        private async UniTask LoadAdditiveScene(string sceneName)
        {
            Debug.Log($"[Client] LoadAdditiveScene: {sceneName}");
            // Host 방어: Host는 서버가 이미 프리로드했으므로 isLoaded = true → 스킵
            // 일반 클라이언트: 항상 isLoaded = false → 로드 실행
            if (!SceneManager.GetSceneByPath(sceneName).isLoaded)
                await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive).ToUniTask();

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

            if (SceneManager.GetSceneByPath(sceneName).isLoaded)
                await SceneManager.UnloadSceneAsync(sceneName).ToUniTask();

            var zone = _manager.zones.FirstOrDefault(z => z.name == sceneName);
            if (zone != null && !string.IsNullOrEmpty(zone.ZoneAssetLabel?.labelString))
                _zoneLoader.ReleaseZone(zone.ZoneAssetLabel.labelString);
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