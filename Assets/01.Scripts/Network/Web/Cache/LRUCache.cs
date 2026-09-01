using System;
using System.Collections.Generic;

namespace MySecondWorld.Network.Web
{
    // 제너릭 베이스
    // “Least Recently Used”가장 최근에 사용되지 않은 데이터를 먼저 제거하는 캐시 관리 알고리즘
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Action<TValue> _onEvict; // Evict = 캐시에서 데이터를 내보내는 동작
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map = new();
        private readonly LinkedList<(TKey Key, TValue Value)> _list = new();

        public LRUCache(int capacity, Action<TValue> onEvict = null)
        {
            _capacity = capacity;
            _onEvict = onEvict;
        }

        // 캐시에서 데이터를 읽어오기
        // 읽기(access) 시 최근 사용 상태를 갱신
        public bool TryGet(TKey key, out TValue value)
        {
            if (!_map.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }

            // 접근할 때마다 head로 이동 (가장 최근 사용)
            // LRU 캐시에서는 가장 최근에 사용된 데이터를 맨 앞에 두고, 가장 오래된 데이터를 맨 뒤에 둔다.
            // 따라서 이 동작은 “이 노드가 방금 사용되었음”을 표시한다.
            _list.Remove(node);
            _list.AddFirst(node); // 제거한 노드를 리스트의 맨 앞(head)에 다시 추가
            value = node.Value.Value;
            return true;
        }

        // 캐시에 데이터를 저장하거나 갱신.
        // 쓰기(insert/update) 시 최근 사용 상태를 갱신하고, 필요하면 오래된 데이터 제거.
        public void Put(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _list.Remove(existing);
                _map.Remove(key);
            }

            // “가장 최근에 사용된 데이터”맵에 등록
            var node = _list.AddFirst((key, value));
            _map[key] = node;

            // 용량 초과시 가장 마지막 노드 제거
            if (_map.Count > _capacity)
                Evict();
        }

        // 리스트의 마지막 노드(가장 오래 안 쓴 것) 제거
        private void Evict()
        {
            var tail = _list.Last;
            if (tail == null) return;

            _list.RemoveLast();
            _map.Remove(tail.Value.Key);
            _onEvict?.Invoke(tail.Value.Value);
        }
    }
}
/*
예시 시나리오
캐시 상태: [A] (A가 맨 앞, 즉 최근 사용됨)

TryGet(A) 실행:

A를 읽고 맨 앞으로 이동 → 여전히 [A]

Put(B) 실행:

B를 새로 추가하고 맨 앞으로 이동 → [B, A]

이제 B가[0] 노드, A는[1] 노드가 됨.
*/