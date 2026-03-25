using UnityEngine;
using Mirror;

namespace MySecondWorld.Network.OpenWorld
{
    public class ZoneHandler : MonoBehaviour
    {
        [Scene]
        public string subScene;

        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            // 플레이어가 아닌 다른 콜라이더의 접근 무시
            Debug.Log($"[Zone] 진입: {other.name}, 태그: {other.tag}");
            if (!other.CompareTag("Player")) return;
            Debug.Log("[Zone] Player 확인됨");
            if (other.TryGetComponent<NetworkIdentity>(out NetworkIdentity networkIdentity))
            {
                Debug.Log("[Zone] NetworkIdentity 찾음 → SceneMessage 전송");
                SceneMessage sceneMessage = new SceneMessage
                {
                    sceneName = subScene,
                    sceneOperation = SceneOperation.LoadAdditive,
                    customHandling = false
                };
                /*false (기본값): Mirror가 자동으로 씬 로드/언로드 실행
                클라이언트 측에서 직접 핸들링
                NetworkClient.RegisterHandler<SceneMessage>(OnSceneMessage);
                void OnSceneMessage(SceneMessage msg)
                {
                    // 로딩 화면 띄우기
                    // 페이드 인/아웃 연출
                    // 그 다음에 SceneManager.LoadSceneAsync(...)
                }*/

                networkIdentity.connectionToClient.Send(sceneMessage);
            }
            else
            {
                Debug.Log("[Zone] NetworkIdentity 못 찾음!");
            }
        }

        [ServerCallback]
        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent(out NetworkIdentity networkIdentity))
            {
                SceneMessage sceneMessage = new SceneMessage
                {
                    sceneName = subScene,
                    sceneOperation = SceneOperation.UnloadAdditive,
                    customHandling = false
                };

                networkIdentity.connectionToClient.Send(sceneMessage);
            }
        }
    } 
}