using System;

namespace MySecondWorld.Network
{
    public interface IGameNetworkManager
    {
        event Action OnClientStopped;
        string NetworkAddress { get; set; }
        void SetServerAddress(string address);
        void ToggleHost(bool isOn);
        void ToggleClient(bool isOn);
        void CleanupAndReturnToRobby();
    }
}