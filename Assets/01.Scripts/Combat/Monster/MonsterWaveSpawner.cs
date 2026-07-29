using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MySecondWorld.Combat.Monster
{
    // 파수를 순서대로 소환하고, 파수 내 전멸 시 다음 파수로 넘어간다. 마지막 파수 클리어 시 이벤트 발행.
    // room 식별자는 그냥 실어 나르기만 함 — 어떤 room 시스템(던전/필드)인지는 몰라도 됨(DungeonRoomRegistry 직접 참조 금지).
    public class MonsterWaveSpawner : MonoBehaviour
    {
        [SerializeField] private MonsterWaveDefinitionSO _waveDefinition;
        [SerializeField] private Transform[] _spawnPoints; // 한 파수의 몬스터 수만큼 순환 사용

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
            _aliveMonsters.Clear();

            for (int i = 0; i < wave.Monsters.Length; i++)
            {
                var entry = wave.Monsters[i];
                var spawnPoint = _spawnPoints[i % _spawnPoints.Length];
                var instance = MonsterFactory.Create(entry.PurePrefab, entry.Stats, spawnPoint.position, spawnPoint.rotation);
                // Instantiate는 기본적으로 활성 씬(Active Scene)에 생성하므로, 스포너가 있는 씬(FieldScene 등)으로 옮겨줌
                SceneManager.MoveGameObjectToScene(instance, gameObject.scene);

                var health = instance.GetComponent<Health>();
                _aliveMonsters.Add(health);
                health.OnDied += () => HandleMonsterDied(health);
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
