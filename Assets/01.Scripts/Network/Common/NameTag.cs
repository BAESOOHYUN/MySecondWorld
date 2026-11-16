using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Network
{
    public class NameTag : NetworkBehaviour
    {
        [SerializeField, Tooltip("NPC처럼 프리팹에 고정된 이름. 플레이어는 비워 둔다")]
        private string _defaultName;
        [SyncVar] private string _displayName;
        public string DisplayName => _displayName;
        public Transform Anchor => transform;

        private static readonly List<NameTag> _activeOnClient = new();
        public static IReadOnlyList<NameTag> ActiveOnClient => _activeOnClient;
        public static event Action<NameTag> ClientSpawned;
        public static event Action<NameTag> ClientDespawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _activeOnClient.Clear();
            ClientSpawned = null;
            ClientDespawned = null;
        }

        public override void OnStartServer()
        {
            if (!string.IsNullOrEmpty(_defaultName))
                _displayName = _defaultName;
        }

        [Server]
        public void ServerSetName(string name) => _displayName = name;

        public override void OnStartClient()
        {
            _activeOnClient.Add(this);
            ClientSpawned?.Invoke(this);
        }

        public override void OnStopClient()
        {
            _activeOnClient.Remove(this);
            ClientDespawned?.Invoke(this);
        }
    }
}