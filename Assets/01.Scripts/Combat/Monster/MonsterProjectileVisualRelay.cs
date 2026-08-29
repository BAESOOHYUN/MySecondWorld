using Mirror;
using MySecondWorld.Pool;
using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // EnemyProjectile은 순수 로컬(판정 전용)이라 서버 화면에만 보인다.
    // 이 컴포넌트는 "발사했다"는 사실만 ClientRpc로 알려서, 각 클라가 판정 없는
    // 순수 시각 이펙트를 자기 화면에서 독립 재생하게 한다 — 히트 판정은 여전히 서버 Projectile 전담.
    public class MonsterProjectileVisualRelay : NetworkBehaviour
    {
        [SerializeField] private GameObject _visualProjectilePrefab; // RangedMonsterStatsSO.ProjectilePrefab과 동일한 순수 시각 프리팹 재사용 가능
        [SerializeField] private float _visualLifetime = 3f;

        // 프리팹 종류당 공유 풀 — 몬스터 인스턴스가 여러 마리라도 같은 모양 화살이면 재사용을 같이 쓴다
        private static readonly Dictionary<GameObject, IGameObjectPool> _pools = new Dictionary<GameObject, IGameObjectPool>();

        // Domain Reload OFF라 static이 세션 간 안 지워짐 — 이전 Play 세션이 남긴 죽은 참조 청소
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPoolsOnNewSession() => _pools.Clear();


        [ClientRpc]
        public void RpcPlayProjectileVisual(Vector3 origin, Vector3 direction, float speed)
        {
            if (isServer) return; // 호스트=서버는 Projectile 실물이 이미 있어 중복 재생 방지
            if (_visualProjectilePrefab == null) return;
            
            var pool = GetOrCreatePool();
            var fx = pool.Rent();
            fx.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            fx.GetComponent<ProjectileVisualMover>().Setup(direction, speed, _visualLifetime);
        }

        private IGameObjectPool GetOrCreatePool()
        {
            if (_pools.TryGetValue(_visualProjectilePrefab, out var pool))
                return pool;

            var prefab = _visualProjectilePrefab;
            IGameObjectPool newPool = null;
            newPool = new GameObjectPool(() =>
            {
                var instance = Instantiate(prefab);
                instance.AddComponent<ProjectileVisualMover>();
                instance.AddComponent<PooledObject>().Init(newPool);
                return instance;
            }, defaultCapacity: 4, maxSize: 20);

            _pools[_visualProjectilePrefab] = newPool;
            return newPool;
        }
    }
}