using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MySecondWorld.Combat.Monster
{
    // 웨이브를 순서대로 소환하고, 웨이브 내 전멸 시 다음 웨이브로 넘어간다. 마지막 웨이브 클리어 시 이벤트 발행.
    // room 식별자는 그냥 실어 나르기만 함 — 어떤 room 시스템(던전/필드)인지는 몰라도 됨(DungeonRoomRegistry 직접 참조 금지).
    public class MonsterWaveSpawner : MonoBehaviour
    {
        [SerializeField] private MonsterWaveDefinitionSO _waveDefinition;
        [SerializeField] private Transform[] _spawnPoints; // 한 웨이브의 몬스터 수만큼 순환 사용

        [Header("테스트용")]
        [SerializeField] private bool _autoStartForTesting;
        [SerializeField] private string _testRoomId = "test-room";

        public event Action<string> OnAllWavesCleared;

        private int _currentWaveIndex = -1;
        private readonly List<Health> _aliveMonsters = new List<Health>();
        private string _roomId;

        private void Start()
        {
            if (_autoStartForTesting)
                StartWaves(_testRoomId);
        }

        public void StartWaves(string roomId)
        {
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
                var spawnPoint = _spawnPoints[i % _spawnPoints.Length];
                var instance = MonsterFactory.Create(entry.PurePrefab, entry.Stats, spawnPoint.position, spawnPoint.rotation);
                if (instance == null) continue; // 프리팹/스탯 누락 — MonsterFactory가 이미 에러 로그를 남김

                // Instantiate는 기본적으로 활성 씬(Active Scene)에 생성하므로, 스포너가 있는 씬(FieldScene 등)으로 옮겨줌
                SceneManager.MoveGameObjectToScene(instance, gameObject.scene);

                var health = instance.GetComponent<Health>();
                _aliveMonsters.Add(health);

                // 자기 자신을 구독 해제하는 델리게이트 — 죽은 몬스터는 Destroy가 아니라 SetActive(false)로만 남아서
                // 명시적으로 해제 안 하면 이 구독이 계속 메모리에 쌓임
                Action onDied = null;
                onDied = () =>
                {
                    health.OnDied -= onDied;
                    HandleMonsterDied(health);
                };
                health.OnDied += onDied;
            }
        }

        private void HandleMonsterDied(Health health)
        {
            _aliveMonsters.Remove(health);
            if (_aliveMonsters.Count == 0)
                SpawnNextWave();
        }
    }
}
