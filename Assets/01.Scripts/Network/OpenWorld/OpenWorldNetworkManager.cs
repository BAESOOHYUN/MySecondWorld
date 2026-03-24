using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.SceneManagement;

namespace MySecondWorld.Network.OpenWorld
{
    public class OpenWorldNetworkManager : NetworkManager
    {
        [Scene]
        [Tooltip("Add all sub-scenes to this list")]
        public string[] subScenes;

        // zonePrefab 인스턴스마다 SubScene은 따로 연결. 씬에 배치.
        public GameObject zonePrefab;

        //public override void OnStartServer()
        //{
        //    base.OnStartServer();
        //    StartCoroutine(LoadSubScenes());
        //    Instantiate(zonePrefab);
        //}

        // OnStartServer() 대신
        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName == onlineScene)
            {
                StartCoroutine(LoadSubScenes());
                Instantiate(zonePrefab);
            }
        }

        public override void OnStopServer()
        {
            StartCoroutine(UnloadScenes());
        }


        /*
        void StopHost()
        {
            StopClient();   // 1번 먼저
            StopServer();   // 2번 나중
        }
        
        Host 종료 흐름:

        StopHost() 호출
        → StopClient()
            → OnStopClient() 발생  ← 이 시점 서버 아직 살아있음
                mode == Offline?  → NO (서버가 살아있으니까)
                → UnloadScenes 스킵
        → StopServer()
            → OnStopServer() 발생
                → UnloadScenes() 실행 ✅
        */

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            Debug.Log($"[Client] 씬 변경 완료: {SceneManager.GetActiveScene().name}, 총 씬 수: {SceneManager.sceneCount}");
        }

        public override void OnStopClient()
        {
            // Host는 클라이언트부터 종료하고 서버를 종료하기 때문에,
            // 이 시점에서는 아직 Offline이 아니라서 패스하고 OnStopServer로 넘어간다.
            if (mode == NetworkManagerMode.Offline)
                StartCoroutine(UnloadScenes());
        }

        private IEnumerator LoadSubScenes()
        {
            foreach (string subSceneName in subScenes)
            {
                yield return SceneManager.LoadSceneAsync(subSceneName, LoadSceneMode.Additive);
            }
        }

        private IEnumerator UnloadScenes()
        {
            foreach (string subSceneName in subScenes)
            {
                // IsValid() "이 씬이 실제로 로드된 상태인가?"를 확인.
                // Unity에서 GetSceneByName()은 씬을 못 찾아도 예외를 던지지 않고 빈 Scene 구조체를 반환한다.
                // IsValid()가 false면 빈 구조체라는 뜻
                if (SceneManager.GetSceneByName(subSceneName).IsValid() || SceneManager.GetSceneByPath(subSceneName).IsValid())
                    yield return SceneManager.UnloadSceneAsync(subSceneName);
            }

            //씬을 언로드 해도 씬이 참조하던 텍스처/메쉬/오디오 같은 에셋은 메모리에남아 있다.
            //UnloadUnusedAssets()가 정리.
            yield return Resources.UnloadUnusedAssets();
        }
    }
}