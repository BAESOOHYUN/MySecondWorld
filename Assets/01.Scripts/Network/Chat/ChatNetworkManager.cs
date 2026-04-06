using Mirror;
using UnityEngine;

namespace MySecondWorld.Network.Chat
{
    public class ChatNetworkManager : NetworkManager
    {
        public override void OnDestroy()
        {
            if (NetworkServer.active) 
                StopHost();
            else if (NetworkClient.isConnected) 
                StopClient();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.authenticationData != null)
            {
                PlayerAuthenticator.playerNames.Remove((string)conn.authenticationData);
            }
            // base를 마지막에 부르는 이유: Mirror의 base 구현이 conn 객체를 정리해버려서,
            // base 호출 전에 authenticationData를 먼저 읽어야 한다.
            base.OnServerDisconnect(conn);
        }

        public void ToggleHost(bool isOn)
        {
            Debug.Log($"[NM] ToggleHost isOn={isOn}");
            if (isOn) StartHost();
            else StopHost();
        }

        public void ToggleClient(bool isOn)
        {
            Debug.Log($"[NM] ToggleClient isOn={isOn}");
            if (isOn) StartClient();
            else StopClient();
        }

        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            base.OnServerReady(conn);
            Debug.Log($"[NM] OnServerReady conn={conn.connectionId}");
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);
            Debug.Log($"[NM] OnServerAddPlayer conn={conn.connectionId}, playerPrefab={playerPrefab?.name ?? "NULL"}");
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("[NM] OnClientConnect");
        }
    }
}
