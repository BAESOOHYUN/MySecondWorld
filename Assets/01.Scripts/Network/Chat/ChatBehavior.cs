using UnityEngine;
using Mirror;
using TMPro;

namespace MySecondWorld.Network.Chat
{
    public class ChatBehavior : NetworkBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _messageContainer;  // ScrollView > Viewport > Content
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private TMP_InputField _localPlayerNameInput;
        [SerializeField] private TMP_Text _messageTextPrefab;

        [Header("Client Username")]
        public static string localPlayerName;

        public override void OnStartLocalPlayer()
        {
            _messageInput.onSubmit.AddListener(OnSubmit);
            _localPlayerNameInput.onSubmit.AddListener(SetPlayerName);
        }

        public override void OnStopLocalPlayer()
        {
            _messageInput.onSubmit.RemoveListener(OnSubmit);
            _localPlayerNameInput.onSubmit.RemoveListener(SetPlayerName);
            _localPlayerNameInput.interactable = true;
        }


        [Command(requiresAuthority = false)]
        void CmdSendMessage(string message, NetworkConnectionToClient sender = null)
        {
            Debug.Log($"[Server] CmdSendMessage: {message}");
            Debug.Log($"observers: {netIdentity.observers.Count}");

            if (sender?.authenticationData == null) 
                return;

            // 서버에서 실행 → 모든 클라이언트에게 브로드캐스트
            string name = (string)sender.authenticationData;
            RpcReceiveMessage(name, message.Trim());
        }

        [ClientRpc]
        void RpcReceiveMessage(string playerName, string message)
        {
            Debug.Log($"[Client] RpcReceiveMessage: {message}");
            // 모든 클라이언트에서 실행
            if (_messageTextPrefab == null || _messageContainer == null)
            {

                Debug.LogWarning("prefab or container is null");
                return;
            }

            TMP_Text entry = Instantiate(_messageTextPrefab, _messageContainer);

            if (playerName == localPlayerName && NetworkServer.active == true) // "HOST"일때
            {
                entry.text = $"<color=red>{playerName}:</color> {message}";
            }
            else if (playerName == localPlayerName) // "나"일때
            {
                entry.text = $"<color=blue>{playerName}:</color> {message}";
            }
            else // "다른 클라이언트"
            {
                entry.text = $"<color=green>{playerName}:</color> {message}";
            }
            Debug.Log($"entry   {entry != null}");
            //entry.text = message;
        }

        public void OnSubmit(string msg)
        {
            msg = msg.Trim();
            if (string.IsNullOrWhiteSpace(msg)) return;

            Debug.Log($"isClient: {isClient}, isServer: {isServer}, netId: {netId}, msg: {msg}");
            CmdSendMessage(msg);
            _messageInput.text = string.Empty;
            _messageInput.ActivateInputField();
        }

        // 지금은 InputField에서 받아와 쓰고,
        // TODO : username을 관리할 로그인단, 서버 구상
        public void SetPlayerName(string username)
        {
            localPlayerName = username;
            _localPlayerNameInput.interactable = false;
        }
    }
}