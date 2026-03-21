using System;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Mirror.Chat
{
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
        public void TrySendMessage(string message)
        {
            if (isServer)
                ServerHandleMessage(message, null); // Host: 직접 호출
            else
                CmdSendMessage(message);            // 일반 클라이언트: Command 전송
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
            var msg = new ChatMessage { content = formatted };

            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn.connectionId == 0) continue; // Host 로컬 커넥션 제외
                conn.Send(msg);
            }

            if (!isServerOnly)
                OnMessageReceived?.Invoke(formatted);
        }
    }
}
