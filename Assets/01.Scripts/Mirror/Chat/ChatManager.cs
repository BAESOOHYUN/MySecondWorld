using System;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Mirror.Chat
{
    /// <summary>
    /// [Command] : 클라이언트 → 서버
    /// NetworkServer.SendToAll : 서버 → 전체 (observer 의존 없이 직접 전달)
    /// </summary>
    public struct ChatMessage : NetworkMessage
    {
        public string content;
    }

    public class ChatManager : NetworkBehaviour
    {
        public static event Action<string> OnMessageReceived;

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<ChatMessage>(OnNetworkChatMessage);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<ChatMessage>();
        }

        private void OnNetworkChatMessage(ChatMessage msg)
        {
            OnMessageReceived?.Invoke(msg.content);
        }

        /// <summary>
        /// ChatUI에서 호출하는 진입점.
        /// Host이면 서버 메서드 직접 호출, 일반 클라이언트이면 Command 전송.
        /// </summary>
        public void TrySendMessage(string message)
        {
            if (isServer)
                ServerHandleMessage(message, null);
            else
                CmdSendMessage(message);
        }

        [Command(requiresAuthority = false)]
        private void CmdSendMessage(string message, NetworkConnectionToClient sender = null)
        {
            ServerHandleMessage(message, sender);
        }

        [Server]
        private void ServerHandleMessage(string message, NetworkConnectionToClient sender)
        {
            string senderName = sender != null ? $"Client{sender.connectionId}" : "Host";
            string formatted = $"[{senderName}] {message}";

            // observer 의존 없이 모든 연결에 직접 전달 (Host 로컬 커넥션 포함)
            NetworkServer.SendToAll(new ChatMessage { content = formatted });
        }
    }
}
