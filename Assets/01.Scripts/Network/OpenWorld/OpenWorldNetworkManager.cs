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

        [Scene]
        [Tooltip("서버 시작 시 미리 로드해둘 SubScene 목록 (세이브 데이터용 씬 등)")]
        public string[] preloadScenes = System.Array.Empty<string>();

        // 클라이언트별 현재 로드된 씬 목록 (connectionId → scene paths)
        private readonly Dictionary<int, HashSet<string>> _clientScenes = new();

        // 클라이언트 ACK를 기다리는 동안 저장해둘 스폰 위치 (connectionId → position)
        // Vector3.zero = 세이브 없음 (GetStartPosition 사용)
        private readonly Dictionary<int, Vector3> _pendingSpawnPositions = new();

        // 서버에서 주기적으로 갱신하는 "마지막 안전(착지) 위치"
        // 낙하 중에 접속을 끊어도 이 좌표를 저장 → 추락 위치가 세이브되지 않음
        private readonly Dictionary<int, Vector3> _lastSafePositions = new();

        // 서버가 onlineScene 전환 + preload를 완료했는지 나타내는 플래그
        private bool _defaultSubSceneLoaded = false;

        // ── 클라이언트 측: 씬 로드 완료 카운터 ──────────────────────────
        // SpawnReadyMessage로 받은 기대 개수에 도달하면 서버에 ACK 전송
        private int _expectedSceneLoads = 0;
        private int _completedSceneLoads = 0;

        // ── 서버/클라이언트 시작 ─────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.ReplaceHandler<SceneLoadedMessage>(OnSceneLoadedAck);
            StartCoroutine(TrackSafePositions());
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // 서버 → 클라이언트: 씬 개수 알림
            NetworkClient.ReplaceHandler<SpawnReadyMessage>(OnSpawnReady);
        }

        // LobbyScene -> MainScene -> SubScene 한 단계 더 거치므로
        // OnStartServer() 대신 OnServerSceneChanged()
        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName == onlineScene)
            {
                _defaultSubSceneLoaded = false;
                StartCoroutine(SetServerReady());
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            Debug.Log($"[Client] 씬 변경 완료: {SceneManager.GetActiveScene().name}, 총 씬 수: {SceneManager.sceneCount}");
        }

        public override void OnStopServer()
        {
            _defaultSubSceneLoaded = false;
            _clientScenes.Clear();
            _pendingSpawnPositions.Clear();
            _lastSafePositions.Clear();
            StartCoroutine(UnloadScenes());
        }

        public override void OnStopClient()
        {
            _expectedSceneLoads = 0;
            _completedSceneLoads = 0;
            // Host는 클라이언트부터 종료하고 서버를 종료하기 때문에,
            // 이 시점에서는 아직 Offline이 아니라서 패스하고 OnStopServer로 넘어간다.
            if (mode == NetworkManagerMode.Offline)
                StartCoroutine(UnloadScenes());
        }

        // ── 씬 변경 처리 (클라이언트) ────────────────────────────────────

        // customHandling = true 메시지만 여기서 직접 처리.
        // Mirror 내부의 단일 loadingSceneAsync 필드가 여러 요청에 덮어써지는 충돌을 우회한다.
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
            if (!SceneManager.GetSceneByPath(sceneName).isLoaded &&
                !SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(sceneName)).isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Debug.Log($"[Client] Additive 로드 완료: {sceneName}, 총 씬 수: {SceneManager.sceneCount}");
            }
            else
            {
                Debug.LogWarning($"[Client] 이미 로드됨 스킵: {sceneName}");
            }

            // 로드 완료(또는 이미 로드됨)를 카운트 → SpawnReadyMessage 기준 충족 시 ACK 전송
            _completedSceneLoads++;
            CheckAndSendSpawnAck();
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

        // ── 스폰 흐름 (서버) ─────────────────────────────────────────────

        /// <summary>
        /// base는 호출하지 않는다. 씬 로드 완료 ACK를 클라이언트에서 받은 뒤 스폰.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            StartCoroutine(PrepareAndRequestSpawn(conn));
        }

        private IEnumerator PrepareAndRequestSpawn(NetworkConnectionToClient conn)
        {
            while (!_defaultSubSceneLoaded)
                yield return null;

            // 로드할 씬 목록과 복구 위치 결정
            string[] scenesToLoad;
            Vector3 savedPosition = Vector3.zero;
            bool usingSaveData = false;

            if (SaveManager.Instance?.Load((uint)conn.connectionId) is { } saved &&
                saved.lastSceneNames is { Length: > 0 })
            {
                savedPosition = saved.lastPosition;
                scenesToLoad = saved.lastSceneNames;
                usingSaveData = true;
            }
            else
            {
                if (string.IsNullOrEmpty(defaultSubScene)) yield break;
                scenesToLoad = new[] { defaultSubScene };
            }

            // preloadScenes에 등록된 씬은 이미 로드됨 → 즉시 통과
            // 미등록 씬은 여기서 서버에 로드
            foreach (var scene in scenesToLoad)
                yield return EnsureSceneOnServer(scene);

            // 스폰 위치 저장 (ACK 핸들러에서 사용)
            _pendingSpawnPositions[conn.connectionId] = savedPosition;

            // 클라이언트에 씬 메시지 전송
            foreach (var scene in scenesToLoad)
                SendSceneLoad(conn, scene);

            // 세이브 데이터로 복구할 때는 defaultSubScene 클라이언트에서 불필요
            if (usingSaveData && !string.IsNullOrEmpty(defaultSubScene))
                SendSceneUnload(conn, defaultSubScene);

            // 클라이언트에게 "씬 N개 로드 완료 후 ACK 보내줘" 신호
            // 클라이언트가 ACK를 보내면 그때 base.OnServerAddPlayer(스폰) 실행
            conn.Send(new SpawnReadyMessage { sceneCount = scenesToLoad.Length });
        }

        /// <summary>
        /// 클라이언트가 모든 씬 로드를 완료하고 보낸 ACK.
        /// 이 시점에서 비로소 캐릭터를 스폰한다.
        /// </summary>
        private void OnSceneLoadedAck(NetworkConnectionToClient conn, SceneLoadedMessage msg)
        {
            if (!_pendingSpawnPositions.TryGetValue(conn.connectionId, out var savedPos))
            {
                Debug.LogWarning($"[Spawn] ACK 수신했지만 대기 중인 스폰 정보 없음 (connId={conn.connectionId})");
                return;
            }
            _pendingSpawnPositions.Remove(conn.connectionId);

            // 씬 로드 완료 후 스폰 → 클라이언트에 이미 지형이 있는 상태
            base.OnServerAddPlayer(conn);

            if (savedPos != Vector3.zero && conn.identity != null)
            {
                conn.identity.transform.position = GetSafeSpawnPosition(savedPos);
                // 텔레포트 직후 physics 엔진을 강제 동기화
                // → 스폰 위치가 Zone 콜라이더 안이어도 OnTriggerEnter가 즉시 발동
                Physics.SyncTransforms();
            }
        }

        // ── 스폰 흐름 (클라이언트) ──────────────────────────────────────

        /// <summary>서버로부터 "씬 N개 로드 완료 후 ACK 보내줘" 신호 수신</summary>
        private void OnSpawnReady(SpawnReadyMessage msg)
        {
            _expectedSceneLoads = msg.sceneCount;
            Debug.Log($"[Client] SpawnReady 수신 — 씬 {_expectedSceneLoads}개 대기 중 (현재 완료: {_completedSceneLoads})");
            CheckAndSendSpawnAck();
        }

        /// <summary>
        /// 완료된 씬 수가 기대치에 도달하면 서버에 ACK 전송.
        /// LoadAdditiveClient 완료 시와 OnSpawnReady 수신 시 양쪽에서 호출 — 어느 쪽이 늦어도 동작.
        /// </summary>
        private void CheckAndSendSpawnAck()
        {
            if (_expectedSceneLoads <= 0 || _completedSceneLoads < _expectedSceneLoads) return;

            _expectedSceneLoads = 0;
            _completedSceneLoads = 0;
            Debug.Log("[Client] 모든 씬 로드 완료 → 서버에 스폰 ACK 전송");
            NetworkClient.Send(new SceneLoadedMessage());
        }

        // ── 연결 해제 ────────────────────────────────────────────────────

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (SaveManager.Instance != null && conn.identity != null)
            {
                _clientScenes.TryGetValue(conn.connectionId, out var scenes);

                // 낙하 중 접속 끊김 방지: 마지막으로 땅에 서 있던 위치 사용.
                // 안전 위치 기록이 없으면(아직 한 번도 착지 못한 경우) 현재 위치 그대로.
                var posToSave = _lastSafePositions.TryGetValue(conn.connectionId, out var safePos)
                    ? safePos
                    : conn.identity.transform.position;

                SaveManager.Instance.Save((uint)conn.connectionId, new SaveData
                {
                    lastSceneNames = scenes != null ? scenes.Where(s => s != defaultSubScene).ToArray() : System.Array.Empty<string>(),
                    lastPosition   = posToSave
                });
            }

            _clientScenes.Remove(conn.connectionId);
            _pendingSpawnPositions.Remove(conn.connectionId);
            _lastSafePositions.Remove(conn.connectionId);
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
            if (!_clientScenes.TryGetValue(conn.connectionId, out var set))
            {
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

        // ── 안전 스폰 위치 ──────────────────────────────────────────────

        // 저장된 위치 아래에 지형이 없으면 낙하 상태 → NetworkStartPosition으로 복구.
        // EnsureSceneOnServer 완료 후 호출해야 Collider가 존재한다.
        private Vector3 GetSafeSpawnPosition(Vector3 savedPosition)
        {
            bool hasGround = Physics.Raycast(
                savedPosition + Vector3.up * 5f,
                Vector3.down,
                100f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (hasGround) return savedPosition;

            Debug.LogWarning($"[Spawn] 저장 위치({savedPosition}) 아래 지형 없음 → 기본 스폰 위치로 복구");
            Transform startPos = GetStartPosition();
            if (startPos == null)
                Debug.LogError("[Spawn] NetworkStartPosition 없음 — 씬에 배치 필요");
            return startPos != null ? startPos.position : Vector3.zero;
        }

        // ── 씬 로드/언로드 코루틴 ──────────────────────────────────────

        // 0.2초마다 모든 플레이어의 착지 여부를 체크해 안전 위치 갱신.
        // 발 아래 2f 이내에 지형이 있으면 "안전"으로 판정.
        private IEnumerator TrackSafePositions()
        {
            var wait = new WaitForSeconds(0.2f);
            while (NetworkServer.active)
            {
                foreach (var conn in NetworkServer.connections.Values)
                {
                    if (conn?.identity == null) continue;
                    var pos = conn.identity.transform.position;
                    if (Physics.Raycast(pos + Vector3.up, Vector3.down, 2f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        _lastSafePositions[conn.connectionId] = pos;
                }
                yield return wait;
            }
        }

        private IEnumerator SetServerReady()
        {
            // 서버 시작 시 자주 쓰는 씬을 미리 로드 → 플레이어 접속 시 EnsureSceneOnServer가 즉시 통과
            if (!string.IsNullOrEmpty(defaultSubScene))
                yield return EnsureSceneOnServer(defaultSubScene);
            foreach (var scene in preloadScenes)
                yield return EnsureSceneOnServer(scene);
            _defaultSubSceneLoaded = true;
        }

        // 씬이 서버에 아직 없으면 로드. 이미 있으면 즉시 통과.
        private IEnumerator EnsureSceneOnServer(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) yield break;
            string shortName = System.IO.Path.GetFileNameWithoutExtension(sceneName);
            if (SceneManager.GetSceneByPath(sceneName).isLoaded ||
                SceneManager.GetSceneByName(shortName).isLoaded)
                yield break;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
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
