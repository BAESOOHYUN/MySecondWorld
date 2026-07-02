using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MySecondWorld.Network.Chat
{
    public class ChatPlayer : NetworkBehaviour
    {
        [Inject] private ChatUI _chatUI;

        public override void OnStartClient()
        {
            LifetimeScope.Find<ProjectLifetimeScope>().Container.Inject(this);
        }

        public override void OnStartLocalPlayer()
        {
            _chatUI.OnSubmitListener += OnSubmit;
            _chatUI.OnLocalPlayerReady();
        }

        public override void OnStopLocalPlayer()
        {
            if (_chatUI == null) return;
            _chatUI.OnLocalPlayerStopped();
            _chatUI.OnSubmitListener -= OnSubmit;
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
            _chatUI.CreateMessagePrefab(playerName, message);
        }

    }
}