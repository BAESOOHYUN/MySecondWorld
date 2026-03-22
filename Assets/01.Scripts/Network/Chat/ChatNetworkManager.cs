using Mirror;

namespace MySecondWorld.Network.Chat
{
    public class ChatNetworkManager : NetworkManager
    {
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.authenticationData != null)
            {
                ChatAuthenticator.playerNames.Remove((string)conn.authenticationData);
            }
            // base를 마지막에 부르는 이유: Mirror의 base 구현이 conn 객체를 정리해버려서,
            // base 호출 전에 authenticationData를 먼저 읽어야 한다.
            base.OnServerDisconnect(conn);
        }

        public void ToggleHost(bool isOn)
        {
            if (isOn) StopHost();
            else StartHost();
        }

        public void ToggleClient(bool isOn)
        {
            if (isOn) StopClient();
            else StartClient();
        }
    }
}
