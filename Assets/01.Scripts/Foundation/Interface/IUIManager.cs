using UnityEngine;

namespace MySecondWorld.Network
{
    public interface IUIManager
    {
        GameObject GetOrCreateDynamicCanvas<T>() where T : Component;
    }
}