using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using MySecondWorld.Save;

namespace MySecondWorld.Network.OpenWorld
{
    public class OpenWorldNetworkManager : NetworkManager
    {
        [Scene]
        [Tooltip("재접속 세이브 데이터가 없을 때 기본으로 로드할 SubScene")]
        public string defaultSubScene;

        // 클라이언트별 현재 로드된 씬 목록 (connectionId → scene paths)
        private readonly Dictionary<int, HashSet<string>> _clientScenes = new();

        // LobbyScene -> MainScene  -> SubScene 한 단계 더 거치므로
        // OnStartServer() 대신 OnServerSceneChanged()
        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName == onlineScene)
                StartCoroutine(LoadDefaultSubScene());
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            Debug.Log($"[Client] 씬 변경 완료: {SceneManager.GetActiveScene().name}, 총 씬 수: {SceneManager.sceneCount}");
        }

        public override void OnStopServer()
        {
            _clientScenes.Clear();
            StartCoroutine(UnloadScenes());
        }

        public override void OnStopClient()
        {
            // Host는 클라이언트부터 종료하고 서버를 종료하기 때문에,
            // 이 시점에서는 아직 Offline이 아니라서 패스하고 OnStopServer로 넘어간다.
            if (mode == NetworkManagerMode.Offline)
                StartCoroutine(UnloadScenes());
        }

        // customHandling = true 메시지만 여기서 직접 처리.
        // Mirror 내부의 단일 loadingSceneAsync 필드가 여러 요청에 덮어써지는 충돌을 우회한다.
        // SceneManager.LoadSceneAsync 자체는 각 코루틴에서 그대로 호출한다.
        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            Debug.Log($"[Client] 씬 변경 시작: {newSceneName}, op={sceneOperation}, custom={customHandling}");

            if (customHandling)
            {
                if (sceneOperation == SceneOperation.LoadAdditive)
                    StartCoroutine(LoadAdditiveClient(newSceneName));
                else if (sceneOperation == SceneOperation.UnloadAdditive)
                    StartCoroutine(UnloadAdditiveClient(newSceneName));
                return;
            }

            base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
        }

        private IEnumerator LoadAdditiveClient(string sceneName)
        {
            //
            if (SceneManager.GetSceneByPath(sceneName).isLoaded ||
                SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(sceneName)).isLoaded)
            {
                // Path.GetFileNameWithoutExtension : 파일 경로(문자열)로부터 확장자를 제외한 순수 파일 이름만을 추출할 때 사용하는 메서드
                Debug.LogWarning($"[Client] 이미 로드됨 스킵: {sceneName}");
                yield break;
            }

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            Debug.Log($"[Client] Additive 로드 완료: {sceneName}, 총 씬 수: {SceneManager.sceneCount}");
        }

        private IEnumerator UnloadAdditiveClient(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByPath(sceneName);
            if (!scene.IsValid())
                scene = SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(sceneName));
            if (!scene.IsValid()) yield break;

            yield return SceneManager.UnloadSceneAsync(scene);
            Debug.Log($"[Client] Additive 언로드 완료: {sceneName}");
        }

        /// <summary>
        /// 플레이어 접속 시 세이브 데이터를 기반으로 SubScene SceneMessage 전송.
        /// 세이브 없으면 defaultSubScene 사용.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);

            string targetScene = defaultSubScene;
            // 세이브 데이터 가져오기 (패턴 매칭으로 null 체크와 할당을 동시에)
            if (SaveManager.Instance?.Load((uint)conn.connectionId) is { } saved)
            {
                if (saved.lastPosition != Vector3.zero && conn.identity != null)
                    conn.identity.transform.position = saved.lastPosition;

                if (saved.lastSceneNames is { Length: > 0 })
                {
                    foreach (var scene in saved.lastSceneNames)
                        SendSceneLoad(conn, scene);
                    return;
                }
            }
            if (string.IsNullOrEmpty(targetScene)) return;
            SendSceneLoad(conn, targetScene);
        }

        /// <summary>
        /// 클라이언트 연결 해제 시 현재 씬+위치 저장 및 추적 데이터 정리.
        /// </summary>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (SaveManager.Instance != null && conn.identity != null)
            {
                _clientScenes.TryGetValue(conn.connectionId, out var scenes);
                SaveManager.Instance.Save((uint)conn.connectionId, new SaveData
                {
                    lastSceneNames = scenes != null ? scenes.Where(s => s != defaultSubScene).ToArray() : System.Array.Empty<string>(),
                    lastPosition   = conn.identity.transform.position
                });
            }

            _clientScenes.Remove(conn.connectionId);
            base.OnServerDisconnect(conn);
        }

        // ── 씬 추적 퍼블릭 API (ZoneHandler에서 호출) ──────────────────

        /// <summary>특정 클라이언트에게 씬 로드 메시지를 보내고 추적에 등록.
        /// 이미 로드된 씬이면 무시.</summary>
        public void SendSceneLoad(NetworkConnectionToClient conn, string sceneName)
        {
            if (_clientScenes.TryGetValue(conn.connectionId, out var scenes) && scenes.Contains(sceneName))
            {
                Debug.LogWarning($"[SceneTrack] {sceneName} 이미 로드됨 → 스킵 (connId={conn.connectionId})");
                return;
            }

            conn.Send(new SceneMessage
            {
                sceneName = sceneName,
                sceneOperation = SceneOperation.LoadAdditive,
                customHandling = true
            });
            TrackLoad(conn, sceneName);
        }

        /// <summary>특정 클라이언트에게 씬 언로드 메시지를 보내고 추적에서 제거.</summary>
        public void SendSceneUnload(NetworkConnectionToClient conn, string sceneName)
        {
            conn.Send(new SceneMessage
            {
                sceneName = sceneName,
                sceneOperation = SceneOperation.UnloadAdditive,
                customHandling = true
            });
            TrackUnload(conn, sceneName);
        }

        /// <summary>
        /// 해당 클라이언트에 로드된 Dungeon/ 씬을 모두 언로드.
        /// 던전 출구 ZoneHandler에서 호출.
        /// </summary>
        public void UnloadAllDungeonsFor(NetworkConnectionToClient conn)
        {
            if (!_clientScenes.TryGetValue(conn.connectionId, out var scenes)) return;

            var toUnload = new List<string>();
            foreach (var scene in scenes)
            {
                if (scene.Contains("Dungeon/"))
                    toUnload.Add(scene);
            }

            foreach (var scene in toUnload)
            {
                conn.Send(new SceneMessage
                {
                    sceneName = scene,
                    sceneOperation = SceneOperation.UnloadAdditive,
                    customHandling = true
                });
                scenes.Remove(scene);
            }
        }

        // ── 내부 추적 헬퍼 ─────────────────────────────────────────────

        private void TrackLoad(NetworkConnectionToClient conn, string sceneName)
        {
            // _clientScenes 딕셔너리에 해당 키가 있냐 없냐를 체크
            if (!_clientScenes.TryGetValue(conn.connectionId, out var set))
            {
                //  List 대신 HashSet을 쓰는 이유는 SendSceneLoad의 중복 체크 때문
                set = new HashSet<string>();
                _clientScenes[conn.connectionId] = set;
            }
            set.Add(sceneName);
        }

        private void TrackUnload(NetworkConnectionToClient conn, string sceneName)
        {
            if (_clientScenes.TryGetValue(conn.connectionId, out var set))
                set.Remove(sceneName);
        }

        // ── 씬 로드/언로드 코루틴 ──────────────────────────────────────

        private IEnumerator LoadDefaultSubScene()
        {
            if (string.IsNullOrEmpty(defaultSubScene)) yield break;
            yield return SceneManager.LoadSceneAsync(defaultSubScene, LoadSceneMode.Additive);
        }

        private IEnumerator UnloadScenes()
        {
            // active scene(onlineScene)을 제외한 모든 additive 씬 언로드
            Scene activeScene = SceneManager.GetActiveScene();

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == activeScene) continue;
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            yield return Resources.UnloadUnusedAssets();
        }
    }
}
