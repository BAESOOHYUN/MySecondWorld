using Mirror;
using UnityEngine;

namespace MySecondWorld.Network.Chat
{
    public class ChatPlayer : NetworkBehaviour
    {
        public override void OnStartLocalPlayer()
        {
            ChatUI.Instance.OnSubmitListener += OnSubmit;
            ChatUI.Instance.OnLocalPlayerReady();
        }
        public override void OnStopLocalPlayer()
        {
            ChatUI.Instance.OnLocalPlayerStopped();
            ChatUI.Instance.OnSubmitListener -= OnSubmit;
        }

        private void OnSubmit(string msg) => CmdSendMessage(msg); // 브릿지

        [Command(requiresAuthority = true)]
        private void CmdSendMessage(string message, NetworkConnectionToClient sender = null)
        {
            Debug.Log($"[Server] CmdSendMessage: {message}");
            Debug.Log($"observers: {netIdentity.observers.Count}");

            if (sender?.authenticationData == null)
                return;

            Debug.Log($"isClient: {isClient}, isServer: {isServer}, netId: {netId}, message: {message}");
            // 서버에서 실행 → 모든 클라이언트에게 브로드캐스트
            string name = (string)sender.authenticationData;
            RpcReceiveMessage(name, message.Trim());
        }


        [ClientRpc]
        private void RpcReceiveMessage(string playerName, string message)
        {
            Debug.Log($"[Client] RpcReceiveMessage: {message}");
            // 모든 클라이언트에서 실행
            ChatUI.Instance.CreateMessagePrefab(playerName, message);
        }

    }
}