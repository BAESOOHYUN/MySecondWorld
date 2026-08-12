using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MySecondWorld.Network.Companion
{
    // 테스트용 임시 스크립트 — RecruitCompanion() 인게임 진입점이 아직 없어서 검증용으로만 추가.
    // 실제 영입 트리거(퀘스트/이벤트)가 생기면 이 파일은 삭제.
    public class CompanionDebugRecruiter : NetworkBehaviour
    {
        private bool _isReady;

        public override void OnStartLocalPlayer()
        {
            _isReady = true;
        }

        private void Update()
        {
            if (!_isReady) return;
            if (Keyboard.current.f8Key.wasPressedThisFrame)
                CmdToggleCompanionFollow();
            if (Keyboard.current.f9Key.wasPressedThisFrame)
                CmdRecruitDebugCompanion("Ari");
            if (Keyboard.current.f10Key.wasPressedThisFrame)
                CmdKillDebugCompanion();
            if (Keyboard.current.f11Key.wasPressedThisFrame)
                CmdReviveDebugCompanion();
        }

        [Command]
        private void CmdRecruitDebugCompanion(string companionId)
        {
            var manager = (GameNetworkManager)NetworkManager.singleton;
            manager.DebugRecruitCompanion(connectionToClient, companionId);
        }

        [Command]
        private void CmdKillDebugCompanion()
        {
            var manager = (GameNetworkManager)NetworkManager.singleton;
            manager.DebugKillCompanion(connectionToClient);
        }

        [Command]
        private void CmdReviveDebugCompanion()
        {
            var manager = (GameNetworkManager)NetworkManager.singleton;
            manager.DebugReviveCompanion(connectionToClient);
        }

        [Command]
        private void CmdToggleCompanionFollow()
        {
            var manager = (GameNetworkManager)NetworkManager.singleton;
            manager.DebugToggleCompanionFollow(connectionToClient);
        }
    }
}
