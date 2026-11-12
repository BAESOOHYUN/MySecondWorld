using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Cleverous.VaultInventory;
using Cleverous.VaultSystem;
using MySecondWorld.Save;
using MySecondWorld.Combat.Companion;
using MySecondWorld.Customization;
using MySecondWorld.Network.Companion;

namespace MySecondWorld.Network
{
    using Inventory = Cleverous.VaultInventory.Inventory;

    // 플레이어 세션 소유자 (서버 전용).
    //  - 접속 흐름 : 세션 생성 → 번들·씬 로드 지시 → 캐릭터 스폰 → 퇴장 시 저장·정리
    //  - 컴패니언  : 영입, 접속 시 재소환
    //  - 저장·복원 : 인벤토리·외형·도감·장착 칸·위치·컴패니언 상태를 세이브로 만들고 되살림
    //  - 테스트/디버그 : 시작 골드·테스트 킷 지급
    public class ServerSessionService
    {
        #region 필드
        // ── 서비스·표 ──
        private readonly GameNetworkManager _manager;
        private readonly DungeonRoomRegistry _roomRegistry;
        private readonly SaveManager _saveManager;
        private readonly AvatarPartKeyTableSO _avatarPartKeyTable;

        // ── NPC 기억 (connectionId → npcKey → 그 NPC의 기억[요약 필드 + 대화 기록]) ──
        // 대화 기록은 Append로만, 요약 필드는 Summary로만 바뀐다(서로 상대 필드를 건드리지 않음)
        private const int MaxNpcPairs = 10;
        private const int MaxNpcKeys = 64; // 한 플레이어가 가질 수 있는 서로 다른 NPC 수 상한 (조작된 클라가 키를 무한히 만들어 서버 메모리를 채우는 것 방지)
        private const int MaxNpcFacts = 50; // NPC가 기억하는 유저에 대한 사실들
        private const int MaxNpcTextLength = 500;       // 대사·요약 문자열 하나의 최대 길이(넘으면 ClampNpcText가 잘라냄)
        private const float MinAppendInterval = 0.1f;   // 플레이어당 업로드 최소 간격(연타 방지)
        // 접속(connectionId)마다 하나씩 들고 있는 캐시. 저장 때 BuildNpcMemorySave가 이 값을 세이브로 내보내고, 접속이 끊기면 RemovePlayer에서 지운다
        private readonly Dictionary<int, Dictionary<string, NpcMemorySave>> _npcMemories = new();
        // 접속별 마지막 Append 수신 시각 — 연타 제한(MinAppendInterval) 비교용
        private readonly Dictionary<int, float> _lastNpcAppendTime = new();
        private readonly Dictionary<int, float> _lastNpcSummaryTime = new();   // 위 _lastNpcAppendTime의 Append와 따로 센다(같은 프레임에 연달아 오기 때문)

        // ── 시작 골드 ──
        private readonly RootItem _startingGoldItem;
        private readonly int _startingGoldAmount;

        // ── 테스트 킷 아이템 (인게임 획득 경로가 생기면 삭제) ──
        private readonly RootItem _testSwordItem;
        private readonly RootItem _testRingItem;
        private readonly RootItem _testBowItem;
        private readonly RootItem _testQuiverItem;
        private readonly RootItem _testBraceletItem;
        private readonly RootItem _testNecklaceItem;
        private readonly RootItem _testAxeItem;
        private readonly RootItem _testPolearmItem;

        // ── 세션 장부 ──
        public Dictionary<int, PlayerSession> Sessions { get; } = new();
        #endregion

        #region 생성자
        public ServerSessionService(
           GameNetworkManager manager,
           DungeonRoomRegistry roomRegistry,
           SaveManager saveManager,
           RootItem startingGoldItem,
           int startingGoldAmount,
           RootItem testSwordItem,
           RootItem testRingItem,
           RootItem testBowItem,
           RootItem testQuiverItem,
           RootItem testBraceletItem,
           RootItem testNecklaceItem,
           RootItem testAxeItem,
           RootItem testPolearmItem,
           AvatarPartKeyTableSO avatarPartKeyTable
           )
        {
            _manager = manager;
            _roomRegistry = roomRegistry;
            _saveManager = saveManager ?? new SaveManager();
            _avatarPartKeyTable = avatarPartKeyTable;
            _startingGoldItem = startingGoldItem;
            _startingGoldAmount = startingGoldAmount;
            _testSwordItem = testSwordItem;
            _testRingItem = testRingItem;
            _testBowItem = testBowItem;
            _testQuiverItem = testQuiverItem;
            _testBraceletItem = testBraceletItem;
            _testNecklaceItem = testNecklaceItem;
            _testAxeItem = testAxeItem;
            _testPolearmItem = testPolearmItem;
        }
        #endregion

        #region 접속 흐름

        // [S3] 세션 생성 + PrepareAndSpawn 시작
        // Mirror 기본: base.OnServerAddPlayer(conn)에서 바로 캐릭터를 만들지만,
        // 씬이 먼저 로드된 후에 스폰해야 하므로 base를 부르지 않는다.
        public void AddPlayer(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Session] AddPlayer conn={conn.connectionId}");
            // 같은 playerId로 이미 세션 존재 시 → 기존 세션 정리 (재접속 처리)
            string playerId = conn.authenticationData as string;
            if (!string.IsNullOrEmpty(playerId))
            {
                var stale = Sessions.FirstOrDefault(s => s.Value.playerId == playerId);
                if (stale.Value != null)
                {
                    if (NetworkServer.connections.TryGetValue(stale.Key, out var oldConn))
                        oldConn.Disconnect();
                    Sessions.Remove(stale.Key);
                }
            }

            PlayerSession session = new PlayerSession
            {
                loadedScenes = new HashSet<string>(),
                playerId = conn.authenticationData as string
            };

            Sessions.Add(conn.connectionId, session);
            PrepareAndSpawn(conn).Forget();
        }

        // [S4] 클라이언트에게 씬 로드 지시 + 스폰 위치 저장
        // 초기 접속은 번들/씬 로드는 PrepareAndSpawn이 담당
        public async UniTaskVoid PrepareAndSpawn(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Session] PrepareAndSpawn 시작 conn={conn.connectionId}");
            await UniTask.WaitUntil(() => _manager.ServerReady, cancellationToken: _manager.Cts.Token);
            Debug.Log($"[Session] ServerReady 통과");
            // 서버 준비 대기 중 이 클라이언트(conn)가 먼저 연결을 끊으면 RemovePlayer가 세션을 삭제
            // Sessions[conn.connectionId] 직접 접근 시 KeyNotFoundException 발생
            if (!Sessions.TryGetValue(conn.connectionId, out var session))
            {
                Debug.Log("[Session] 세션 없음 → return");
                return;
            }

            var saveData = (!string.IsNullOrEmpty(session.playerId) && _saveManager != null)
                          ? _saveManager.Load(session.playerId) : null;
            var hasSpawnData = saveData.HasValue;
            session.isNewPlayer = !hasSpawnData;

            string spawnScene = !string.IsNullOrEmpty(_manager.defaultSubScene) ? _manager.defaultSubScene : "Assets/00.Scenes/FieldScene.unity";
            Vector3 spawnPosition;

            if (!string.IsNullOrEmpty(session.playerId) && hasSpawnData)
            {
                // 세이브 데이터가 있으면 → 저장된 씬 + 위치 사용
                spawnScene = saveData.Value.lastSceneNames != null && saveData.Value.lastSceneNames.Length > 0
                    ? saveData.Value.lastSceneNames[0]
                    : spawnScene;
                spawnPosition = saveData.Value.lastPosition;
                session.recruitedCompanionIds = saveData.Value.recruitedCompanionIds ?? Array.Empty<string>();
                session.companionAlive = saveData.Value.companionAlive;
                session.companionPresent = saveData.Value.companionPresent;
            }
            else
            {
                // 시작 지점은 신규 플레이어만 소비한다.
                var startPos = _manager.GetStartPosition();
                if (startPos == null)
                    Debug.LogWarning("[Session] NetworkStartPosition이 없다 — 원점에 스폰된다.");
                spawnPosition = startPos != null ? startPos.position : Vector3.zero;
            }

            session.pendingSpawnPosition = spawnPosition;
            session.pendingSpawnScene = spawnScene;

            // spawnScene에 해당하는 번들 메시지도 전송
            var zone = _manager.zones != null ? _manager.zones.FirstOrDefault(z => z.sceneName == spawnScene) : null;
            bool isLocalHost = NetworkServer.localConnection != null
                            && conn == NetworkServer.localConnection;

            if (!isLocalHost)
            {
                var labels = new List<string> { "character_parts" };

                if (zone != null && !string.IsNullOrEmpty(zone.ZoneAssetLabel?.labelString))
                    labels.Add(zone.ZoneAssetLabel.labelString);

                foreach (var label in labels)
                    session.sentBundleLabels.Add(label);

                conn.Send(new BundleRequiredMessage { labels = labels.ToArray() });

                // 번들 전부(캐릭터 파츠 + 존) 완료까지 대기 — 씬 로드/스폰은 이 아래에서만 진행됨
                await UniTask.WaitUntil(() =>
                    _manager.IsBundleReady(conn.connectionId) || !Sessions.ContainsKey(conn.connectionId),
                    cancellationToken: _manager.Cts.Token);
                // 세션이 없으면 return (연결 끊김)
                if (!Sessions.TryGetValue(conn.connectionId, out session))
                    return;
            }
            // 번들 완료 확인 후 씬 로드 지시
            conn.Send(new AdditiveSceneMessage { sceneName = spawnScene, load = true });
            // LoadSceneForPlayer(ServerZoneService)를 안 거치는 경로라 loadedScenes에 수동 등록해야 한다.
            // 빠뜨리면 첫 CheckZones 틱이 스폰 씬까지 "신규"로 판정해 중복 로드 지시를 보낸다.
            session.loadedScenes.Add(spawnScene);
            conn.Send(new SpawnReadyMessage { sceneCount = 1 });
        }

        // [S5] ACK 수신 → 캐릭터 생성 + 씬 이동
        // 클라이언트가 씬 로드 완료 ACK를 보내면 서버가 캐릭터를 스폰한다. (S1에서 핸들러 등록)
        public void OnSceneLoadedAck(NetworkConnectionToClient conn, SceneLoadedAckMessage msg)
        {
            Debug.Log($"[Session] OnSceneLoadedAck conn={conn.connectionId}");
            SpawnPlayer(conn);  // 씬 ACK 오면 항상 스폰 (번들은 이미 완료됨)
        }

        public void SpawnPlayer(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Session] SpawnPlayer 호출 conn={conn.connectionId}");
            // 그 사이 접속이 끊겨 세션이 사라졌으면 스폰하지 않는다
            if (!Sessions.TryGetValue(conn.connectionId, out var session)) return;

            // Instantiate 시점부터 올바른 위치에 생성해야 Spawn 메시지에 정확한 위치가 포함됨.
            // 위치는 PrepareAndSpawn에서 이미 확정됐으므로 여기서 다시 뽑지 않는다 —
            // GetStartPosition()을 한 번 더 부르면 RoundRobin 순번이 접속 1회당 두 칸씩 밀린다.
            Vector3 spawnPos = session.pendingSpawnPosition;

            // base.OnServerAddPlayer(conn) 대신 직접 인스턴스화 (위치 보정 위해)
            GameObject player = MySecondWorld.Network.Player.PlayerFactory.Create(_manager.playerPrefab, spawnPos, Quaternion.identity);
            if (player == null)
                return;
            
            if (!session.isNewPlayer && player.TryGetComponent(out NetworkCharacterAppearance appearance))
            {
                var saved = _saveManager.Load(session.playerId);
                if (saved?.hasAppearance == true)
                    appearance.ServerRestoreAppearance(saved.Value.appearance);
            }

            NetworkServer.AddPlayerForConnection(conn, player);

            // 신규 플레이어에게만 시작 골드 지급 (재접속 시 중복 지급 방지)
            if (session.isNewPlayer)
                GrantStartingGold(conn);
            else
            {
                RestoreInventory(conn, session);
                RestoreEquippedPartSlots(conn, session); // 인벤토리가 채워진 뒤여야 칸 번호 번역이 맞는다
                RestoreUnlockedKeys(conn, session);      // 도감을 복원한다.
            }

            // 신규 플레이어는 시작 골드 지급 후, 기존 플레이어는 세이브 복원 후에 해금 목록 전체를 클라에 처음 보낸다.
            // (이 호출 이후부터는 새로 해금될 때마다 자동으로 보낸다. 복원 중 DoAdd 때는 보내지 않는다)
            conn.GetPlayerComponent<NetworkCharacterAppearance>()?.ServerSendUnlockedKeys();

            session.currentScene = session.pendingSpawnScene;
            var targetScene = _manager.GetServerScene(session.currentScene);
            if (targetScene.IsValid() && targetScene.isLoaded && conn.identity != null)
            {
                var playerGo = conn.identity.gameObject;
                if (playerGo.transform.parent != null)
                    playerGo.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(playerGo, targetScene);
            }

            // 스폰 직후 lastSafePosition 초기화:
            // zone bounds 체크 실패해도 즉시 텔레포트되지 않도록 안전 위치를 스폰 위치로 세팅
            session.lastSafePosition = session.pendingSpawnPosition;
            session.lastSafeScene = session.currentScene;
            session.fallTimer = 0f;
            session.isSpawned = true;

            // 첫 접속 시 씬이 던전인지 판별하여 matchId ← roomId 배정
            if (_roomRegistry.IsDungeonScene(session.currentScene))
            {
                var roomId = _roomRegistry.AssignRoom(session.currentScene, conn.connectionId, out bool isNewRoom);
                conn.GetPlayerComponent<NetworkMatch>().matchId = roomId;
                session.roomId = roomId;

                // 시연용 오버레이에 방을 알려준다(컴포넌트가 없으면 조용히 건너뜀)
                conn.GetPlayerComponent<RoomIdDisplay>()?.SetRoom(roomId);

                // matchId를 세팅한 '뒤에' 스폰을 알린다.
                // 순서가 반대면 몬스터가 스폰되는 시점에 이 플레이어가 아직 옛 match라
                // 옵저버에서 빠지고, 서버에만 있는 안 보이는 몬스터가 된다.
                if (isNewRoom)
                    _roomRegistry.NotifyRoomOpened(session.currentScene, roomId);
            }
            else
            {
                // session.roomId는 Guid.Empty 그대로 — 던전 방이 아니니까
                conn.GetPlayerComponent<NetworkMatch>().matchId 
                    = SceneMatchId.GetMatchIdFromScene(session.currentScene);
                conn.GetPlayerComponent<RoomIdDisplay>()?.SetRoom(Guid.Empty);
            }

            // 이미 영입한 컴패니언을 자동 재소환한다. (현재는 1마리만, 소환 해제 상태면 건너뜀)
            if (session.recruitedCompanionIds != null && session.recruitedCompanionIds.Length > 0 && session.companionPresent)
            {
                var companionId = session.recruitedCompanionIds[0];
                var prefab = _manager.CompanionService.GetPrefab(companionId);
                if (prefab != null)
                {
                    SpawnCompanionFor(session, conn, prefab);
                    // 마지막 접속 때 죽어있었으면 애니메이션 없이 바로 죽은 채로 재현
                    if (!session.companionAlive && session.companionIdentity != null &&
                        session.companionIdentity.TryGetComponent(out ICompanionServerState serverState))
                        serverState.MarkDead();
                }
                else
                    Debug.LogWarning($"[Session] companionId '{companionId}'에 매칭되는 프리팹이 레지스트리에 없음");
            }
        }

        // [S-End] 세이브 + 세션 정리 (OnServerDisconnect에서 호출)
        public void RemovePlayer(NetworkConnectionToClient conn)
        {
            // 1. playerNames 정리 — 세션이 만들어지기 전(인증만 통과하고 끊김)에도 이름이 남지 않게 if 바깥으로
            if (conn.authenticationData is string username)
                PlayerAuthenticator.playerNames.Remove(username);

            if (Sessions.TryGetValue(conn.connectionId, out var session))
            {
                // 2. 세이브
                SavePlayer(conn, session);

                if (session.companionIdentity != null)
                    NetworkServer.Destroy(session.companionIdentity.gameObject);

                // 3. 던전 룸 퇴장 — 방 슬롯 반환해야 다른 플레이어가 입장 가능
                if (session.roomId != Guid.Empty)
                    _roomRegistry.LeaveRoom(session.currentScene, conn.connectionId);

                // 4. 세션 제거
                session.sentBundleLabels.Clear();
                Sessions.Remove(conn.connectionId);

                _npcMemories.Remove(conn.connectionId);
                _lastNpcAppendTime.Remove(conn.connectionId);
                _lastNpcSummaryTime.Remove(conn.connectionId);
            }
        }

        #endregion

        #region 컴패니언

        private void SpawnCompanionFor(PlayerSession session, NetworkConnectionToClient conn, GameObject prefab)
        {
            // 플레이어 발밑(0,0,0) 대신, 플레이어 우측 1.5m 옆 지점에 최초 생성! (들썩거림 원천 차단)
            Vector3 playerPos = conn.identity.transform.position;
            Vector3 spawnPos = playerPos + (Vector3.right * 1.5f);
            var companion = CompanionFactory.Create(
                prefab, spawnPos, Quaternion.identity, conn.identity.transform);
            if (companion == null) return;

            // 최초 스폰 시 씬 위치 동일하게 맞춤
            if (conn.identity != null)
            {
                var targetScene = conn.identity.gameObject.scene;
                if (targetScene.IsValid() && targetScene.isLoaded)
                {
                    if (companion.transform.parent != null)
                        companion.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(companion, targetScene);
                }
            }

            session.companionIdentity = companion.GetComponent<NetworkIdentity>();
            if (companion.TryGetComponent(out NetworkMatch companionMatch))
                companionMatch.matchId = conn.GetPlayerComponent<NetworkMatch>().matchId;

            // 실전투 사망/부활 등 생사가 바뀔 때마다 세션에 자동 기록 — 재접속 시 그대로 재현하기 위함
            if (companion.TryGetComponent(out ICompanionServerState serverState))
                serverState.AliveChanged += alive => session.companionAlive = alive;
        }

        // 컴패니언 영입 진입점. 현재 호출자는 CompanionAdminService — "누가 언제 부르는지"는 이 메서드 책임 밖
        public void RecruitCompanion(NetworkConnectionToClient conn, GameObject speciesPrefab, string companionId)
        {
            if (!Sessions.TryGetValue(conn.connectionId, out var session)) return;
            if (session.companionIdentity != null) return; // 현재는 1마리만 — 이미 있으면 무시

            SpawnCompanionFor(session, conn, speciesPrefab);
            session.companionAlive = true;
            session.companionPresent = true;

            if (session.companionIdentity == null) return; // 스폰 실패

            // recruitedCompanionIds는 "이 플레이어가 지금까지 영입한 companion들의 ID 목록"
            var ids = session.recruitedCompanionIds ?? Array.Empty<string>();
            if (System.Array.IndexOf(ids, companionId) < 0) // < 0은 "못 찾았다 = 아직 이 companionId가 목록에 없다"는 뜻
            {
                // (없을 때만) 배열을 하나 더 큰 걸로 새로 만듦
                var newIds = new string[ids.Length + 1];
                // 옛날 배열(ids)에 있던 값들을 새 배열(newIds)의 0번 자리부터 그대로 복사
                // 예를 들어 ids가 ["Mowe"]였으면, newIds는 지금 ["Mowe", null](맨 뒤 한 칸은 아직 빈 자리) 상태
                ids.CopyTo(newIds, 0);
                // 새 값을 맨 끝자리에 넣기
                newIds[ids.Length] = companionId;
                //  세션에 반영
                session.recruitedCompanionIds = newIds;
            }
        }

        #endregion

        #region NPC 기억

        // 이 접속의 NPC 기억 캐시를 돌려준다.
        // 이 접속에서 처음 부르면 서버 세이브(playerId 파일)에서 한 번 읽어 캐시에 채우고, 이후엔 캐시만 쓴다
        // 옛 세이브라 dialogue가 null이면 빈 배열로 바꿔 뒤쪽 코드가 null 검사를 안 해도 되게 한다
        private Dictionary<string, NpcMemorySave> GetNpcMemories(NetworkConnectionToClient conn, PlayerSession session)
        {
            if (_npcMemories.TryGetValue(conn.connectionId, out var memories))
                return memories;

            memories = new Dictionary<string, NpcMemorySave>();
            var saved = _saveManager.Load(session.playerId)?.npcMemories;
            if (saved != null)
            {
                foreach (var m in saved)
                {
                    if (string.IsNullOrEmpty(m.npcKey)) continue;
                    var copy = m;
                    copy.dialogue ??= Array.Empty<NpcDialoguePairSave>();
                    memories[m.npcKey] = copy;
                }
            }
            _npcMemories[conn.connectionId] = memories;
            return memories;
        }

        // [수신: 클라 → 서버] 클라가 보낸 NpcMemoryRequestMessage를 서버가 받는 핸들러
        // 이 NPC의 기억(요약 필드 + 대화 기록)을 찾아 요청한 클라에게 돌려준다
        // 서버에 기록이 없는 NPC면 빈 기억을 만들어 돌려주기만 하고, 캐시에는 넣지 않는다(Append/Summary가 올 때 만든다)
        public void OnNpcMemoryRequest(NetworkConnectionToClient conn, NpcMemoryRequestMessage msg)
        {
            // 서버 프로세스 내에서 일어나는 일
            if (!Sessions.TryGetValue(conn.connectionId, out var session) || string.IsNullOrEmpty(session.playerId)) return;
            if (string.IsNullOrEmpty(msg.npcKey) || msg.npcKey.Length > 100) return;

            if (!GetNpcMemories(conn, session).TryGetValue(msg.npcKey, out var memory))
            {
                memory = new NpcMemorySave
                {
                    npcKey = msg.npcKey,
                    dialogue = Array.Empty<NpcDialoguePairSave>()
                };
            }
            // [송신: 서버 → 클라] 요청한 클라(conn)에게만 대화 기록을 답장
            conn.Send(new NpcMemoryResponseMessage{ memory = memory });
        }

        // [수신: 클라 → 서버] 클라가 보낸 NpcMemoryAppendMessage를 서버가 받는 핸들러
        // 새 한 쌍을 뒤에 붙이고 MaxNpcPairs개로 자른다. pairId가 이미 있으면 중복이라 붙이지 않고 Ack만 보낸다
        // 처리 순서: 검증(세션·키·pairId) → 연타 제한 → 캐시 찾기/만들기 → 중복 검사 후 추가 → Ack
        public void OnNpcMemoryAppend(NetworkConnectionToClient conn, NpcMemoryAppendMessage msg)
        {
            // 서버 프로세스 내에서 일어나는 일
            if (!Sessions.TryGetValue(conn.connectionId, out var session) || string.IsNullOrEmpty(session.playerId)) return;
            if (string.IsNullOrEmpty(msg.npcKey) || msg.npcKey.Length > 100) return;
            if (string.IsNullOrEmpty(msg.pair.pairId) || msg.pair.pairId.Length > 64) return;

            // 연타 제한 — Ack를 안 보내면 클라가 나중에 재전송한다
            // 연타 제한에 걸리면 conn.Send(new NpcMemoryAckMessage 줄에 도달하기 전에 return하므로 Ack가 안감
            float now = Time.unscaledTime;
            if (_lastNpcAppendTime.TryGetValue(conn.connectionId, out var last) && now - last < MinAppendInterval) return;
            _lastNpcAppendTime[conn.connectionId] = now;

            var memories = GetNpcMemories(conn, session);
            if (!memories.TryGetValue(msg.npcKey, out var memory))  // 처음 보는 NPC면
            {
                if (memories.Count >= MaxNpcKeys) return;           // 이미 64명이면 새 NPC는 받지 않음
                memory = new NpcMemorySave 
                {
                    npcKey = msg.npcKey,                            // 새 NPC의 빈 기억(요약 필드는 기본값 0/빈 값) — 아래에서 대화를 채운 뒤 memories에 넣는다
                    dialogue = Array.Empty<NpcDialoguePairSave>()
                };              
            }

            // 배열(dialogue)은 Add/RemoveAt이 안 되므로 List로 복사해 고친 뒤, 끝에서 다시 배열로 바꿔 넣는다
            // list = 서버가 들고 있는 "이 플레이어와 이 NPC의 대화 기록"
            var list = new List<NpcDialoguePairSave>(memory.dialogue ?? Array.Empty<NpcDialoguePairSave>());
            
            // msg.pair는 클라가 방금 보낸 새 대화 한 쌍
            // 이미 저장된 pairId면 건너뛰어 중복 저장을 막는다
            if (!list.Exists(p => p.pairId == msg.pair.pairId))
            {
                list.Add(new NpcDialoguePairSave
                {
                    pairId = msg.pair.pairId,
                    timestamp = msg.pair.timestamp,
                    userText = ClampNpcText(msg.pair.userText),
                    npcText = ClampNpcText(msg.pair.npcText)
                });

                // 쌍이 MaxNpcPairs개를 넘으면 맨 앞(RemoveAt(0))을 지움. 오래된 것부터 삭제
                // 그 뒤 list를 배열로 되돌려 memory에 넣고, memory를 캐시(memories)에 다시 저장한다
                // (NpcMemorySave는 struct라 복사본을 고친 것 — 캐시에 되써야 반영됨)
                while (list.Count > MaxNpcPairs) list.RemoveAt(0);
                memory.dialogue = list.ToArray();
                memories[msg.npcKey] = memory;
            }

            // [송신: 서버 → 클라] "이 pairId 받았음" 확인 — 클라가 이걸 받으면 미확인 큐에서 지운다
            conn.Send(new NpcMemoryAckMessage { pairId = msg.pair.pairId });
        }


        // [수신: 클라 → 서버] 클라가 보낸 NpcMemorySummaryMessage를 서버가 받는 핸들러
        // 요약 필드만 덮어쓴다(대화 기록 dialogue는 읽지도 건드리지도 않음 — Append만 대화를 바꾼다)
        // factionRelation은 일부러 복사 안 함 — 클라가 보낸 값을 받으면 앱을 조작해 호감도를 올릴 수 있다(혜택과 연결될 수 있음)
        // 그래서 호감도는 서버 코드(AddNpcFactionRelation)만 바꾸고, 이 핸들러가 캐시의 기존 값을 그대로 둔다
        // 클라가 보낸 값은 믿지 않고, 숫자는 범위로 / 문자열은 길이로 / 목록은 개수로 잘라서 쓴다
        // 처리 순서: 검증 → 요약 전용 연타 제한 → 캐시 찾기/만들기 → 필드 덮어쓰기 → Ack(seq)
        public void OnNpcMemorySummary(NetworkConnectionToClient conn, NpcMemorySummaryMessage msg)
        {
            if (!Sessions.TryGetValue(conn.connectionId, out var session) || string.IsNullOrEmpty(session.playerId)) return;
            var m = msg.memory;
            if (string.IsNullOrEmpty(m.npcKey) || m.npcKey.Length > 100) return;

            float now = Time.unscaledTime;
            if (_lastNpcSummaryTime.TryGetValue(conn.connectionId, out var last) && now - last < MinAppendInterval) return;
            _lastNpcSummaryTime[conn.connectionId] = now;

            var memories = GetNpcMemories(conn, session);
            if (!memories.TryGetValue(m.npcKey, out var cur))
            {
                if (memories.Count >= MaxNpcKeys) return;
                cur = new NpcMemorySave
                {
                    npcKey = m.npcKey,
                    dialogue = Array.Empty<NpcDialoguePairSave>()
                };
            }

            cur.relationScore = Mathf.Clamp(m.relationScore, -100f, 100f);
            cur.questStage = Mathf.Max(0, m.questStage);
            cur.questGiven = m.questGiven;
            cur.firstMetDate = ClampNpcText(m.firstMetDate);
            cur.firstMetLocation = ClampNpcText(m.firstMetLocation);
            cur.conversationCount = Mathf.Max(0, m.conversationCount);
            var fact = new List<string>();
            if (m.knownFacts != null)
            {
                foreach (var f in m.knownFacts)
                {
                    if (fact.Count >= MaxNpcFacts) break;
                    if (!string.IsNullOrEmpty(f)) fact.Add(ClampNpcText(f));
                }
            }
            cur.knownFacts = fact.ToArray();
            memories[m.npcKey] = cur;

            // [송신: 서버 → 클라] "이 NPC의 seq번 요약 받았음" 확인
            conn.Send(new NpcMemorySummaryAckMessage { npcKey = m.npcKey, seq = msg.seq });
        }

        // [서버 전용] 호감도(factionRelation)를 바꾸는 유일한 길. delta만큼 더하고 -100~100으로 자른다
        // TODO: 지금은 부르는 곳이 없다. 나중에 계산 로직(긍정 대화·동행 시간·구매·선물)이 서버에서 이 메서드를 부르게 된다
        // 바뀐 값은 캐시에 들어가고, 다음 Request 응답과 저장(SavePlayer) 때 함께 나간다
        public void AddNpcFactionRelation(NetworkConnectionToClient conn, string npcKey, float delta)
        {
            if (!Sessions.TryGetValue(conn.connectionId, out var session) || string.IsNullOrEmpty(session.playerId)) return;
            if (string.IsNullOrEmpty(npcKey) || npcKey.Length > 100) return;
            var memories = GetNpcMemories(conn, session);
            if (!memories.TryGetValue(npcKey, out var cur))
            {
                if (memories.Count >= MaxNpcKeys) return;
                cur = new NpcMemorySave { npcKey = npcKey, dialogue = Array.Empty<NpcDialoguePairSave>() };
            }
            cur.factionRelation = Mathf.Clamp(cur.factionRelation + delta, -100f, 100f);
            memories[npcKey] = cur;
        }

        // 문자열이 null이면 빈 문자열로, MaxNpcTextLength보다 길면 잘라서 돌려준다
        private static string ClampNpcText(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > MaxNpcTextLength ? s.Substring(0, MaxNpcTextLength) : s);

        // SavePlayer용: 캐시를 세이브 배열로. 이 접속에서 NPC 기억을 한 번도 안 건드렸으면 null(→ 이전 세이브 유지)
        private NpcMemorySave[] BuildNpcMemorySave(NetworkConnectionToClient conn)
        {
            if (!_npcMemories.TryGetValue(conn.connectionId, out var memories)) return null;
            return new List<NpcMemorySave>(memories.Values).ToArray();
        }

        #endregion


        #region 저장

        // 저장 시점
        // 1. 서버 시작 후 autoSaveInterval(기본 60초)마다 — AutoSaveLoop → SaveAllPlayers → SavePlayer (접속 중인 전원)
        // 2. 플레이어 접속 종료 시 — OnServerDisconnect → RemovePlayer → SavePlayer (끊긴 한 명)
        // 스폰 전(isSpawned=false)이거나 playerId가 없거나 위치가 zero면 저장하지 않는다
        private void SavePlayer(NetworkConnectionToClient conn, PlayerSession session)
        {
            if (session.isSpawned && !string.IsNullOrEmpty(session.playerId))
            {
                // lastSafePosition 우선, 없으면 현재 위치 사용
                var posToSave = session.lastSafePosition != Vector3.zero
                    ? session.lastSafePosition
                    : conn.identity?.transform.position ?? Vector3.zero;

                if (posToSave != Vector3.zero)
                {
                    var prev = _saveManager.Load(session.playerId);                   // 서버 디스크의 지난번 세이브 전체(없으면 null)
                    bool hadSaved = prev?.hasAppearance ?? false;                     // 그 prev 중 외형이 예전 디스크 세이브에 있었는지만 뽑은 bool
                    bool hasCurrent = BuildAppearanceSave(conn, out var currentApp);  // 지금 떠있는 서버 메모리에 외형이 있는지

                    _saveManager.Save(session.playerId, new SaveData
                    {
                        lastSceneNames = new string[] { session.currentScene },
                        lastPosition = posToSave,
                        recruitedCompanionIds = session.recruitedCompanionIds,
                        companionAlive = session.companionAlive,
                        companionPresent = session.companionPresent,
                        inventorySlots = BuildInventorySave(conn) ?? prev?.inventorySlots,
                        // 외형 기록 유무: 지금 서버 메모리에 있거나(hasCurrent), 디스크의 이전 세이브에 있었으면(hadSaved) true
                        hasAppearance = hasCurrent || hadSaved,
                        // 외형 값: 지금 서버 메모리 값이 있으면(hasCurrent)? 그걸 저장(currentApp) : 없으면 이전 세이브(디스크, prev?.appearance) 저장 ?? 이전 디스크 세이브도 없으면 빈 값(default)
                        appearance = hasCurrent ? currentApp : (prev?.appearance ?? default),
                        mainHandDbKey = BuildMainHandSave(conn) ?? prev?.mainHandDbKey ?? -1,
                        mainHandInventorySlotIndex = BuildMainHandSlotSave(conn) ?? prev?.mainHandInventorySlotIndex ?? -1,
                        equippedPartSlots = BuildEquippedPartSlotsSave(conn) ?? prev?.equippedPartSlots,
                        unlockedItemDbKeys = BuildUnlockedSave(conn) ?? prev?.unlockedItemDbKeys,
                        npcMemories = BuildNpcMemorySave(conn) ?? prev?.npcMemories,
                    });
                }
            }
        }

        public void SaveAllPlayers()
        {
            int count = 0;
            foreach (var kv in Sessions.ToList())
            {
                if (!NetworkServer.connections.TryGetValue(kv.Key, out var conn))
                    continue;

                SavePlayer(conn, kv.Value);
                count++;
            }

            if (count > 0)
                Debug.Log($"[Save] 주기 저장 {count}명");
        }

        // 인벤토리를 읽어 InventorySlotSave[] 를 돌려줌
        private InventorySlotSave[] BuildInventorySave(NetworkConnectionToClient conn)
        {
            var inventory = conn.GetPlayerComponent<Inventory>();
            if (inventory == null) return null;
            var state = inventory.ToState();
            var slots = new InventorySlotSave[state.ItemDbKeys.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new InventorySlotSave
                {
                    dbKey = state.ItemDbKeys[i],
                    count = state.ItemStackCounts[i]
                };
            }

            return slots;
        }

        // 서버에 저장된 외형이 있으면 true, 값은 data로 내보낸다 (out)
        private bool BuildAppearanceSave(NetworkConnectionToClient conn, out CharacterAppearanceData data)
        {
            // out은 어느 경로로 끝나도 값을 채워야 해서, 아래에서 일찍 return해도 비지 않게 미리 채움
            // 아래 한줄이 없으면 뜨는 에러 → "CS0177: 제어가 현재 메서드를 벗어나기 전에 out 매개 변수 'data'를 할당해야 합니다"
            data = default;

            if (conn.identity == null)
            {
                Debug.LogWarning($"[Save] 외형 저장 실패: conn={conn.connectionId} identity가 null");
                return false;
            }

            var appearance = conn.GetPlayerComponent<NetworkCharacterAppearance>();
            if (appearance == null)
            {
                Debug.LogWarning($"[Save] 외형 저장 실패: conn={conn.connectionId} NetworkCharacterAppearance 컴포넌트 없음");
                return false;
            }
            return appearance.ServerTryGetAppearance(out data); // false는 '외형 미저장 계정'을 뜻함. 정상이라 로그 없음
        }

        // 장착 중인 무기 dbKey를 읽음
        private int? BuildMainHandSave(NetworkConnectionToClient conn)
            => conn.GetPlayerComponent<NetworkWeaponEquip>()?.ServerGetMainHandDbKey();

        // 장착한 인벤토리 칸 번호를 읽음
        private int? BuildMainHandSlotSave(NetworkConnectionToClient conn)
            => conn.GetPlayerComponent<NetworkWeaponEquip>()?.ServerGetMainHandInventorySlotIndex();

        // 서버가 기억하는 파츠별 장착 칸 번호를 읽음. 컴포넌트가 없거나 받은 적이 없으면 null
        private int[] BuildEquippedPartSlotsSave(NetworkConnectionToClient conn)
            => conn.GetPlayerComponent<NetworkCharacterAppearance>()?.ServerGetEquippedPartSlots();

        // (도감) 서버가 기억하는 해금 목록을 읽음. 컴포넌트가 없으면 null
        private int[] BuildUnlockedSave(NetworkConnectionToClient conn)
            => conn.GetPlayerComponent<NetworkCharacterAppearance>()?.ServerGetUnlockedKeys();

        #endregion

        #region 복원

        // 세이브를 읽어 인벤토리를 채움
        private void RestoreInventory(NetworkConnectionToClient conn, PlayerSession session)
        {
            // ① 인벤토리를 얻는다 (저장 때와 같은 방식)

            var inventory = conn.GetPlayerComponent<Inventory>();
            if (inventory == null) return;

            // ② 세이브를 읽는다. 없거나, 인벤토리 필드가 비어 있으면(옛 세이브) 아무것도 안 한다
            var loaded = _saveManager.Load(session.playerId);
            if (loaded == null || loaded.Value.inventorySlots == null) return;

            // ③ 슬롯을 한 칸씩 돌며 아이템을 다시 넣는다
            foreach (var slot in loaded.Value.inventorySlots)
            {
                if (slot.dbKey == -1 || slot.count <= 0) continue;        // 빈 슬롯
                if (Vault.Get(slot.dbKey) is not RootItem item) continue; // 아이템 데이터가 없어졌으면 건너뜀
                inventory.DoAdd(new RootItemStack(item, slot.count));
            }

            var equip = conn.GetPlayerComponent<NetworkWeaponEquip>();
            if (equip != null)
            {
                // 저장했던 칸에 그 무기가 그대로 있으면 그 칸에, 아니면(옛 세이브 등) 같은 무기가 든 첫 칸에 장착한다.
                int slot = ToRestoredSlotIndex(loaded.Value.inventorySlots, loaded.Value.mainHandInventorySlotIndex);
                if (!equip.ServerTryEquipMainHand(loaded.Value.mainHandDbKey, slot))
                    equip.ServerTryEquipMainHand(loaded.Value.mainHandDbKey, -1); // 세이브에는 칸 -1(없음/모름)이면 처음 발견한 칸에 장착
            }
        }

        // 세이브를 읽어 파츠별 장착 칸 번호를 서버에 되살리고, 그 클라이언트에게도 알려 준다
        private void RestoreEquippedPartSlots(NetworkConnectionToClient conn, PlayerSession session)
        {
            var appearance = conn.GetPlayerComponent<NetworkCharacterAppearance>();
            if (appearance == null) return;

            // 옛 세이브(칸 번호 필드가 없음)이거나 인벤토리를 저장한 적이 없으면 아무것도 안 한다
            var loaded = _saveManager.Load(session.playerId);
            if (loaded == null
                || loaded.Value.equippedPartSlots == null
                || loaded.Value.inventorySlots == null) return;

            // 저장 때의 칸 번호를 복원 후의 칸 번호로 번역한다. (왜 필요한지는 ToRestoredSlotIndex 주석 참고)
            var saved = loaded.Value.equippedPartSlots; // 세이브 파일에서 읽은 값, 저장할 때의 가방 배치
            var restored = new int[saved.Length];       // saved를 번역한 값	  , 지금 다시 채운 가방 배치
            for (int i = 0; i < saved.Length; i++)
                restored[i] = ToRestoredSlotIndex(loaded.Value.inventorySlots, saved[i]);

            appearance.ServerRestoreEquippedPartSlots(restored); // 서버가 기억해 둔다 (다음 저장 때 쓴다)
            appearance.ServerSendEquippedPartSlots();            // 이 클라이언트에게도 알려 준다 (인벤토리 표시용)
        }

        // (도감) 세이브의 해금 목록을 서버 기억에 합친다. 옛 세이브는 RestoreInventory의 DoAdd가 채워 준다
        private void RestoreUnlockedKeys(NetworkConnectionToClient conn, PlayerSession session)
        {
            var loaded = _saveManager.Load(session.playerId);
            if (loaded == null) return;
            conn.GetPlayerComponent<NetworkCharacterAppearance>()?.
                ServerRestoreUnlockedKeys(loaded.Value.unlockedItemDbKeys);// null(옛 세이브)이면 함수가 알아서 건너뜀
        }

        // 저장할 때의 칸 번호를 복원 후의 칸 번호로 바꾼다.(-1은 그대로 -1)
        // 복원은 빈 칸을 건너뛰고 앞에서부터 채우므로, 새 번호 = 저장 칸 앞에 있던 "실제로 채워진 칸"의 개수.
        //
        // 예) 도끼B(저장 4번 칸)를 장착한 채 저장했다면
        //     저장:  0=도끼A  1=빈칸  2=활  3=빈칸  4=도끼B
        //     복원:  0=도끼A  1=활  2=도끼B          ← 빈 칸이 사라져 앞으로 당겨짐
        //     0~3번을 훑으며 채워진 칸(도끼A, 활)만 세면 2 → 도끼B의 새 번호
        private static int ToRestoredSlotIndex(InventorySlotSave[] savedSlots, int savedIndex)
        {
            if (savedSlots == null || savedIndex < 0 || savedIndex >= savedSlots.Length) return -1; // -1은 "칸 없음/모름"이라 그대로 -1

            int restored = 0;
            for (int i = 0; i < savedIndex; i++)
            {
                var s = savedSlots[i];
                if (s.dbKey == -1 || s.count <= 0) continue; // 빈 칸. RestoreInventory가 건너뛰는 칸과 같은 조건
                if (Vault.Get(s.dbKey) is not RootItem) continue; // 저장된 아이템 정의가 지금은 없어진 경우
                restored++;
            }
            return restored;
        }

        #endregion

        #region 테스트/디버그

        // 시작 골드 지급. 스폰 경로와 디버그 버튼이 같은 코드를 쓰도록 분리.
        // "신규 플레이어인가" 판단은 호출부 책임 — 디버그 경로는 그 조건을 일부러 건너뛴다.
        public void GrantStartingGold(NetworkConnectionToClient conn)
        {
            if (_startingGoldItem == null)
            {
                Debug.LogWarning("[Session] GameNetworkManager의 Starting Gold Item이 비어 있어 지급을 건너뜀");
                return;
            }
            if (conn.identity == null) return;

            var inventory = conn.GetPlayerComponent<Inventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[Session] conn={conn.connectionId}의 플레이어에 Inventory가 없음");
                return;
            }

            inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(_startingGoldItem, _startingGoldAmount));
            Debug.Log($"[Session] conn={conn.connectionId}에게 골드 {_startingGoldAmount} 지급");
        }

        // 무기 테스트용 — Sword/Ring/Bow/Quiver/Bracelet/Necklace/Axe/Polearm을 1개씩 지급.
        // 실제 인게임 획득 경로(상점/드랍)가 충분해지면 이 메서드는 삭제한다.
        public void GrantWeaponTestKit(NetworkConnectionToClient conn)
        {
            if (conn.identity == null) return;

            var inventory = conn.GetPlayerComponent<Inventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[Session] conn={conn.connectionId}의 플레이어에 Inventory가 없음");
                return;
            }

            foreach (var item in new[] {  _testSwordItem, _testRingItem, _testBowItem,
                _testQuiverItem, _testBraceletItem, _testNecklaceItem, _testAxeItem, _testPolearmItem})
            {
                if (item == null) continue;
                inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(item, 1));
            }
            Debug.Log($"[Session] conn={conn.connectionId}에게 무기 테스트 킷 지급");
        }

        // 테스트용 옷/방어구(EquipmentItem) 일괄 인벤토리 지급
        public void GrantArmorTestKit(NetworkConnectionToClient conn)
        {
            if (conn.identity == null) return;
            var inventory = conn.GetPlayerComponent<Inventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[Session] conn={conn.connectionId} 플레이어에 Inventory가 없음");
                return;
            }
            if (_avatarPartKeyTable == null)
            {
                Debug.LogWarning("[Session] AvatarPartKeyTableSO가 연결되지 않아 방어구 지급 불가");
                return;
            }

            int grantedCount = 0;
            var addedItems = new HashSet<Cleverous.VaultInventory.RootItem>();

            // 1~3번 인덱스의 파츠 요구 아이템만 지급 (0번은 맨몸이라 제외)
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                for (int i = 1; i <= 3; i++)
                {
                    // 남성 파츠 요구 아이템
                    var maleItem = _avatarPartKeyTable.GetRequiredItem(part, Gender.Male, i);
                    if (maleItem != null && addedItems.Add(maleItem) && !inventory.Contains(maleItem, 1))
                    {
                        inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(maleItem, 1));
                        grantedCount++;
                    }
                    // 여성 파츠 요구 아이템
                    var femaleItem = _avatarPartKeyTable.GetRequiredItem(part, Gender.Female, i);
                    if (femaleItem != null && femaleItem != maleItem && addedItems.Add(femaleItem) && !inventory.Contains(femaleItem, 1))
                    {
                        inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(femaleItem, 1));
                        grantedCount++;
                    }
                }
            }
            Debug.Log($"[Session] conn={conn.connectionId}에게 1~3번 방어구/의상 아이템 {grantedCount}개 지급");
        }

        #endregion
    }
}