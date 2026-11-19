using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using MySecondWorld.Pool;
using MySecondWorld.Network.Player;

namespace MySecondWorld.Combat.Monster
{
    // 스킬/전투 테스트를 위한 무한 체력 샌드백(더미) 몬스터 스폰 매니저
    public static class MonsterDummySpawner
    {
        private static IGameObjectPool _dummyPool;
        private static GameObject _cachedPrefab;
        private static MonsterStatsSO _cachedStats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitBefore() => Init();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            PlayerDebugCommands.OnSpawnDummyRequested -= HandleSpawnDummy;
            PlayerDebugCommands.OnSpawnDummyRequested += HandleSpawnDummy;
            Debug.Log("[MonsterDummySpawner] 📢 Init 완료: OnSpawnDummyRequested 이벤트 구독됨");
        }

        private static void HandleSpawnDummy(Vector3 position, Quaternion rotation, Guid matchId, Scene scene)
        {
            Debug.Log($"[MonsterDummySpawner] 📥 HandleSpawnDummy 호출됨: pos={position}, matchId={matchId}, scene={scene.name}, isServer={NetworkServer.active}");

            if (!NetworkServer.active)
            {
                Debug.LogWarning("[MonsterDummySpawner] 더미 스폰은 서버에서만 실행 가능합니다.");
                return;
            }

            if (_cachedStats == null)
                _cachedStats = Resources.Load<MonsterStatsSO>("Monster/MonsterDummySO");

            if (_cachedPrefab == null)
                _cachedPrefab = Resources.Load<GameObject>("Monster/MonsterDummyPrefab");

            Debug.Log($"[MonsterDummySpawner] 📦 에셋 로드 확인: Stats={(_cachedStats != null ? _cachedStats.name : "NULL")}, Prefab={(_cachedPrefab != null ? _cachedPrefab.name : "NULL")}");

            if (_cachedStats == null || _cachedPrefab == null)
            {
                Debug.LogError($"[MonsterDummySpawner] ❌ 에셋 로드 실패! Stats: {_cachedStats}, Prefab: {_cachedPrefab}");
                return;
            }

            Scene targetScene = (scene.IsValid() && scene.isLoaded) ? scene : SceneManager.GetActiveScene();

            if (_dummyPool == null)
            {
                _dummyPool = new GameObjectPool(
                    () => MonsterFactory.CreatePooled(_cachedPrefab, _cachedStats, _dummyPool, targetScene),
                    defaultCapacity: 2, maxSize: 10);
            }

            try
            {
                var instance = _dummyPool.Rent();
                if (instance == null)
                {
                    Debug.LogError("[MonsterDummySpawner] ❌ 풀에서 인스턴스 획득 실패");
                    return;
                }

                if (targetScene.IsValid() && targetScene.isLoaded && instance.scene != targetScene)
                {
                    if (instance.transform.parent != null)
                        instance.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(instance, targetScene);
                }

                if (instance.TryGetComponent(out NetworkMatch match))
                {
                    match.matchId = matchId;
                    Debug.Log($"[MonsterDummySpawner] 🏷️ 샌드백 NetworkMatch 설정 완료: matchId={matchId}");
                }

                var monsterAI = instance.GetComponent<MonsterAI>();
                if (monsterAI != null)
                {
                    monsterAI.PrepareForSpawn(position, rotation);
                }

                uint netId = instance.TryGetComponent<NetworkIdentity>(out var nid) ? nid.netId : 0;
                Debug.Log($"[MonsterDummySpawner] 🎯 샌드백 더미 몬스터 소환 완료! NetId={netId}, 위치={position}, MatchId={matchId}, Scene={instance.scene.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MonsterDummySpawner] ❌ 샌드백 소환 중 예외 발생: {ex}");
            }
        }
    }
}
