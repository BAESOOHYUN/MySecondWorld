using Mirror;

namespace MySecondWorld.Network.Player
{
    public class NetworkCompanionCommands : NetworkBehaviour
    {
        private static GameNetworkManager Manager => (GameNetworkManager)NetworkManager.singleton;

        public void RequestRecruit(string companionId) { if (isOwned) CmdRecruit(companionId); }
        public void RequestKill() { if (isOwned) CmdKill(); }
        public void RequestRevive() { if (isOwned) CmdRevive(); }
        public void RequestDismiss() { if (isOwned) CmdDismiss(); }
        public void RequestSummon() { if (isOwned) CmdSummon(); }
        public void RequestToggleFollow() { if (isOwned) CmdToggleFollow(); }

        [Command]
        private void CmdRecruit(string companionId) => Manager.CompanionService.Recruit(connectionToClient, companionId);

        [Command]
        private void CmdKill() => Manager.CompanionService.Kill(connectionToClient);

        [Command]
        private void CmdRevive() => Manager.CompanionService.Revive(connectionToClient);

        [Command]
        private void CmdDismiss() => Manager.CompanionService.Dismiss(connectionToClient);

        [Command]
        private void CmdSummon() => Manager.CompanionService.Summon(connectionToClient);

        [Command]
        private void CmdToggleFollow() => Manager.CompanionService.ToggleFollow(connectionToClient);
    }
}