using System.Collections.Generic;
using UnityEngine;
using MySecondWorld.Network;
using MySecondWorld.Pool;

namespace MySecondWorld.UI
{
    // 스폰마다 하나씩 붙이는 쪽
    public class NameTagSpawner : MonoBehaviour
    {
        [SerializeField] private NameTagUI _prefab;

        private readonly Dictionary<NameTag, NameTagUI> _tags = new();
        private GameObjectPool _pool;

        // ClientSpawned는 OnStartClient에서 불리고, 그 시점에 SyncVar가 이미 들어와 있어서 Bind에서 DisplayName을 바로 읽어도 된다.
        private void OnEnable()
        {
            _pool ??= new GameObjectPool(() => Instantiate(_prefab, transform).gameObject);
            NameTag.ClientSpawned += Attach;
            NameTag.ClientDespawned += Detach;

            foreach (var tag in NameTag.ActiveOnClient)
                Attach(tag);
        }

        private void OnDisable()
        {

            NameTag.ClientSpawned -= Attach;
            NameTag.ClientDespawned -= Detach;

            foreach (var ui in _tags.Values)
                Release(ui);
            _tags.Clear();
        }

        private void OnDestroy() => _pool?.Dispose();

        private void Attach(NameTag tag)
        {
            if (tag == null || _prefab == null || _tags.ContainsKey(tag)) return;

            NameTagUI ui = _pool.Rent().GetComponent<NameTagUI>();
            ui.Bind(tag);
            _tags.Add(tag, ui);
        }

        private void Detach(NameTag tag)
        {
            if (tag == null || !_tags.Remove(tag, out NameTagUI ui)) return;
            Release(ui);
        }

        private void Release(NameTagUI ui)
        {
            ui.Unbind();
            _pool.Return(ui.gameObject);
        }
    }
}