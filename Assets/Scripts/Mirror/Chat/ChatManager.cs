using System;
using Mirror;
using UnityEngine;

namespace MySecondWorld.Mirror.Chat
{
    /// <summary>
    /// 네트워크 채팅 로직 담당.
    /// [Command]: 클라이언트 → 서버
    /// [ClientRpc]: 서버 → 전체 클라이언트
    /// </summary>
    public class ChatManager : NetworkBehaviour
    {
        public static event Action<string> OnMessageReceived;

        /// <summary>
        /// ChatUI에서 호출하는 진입점.
        /// Host이면 서버 메서드 직접 호출, 일반 클라이언트이면 Command 전송.
        /// </summary>
        public void TrySendMessage(string message)
        {
            Debug.Log($"[ChatManager] TrySendMessage — isServer={isServer}, isClient={isClient}");
            if (isServer)
                ServerHandleMessage(message, null);
            else
                CmdSendMessage(message);
        }

        /// <summary>
        /// 일반 클라이언트 → 서버
        /// </summary>
        [Command(requiresAuthority = false)]
        private void CmdSendMessage(string message, NetworkConnectionToClient sender = null)
        {
            ServerHandleMessage(message, sender);
        }

        /// <summary>
        /// 서버에서 실행되는 실제 처리 로직
        /// </summary>
        [Server]
        private void ServerHandleMessage(string message, NetworkConnectionToClient sender)
        {
            string senderName = sender != null ? $"Client{sender.connectionId}" : "Host";
            string formatted = $"[{senderName}] {message}";

            // Host(서버+클라이언트)는 observers에 자신이 포함되지 않으므로 UI를 직접 갱신
            OnMessageReceived?.Invoke(formatted);

            // 연결된 원격 클라이언트들에게 RPC 전송
            RpcReceiveMessage(formatted);
        }

        /// <summary>
        /// 서버 → 원격 클라이언트 브로드캐스트.
        /// Host(isServer=true)는 ServerHandleMessage에서 이미 처리했으므로 스킵.
        /// </summary>
        [ClientRpc]
        private void RpcReceiveMessage(string message)
        {
            Debug.Log($"[ChatManager] RpcReceiveMessage — isServer={isServer}, isClient={isClient}, message={message}");
            if (isServer) return; // Host는 위에서 이미 처리 → 중복 방지
            OnMessageReceived?.Invoke(message);
        }
    }
}
