using System;

namespace MySecondWorld.Network
{
    public interface IGameNetworkManager
    {
        event Action OnClientStopped;
        void ToggleHost(bool isOn);
        void ToggleClient(bool isOn);
    }
}