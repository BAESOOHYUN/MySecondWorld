using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MySecondWorld.Network;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat.Monster
{
    // 웨이브를 순서대로 소환하고, 웨이브 내 전멸 시 다음 웨이브로 넘어간다. 마지막 웨이브 클리어 시 이벤트 발행.
    // room 식별자는 그냥 실어 나르기만 함 — 어떤 room 시스템(던전/필드)인지는 몰라도 됨(DungeonRoomRegistry 직접 참조 금지).
    public class MonsterWaveSpawner : MonoBehaviour
    {
        [SerializeField] private MonsterWaveDefinitionSO _waveDefinition;
        [SerializeField] private Transform[] _spawnPoints; // 한 웨이브의 몬스터 수만큼 순환 사용

        // 몬스터 타입(스탯 SO)별 전용 풀 — 웨이브를 돌수록 같은 타입 몬스터를 재사용
        private readonly Dictionary<MonsterStatsSO, IGameObjectPool> _monsterPools 
            = new Dictionary<MonsterStatsSO, IGameObjectPool>();

        // GetOrCreatePool에서 같은 Stats에 다른 PurePrefab이 들어오는 실수를 잡아내기 위한 보조 기록
        private readonly Dictionary<MonsterStatsSO, GameObject> _poolPrefabs
            = new Dictionary<MonsterStatsSO, GameObject>();


        [Header("테스트용")]
        [SerializeField] private bool _autoStartForTesting;
        [SerializeField] private string _testRoomId = "test-room";

        public event Action<string> OnAllWavesCleared;

        private int _currentWaveIndex = -1;
        private readonly List<Health> _aliveMonsters = new List<Health>();
        private string _roomId;

        private void Start()
        {
            // Additive 씬은 모든 클라에도 로드됨 — 가드 없으면 클라마다 독립적으로 중복 스폰함
            if (!NetworkServer.active) return;

            if (_autoStartForTesting)
                StartWaves(_testRoomId);
        }

        private void OnDestroy()
        {
            // 풀 Dispose는 "대기 중(비활성)" 인스턴스만 정리함
            // — 지금 살아있는 활성 몬스터는 별도로 정리해야 함
            foreach (var health in _aliveMonsters)
            {
                if (health != null)
                    Destroy(health.gameObject);
            }
            _aliveMonsters.Clear();

            foreach (var pool in _monsterPools.Values) // _monsterPools.Values(값 컬렉션만) 검색
                pool.Dispose();
            _monsterPools.Clear();
        }

        public void StartWaves(string roomId)
        {
            // 외부에서 직접 호출될 경우 대비하여 서버 가드 설정
            if (!NetworkServer.active) return;

            if (_waveDefinition == null || _spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogError($"[MonsterWaveSpawner] _waveDefinition 또는 _spawnPoints가 비어있어 웨이브를 시작할 수 없음 on {gameObject.name}", this);
                return;
            }

            _roomId = roomId;
            _currentWaveIndex = -1;
            SpawnNextWave();
        }

        private void SpawnNextWave()
        {
            _currentWaveIndex++;
            if (_currentWaveIndex >= _waveDefinition.Waves.Count)
            {
                OnAllWavesCleared?.Invoke(_roomId);
                return;
            }

            var wave = _waveDefinition.Waves[_currentWaveIndex];
            if (wave.Monsters == null || wave.Monsters.Count == 0)
            {
                // 몬스터 0마리인 웨이브는 전멸 이벤트 자체가 안 생겨서 다음 웨이브로 못 넘어감 — 즉시 건너뜀
                Debug.LogWarning($"[MonsterWaveSpawner] 웨이브 {_currentWaveIndex}에 몬스터가 없어 건너뜀 on {gameObject.name}", this);
                SpawnNextWave();
                return;
            }

            _aliveMonsters.Clear();

            for (int i = 0; i < wave.Monsters.Count; i++)
            {
                var entry = wave.Monsters[i];
                if (entry.PurePrefab == null || entry.Stats == null)
                {
                    Debug.LogError($"[MonsterWaveSpawner] 웨이브 {_currentWaveIndex}의 몬스터 슬롯 {i}에 PurePrefab 또는 Stats가 비어있어 스킵 on {gameObject.name}", this);
                    continue;
                }

                var spawnPoint = _spawnPoints[i % _spawnPoints.Length];

                var pool = GetOrCreatePool(entry);
                var instance = pool.Rent();

                // NetworkMatch — 이 웨이브의 몬스터를 같은 방(roomId)의 플레이어에게만 보이게 함
                // 주의: 지금은 필드 씬 기준(_roomId를 그대로 해시)만 정확함.
                // 던전 인스턴스별 실제 Guid roomId(DungeonRoomRegistry.AssignRoom) 연동은 아직 안 돼있음
                // TODO : roomId 배선이 실제로 연결되면 이 줄을 그 Guid로 교체해야 함.
                if (instance.TryGetComponent(out NetworkMatch match))
                    match.matchId = SceneMatchId.GetMatchIdFromScene(_roomId);

                var monsterAI = instance.GetComponent<MonsterAI>();
                monsterAI.PrepareForSpawn(spawnPoint.position, spawnPoint.rotation);

                var health = instance.GetComponent<Health>();
                _aliveMonsters.Add(health);

                // 자기 자신을 구독 해제하는 델리게이트 — HP가 0 되는 즉시가 아니라,
                // 죽는 애니메이션까지 끝나고 실제로 풀에 반납된 시점에 웨이브를 넘기기 위해 OnReturnedToPool을 구독
                Action onReturned = null;
                onReturned = () =>
                {
                    monsterAI.OnReturnedToPool -= onReturned;
                    HandleMonsterDied(health);
                };
                monsterAI.OnReturnedToPool += onReturned;
            }
        }

        // 풀을 타입별로 만들어주는 헬퍼 
        private IGameObjectPool GetOrCreatePool(MonsterWaveDefinitionSO.MonsterEntry entry)
        {
            // MonsterStatsSO((=몬스터 타입)를 key로 등록해둔 pool(그 타입 전용 풀)이 있으면 그걸 꺼내옴
            if (_monsterPools.TryGetValue(entry.Stats, out var pool))
            {
                if (_poolPrefabs[entry.Stats] != entry.PurePrefab)
                    Debug.LogWarning($"[MonsterWaveSpawner] {entry.Stats.name}는 이미 다른 프리팹({_poolPrefabs[entry.Stats].name})으로 풀이 만들어져 있어, 이번 PurePrefab({entry.PurePrefab.name})은 무시됨 on {gameObject.name}", this);
                return pool; // ① 이미 있으면 그대로 반환
            } 

            var scene = this.gameObject.scene;

            // ② 없으면 새로 만들고
            IGameObjectPool newPool = null;
            newPool = new GameObjectPool(
                () => MonsterFactory.CreatePooled(
                    entry.PurePrefab, entry.Stats, newPool, scene),
                    defaultCapacity: 2, maxSize: 8);

            _monsterPools[entry.Stats] = newPool; // ③ 여기서 처음으로 등록(저장)
            _poolPrefabs[entry.Stats] = entry.PurePrefab;
            return newPool;
        }

        private void HandleMonsterDied(Health health)
        {
            _aliveMonsters.Remove(health);
            if (_aliveMonsters.Count == 0)
                SpawnNextWave();
        }
    }
}
