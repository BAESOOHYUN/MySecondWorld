using Mirror;
using UnityEngine;

namespace MySecondWorld.Network
{
    public static class NetworkConnectionExtensions
    {
        // 접속자의 플레이어 오브젝트에서 T 컴포넌트를 찾는다.
        // conn이 없거나, 아직 스폰 전(identity 없음)이거나, 컴포넌트가 없으면 null
        public static T GetPlayerComponent<T>(this NetworkConnectionToClient conn) where T : Component
            => conn?.identity != null ? conn.identity.GetComponent<T>() : null;
    }
}