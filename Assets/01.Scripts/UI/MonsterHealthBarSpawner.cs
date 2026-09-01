using System.Collections.Generic;
using UnityEngine;
using MySecondWorld.Combat.Monster;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 클라이언트에 스폰된 몬스터마다 HP바를 하나씩 붙인다. 캔버스에 하나만 둔다.
    ///
    /// 개수 걱정을 덜 해도 되는 이유: NetworkClient에 스폰되는 몬스터는
    /// Mirror의 관심 영역(observer)으로 이미 걸러진 근처 개체뿐이다.
    /// </summary>
    public class MonsterHealthBarSpawner : MonoBehaviour
    {
        [SerializeField] private HealthBarUI _barPrefab;

        private readonly Dictionary<MonsterServerState, HealthBarUI> _bars = new();
        private readonly Stack<HealthBarUI> _pool = new();

        private void OnEnable()
        {
            MonsterServerState.ClientSpawned += Attach;
            MonsterServerState.ClientDespawned += Detach;

            // 이 UI가 켜지기 전에 이미 스폰돼 있던 몬스터들을 훑는다.
            // (씬 전환 후 UI가 늦게 살아나는 경우)
            foreach (var state in MonsterServerState.ActiveOnClient)
                Attach(state);
        }

        private void OnDisable()
        {
            MonsterServerState.ClientSpawned -= Attach;
            MonsterServerState.ClientDespawned -= Detach;

            foreach (var bar in _bars.Values)
                Release(bar);
            _bars.Clear();
        }

        private void Attach(MonsterServerState state)
        {
            if (state == null || _barPrefab == null || _bars.ContainsKey(state)) return;

            HealthBarUI bar = _pool.Count > 0 ? _pool.Pop() : Instantiate(_barPrefab, transform);
            bar.gameObject.SetActive(true);
            bar.Bind(state);
            _bars.Add(state, bar);
        }

        private void Detach(MonsterServerState state)
        {
            if (state == null || !_bars.Remove(state, out HealthBarUI bar)) return;
            Release(bar);
        }

        private void Release(HealthBarUI bar)
        {
            bar.Unbind();
            bar.gameObject.SetActive(false);
            _pool.Push(bar);
        }
    }
}