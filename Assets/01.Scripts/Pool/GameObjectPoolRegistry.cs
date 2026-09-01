using System;
using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Pool
{
    // "키 하나당 풀 하나"를 관리하는 색인.
    //
    // GameObjectPool과는 역할이 다르다 —
    //   GameObjectPool             : 한 종류의 인스턴스를 재사용한다
    //   GameObjectPoolRegistry     : 종류별 풀을 만들어두고 골라준다 (+ 한꺼번에/조건부로 정리한다)
    // 그래서 상속이 아니라 포함 관계다. 레지스트리는 풀의 한 종류가 아니라 풀들의 목록이다.
    //
    // 키를 제네릭으로 둔 이유: 호출부마다 기준이 다르다.
    //   투사체 → (프리팹, 씬)   /   몬스터 웨이브 → 스탯 SO
    public class GameObjectPoolRegistry<TKey> : IDisposable
    {
        private readonly Dictionary<TKey, IGameObjectPool> _pools = new Dictionary<TKey, IGameObjectPool>();
        private readonly int _defaultCapacity;
        private readonly int _maxSize;

        public GameObjectPoolRegistry(int defaultCapacity = 4, int maxSize = 32)
        {
            _defaultCapacity = defaultCapacity;
            _maxSize = maxSize;
        }

        // factory는 "그 풀에 소속된 인스턴스 하나"를 만든다.
        // 인자로 풀을 넘겨주는 이유: PooledObject.Init(pool)이 자기 소속을 알아야 스스로 반납할 수 있는데,
        // 그러려면 "풀을 만들면서 그 풀을 참조"해야 한다. 호출부마다 반복되던 그 요령을 여기 한 곳에 가둔다.
        public GameObject Rent(TKey key, Func<IGameObjectPool, GameObject> factory)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                IGameObjectPool created = null;
                created = new GameObjectPool(() => factory(created), _defaultCapacity, _maxSize);
                _pools[key] = created;
                pool = created;
            }
            return pool.Rent();
        }

        // 조건에 맞는 풀만 정리한다. 씬 언로드처럼 "일부만 수명이 끝나는" 경우용.
        public int DisposeWhere(Func<TKey, bool> predicate)
        {
            var doomed = new List<TKey>();
            foreach (var key in _pools.Keys)
                if (predicate(key)) doomed.Add(key);

            foreach (var key in doomed)
            {
                _pools[key].Dispose();
                _pools.Remove(key);
            }
            return doomed.Count;
        }

        public void Dispose()
        {
            foreach (var pool in _pools.Values)
                pool.Dispose();
            _pools.Clear();
        }
    }
}