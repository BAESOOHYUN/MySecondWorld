using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
using MySecondWorld.Network;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat.Monster
{
    public enum SpawnMode
    {
        FieldIndividual, // 필드: 각 몬스터 사망 시 N초 후 반경 내 랜덤 위치에 개별 리스폰 (무한)
        DungeonWave,     // 던전: 웨이브 단위 진행 (전멸 시 다음 웨이브)
    }

    // 웨이브를 순서대로 소환하고, 웨이브 내 전멸 시 다음 웨이브로 넘어간다. 마지막 웨이브 클리어 시 이벤트 발행.
    // room 식별자는 그냥 실어 나르기만 함 — 어떤 room 시스템(던전/필드)인지는 몰라도 됨(DungeonRoomRegistry 직접 참조 금지).
    public class MonsterWaveSpawner : MonoBehaviour
    {
        [Header("스폰 모드 설정")]
        [SerializeField] private SpawnMode _spawnMode = SpawnMode.FieldIndividual;
        [SerializeField] private MonsterWaveDefinitionSO _waveDefinition;
        [Header("스폰 영역 설정 (마비노기 스타일)")]
        [Tooltip("스폰 중심 지점들 (비워두면 이 스포너 자신의 위치가 중심이 됨)")]
        [SerializeField] private Transform[] _spawnPoints; // 한 웨이브의 몬스터 수만큼 순환 사용
        [Tooltip("스폰 중심점 기준 몬스터가 흩어져서 생성될 반경 (m)")]
        [SerializeField] private float _spawnRadius = 5f;
        [Header("공통 설정")]
        [Tooltip("서버 시작 시 자동 스폰 여부 (필드는 true, 던전 트리거형은 false)")]
        [SerializeField] private bool _autoStartOnServer = true;
        [Header("필드 모드(FieldIndividual) 설정")]
        [Tooltip("몬스터 사망 후 개별 리스폰까지의 대기 시간(초)")]
        [SerializeField] private float _individualRespawnDelay = 5f;
        [Header("던전 모드(DungeonWave) 설정")]
        [Tooltip("던전 모든 웨이브 클리어 시 처음부터 무한 반복할지 여부")]
        [SerializeField] private bool _loopDungeonWaves = false;
        [Tooltip("웨이브 간 또는 던전 리셋 대기 시간(초)")]
        [SerializeField] private float _waveDelay = 3f;
        public event Action<string> OnAllWavesCleared;

        // 몬스터 타입(스탯 SO)별 전용 풀 — 웨이브를 돌면서 같은 타입 몬스터를 재사용
        private readonly Dictionary<MonsterStatsSO, IGameObjectPool> _monsterPools = new();
        // GetOrCreatePool에서 같은 Stats에 다른 PurePrefab이 들어오는 실수를 잡아내기 위한 보조 기록
        private readonly Dictionary<MonsterStatsSO, GameObject> _poolPrefabs = new();
        private int _currentWaveIndex = -1;
        private readonly List<Health> _aliveMonsters = new();
        private string _roomId;

        private void Start()
        {
            // Additive 씬은 모든 클라에도 로드됨 — 가드 없으면 클라마다 독립적으로 중복 스폰함
            if (!NetworkServer.active) return;

            if (_autoStartOnServer)
                StartWaves(gameObject.scene.path);
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
            if (_waveDefinition == null)
            {
                Debug.LogError($"[MonsterWaveSpawner] _waveDefinition이 비어있음 on {gameObject.name}", this);
                return;
            }

            _roomId = roomId;

            if (_spawnMode == SpawnMode.FieldIndividual)
            {
                StartFieldMode();
            }
            else
            {
                _currentWaveIndex = -1;
                SpawnNextDungeonWave();
            }
        }
        #region 필드 모드 (개별 반경 랜덤 리스폰)
        private void StartFieldMode()
        {
            if (_waveDefinition.Waves.Count == 0) return;
            var wave = _waveDefinition.Waves[0];
            for (int i = 0; i < wave.Monsters.Count; i++)
            {
                var entry = wave.Monsters[i];
                Vector3 centerPos = GetCenterPosition(i);
                SpawnSingleMonster(entry, centerPos);
            }
        }
        private void SpawnSingleMonster(MonsterWaveDefinitionSO.MonsterEntry entry, Vector3 centerPos)
        {
            if (!NetworkServer.active || entry.PurePrefab == null || entry.Stats == null) return;
            try
            {
                var pool = GetOrCreatePool(entry);
                var instance = pool.Rent();
                if (instance.TryGetComponent(out NetworkMatch match))
                    match.matchId = SceneMatchId.GetMatchIdFromScene(_roomId);
                // 반경 내 유효한 NavMesh 랜덤 좌표 및 랜덤 회전 계산
                Vector3 spawnPos = GetRandomNavMeshPosition(centerPos, _spawnRadius);
                Quaternion spawnRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                var monsterAI = instance.GetComponent<MonsterAI>();
                monsterAI.PrepareForSpawn(spawnPos, spawnRot);
                var health = instance.GetComponent<Health>();
                _aliveMonsters.Add(health);
                // 사망 후 반납 시 -> N초 후 해당 중심 반경 내 랜덤 위치로 다시 리스폰
                Action onReturned = null;
                onReturned = () =>
                {
                    monsterAI.OnReturnedToPool -= onReturned;
                    _aliveMonsters.Remove(health);
                    RespawnSingleAsync(entry, centerPos, destroyCancellationToken).Forget();
                };
                monsterAI.OnReturnedToPool += onReturned;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MonsterWaveSpawner] 필드 몬스터 스폰 실패: {e}", this);
            }
        }
        private async UniTaskVoid RespawnSingleAsync(MonsterWaveDefinitionSO.MonsterEntry entry, Vector3 centerPos, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_individualRespawnDelay), cancellationToken: ct);
                if (!NetworkServer.active) return;
                SpawnSingleMonster(entry, centerPos);
            }
            catch (OperationCanceledException) { }
        }
        #endregion
        #region 던전 모드 (웨이브 반경 랜덤 스폰)
        private void SpawnNextDungeonWave()
        {
            _currentWaveIndex++;
            if (_currentWaveIndex >= _waveDefinition.Waves.Count)
            {
                OnAllWavesCleared?.Invoke(_roomId);
                if (_loopDungeonWaves)
                {
                    LoopDungeonAsync(destroyCancellationToken).Forget();
                }
                return;
            }
            var wave = _waveDefinition.Waves[_currentWaveIndex];
            if (wave.Monsters == null || wave.Monsters.Count == 0)
            {
                SpawnNextDungeonWave();
                return;
            }
            _aliveMonsters.Clear();
            for (int i = 0; i < wave.Monsters.Count; i++)
            {
                try
                {
                    var entry = wave.Monsters[i];
                    if (entry.PurePrefab == null || entry.Stats == null) continue;
                    Vector3 centerPos = GetCenterPosition(i);
                    var pool = GetOrCreatePool(entry);
                    var instance = pool.Rent();
                    if (instance.TryGetComponent(out NetworkMatch match))
                        match.matchId = SceneMatchId.GetMatchIdFromScene(_roomId);
                    Vector3 spawnPos = GetRandomNavMeshPosition(centerPos, _spawnRadius);
                    Quaternion spawnRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    var monsterAI = instance.GetComponent<MonsterAI>();
                    monsterAI.PrepareForSpawn(spawnPos, spawnRot);
                    var health = instance.GetComponent<Health>();
                    _aliveMonsters.Add(health);
                    Action onReturned = null;
                    onReturned = () =>
                    {
                        monsterAI.OnReturnedToPool -= onReturned;
                        _aliveMonsters.Remove(health);
                        if (_aliveMonsters.Count == 0)
                        {
                            DelayedNextWaveAsync(destroyCancellationToken).Forget();
                        }
                    };
                    monsterAI.OnReturnedToPool += onReturned;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MonsterWaveSpawner] 던전 웨이브 {_currentWaveIndex} 스폰 오류: {e}", this);
                }
            }
        }
        private async UniTaskVoid DelayedNextWaveAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_waveDelay), cancellationToken: ct);
                if (NetworkServer.active)
                    SpawnNextDungeonWave();
            }
            catch (OperationCanceledException) { }
        }
        private async UniTaskVoid LoopDungeonAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_waveDelay), cancellationToken: ct);
                if (!NetworkServer.active) return;
                _currentWaveIndex = -1;
                SpawnNextDungeonWave();
            }
            catch (OperationCanceledException) { }
        }
        #endregion
        #region 랜덤 위치 & NavMesh 유틸리티
        private Vector3 GetCenterPosition(int index)
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var pt = _spawnPoints[index % _spawnPoints.Length];
                if (pt != null) return pt.position;
            }
            return transform.position;
        }
        /// <summary>
        /// 중심 위치 기준으로 반경 내 NavMesh 상의 유효한 랜덤 좌표를 추출
        /// </summary>
        private Vector3 GetRandomNavMeshPosition(Vector3 center, float radius)
        {
            if (radius <= 0f) return center;
            // 원 내부 무작위 2D 점 -> 3D XZ 평면 변환
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidatePos = center + new Vector3(randomCircle.x, 0f, randomCircle.y);
            // NavMesh 상의 가장 가까운 유효 지면 샘플링 (반경 3m 내 탐색)
            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return center; // 탐색 실패 시 중심 위치로 폴백
        }
        #endregion
        #region 에디터 기즈모 (스폰 반경 시각화)
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                foreach (var pt in _spawnPoints)
                {
                    if (pt != null)
                    {
                        Gizmos.DrawWireSphere(pt.position, _spawnRadius);
                    }
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, _spawnRadius);
            }
        }
        #endregion
        #region 풀 생성 헬퍼
        private IGameObjectPool GetOrCreatePool(MonsterWaveDefinitionSO.MonsterEntry entry)
        {
            if (_monsterPools.TryGetValue(entry.Stats, out var pool))
                return pool;
            var scene = this.gameObject.scene;
            IGameObjectPool newPool = null;
            newPool = new GameObjectPool(
                () => MonsterFactory.CreatePooled(entry.PurePrefab, entry.Stats, newPool, scene),
                defaultCapacity: 2, maxSize: 8);
            _monsterPools[entry.Stats] = newPool;
            _poolPrefabs[entry.Stats] = entry.PurePrefab;
            return newPool;
        }
        #endregion
    }
}