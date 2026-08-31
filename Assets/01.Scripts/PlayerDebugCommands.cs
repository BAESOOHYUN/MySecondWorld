using Mirror;
using UnityEngine.InputSystem;

namespace MySecondWorld.Network.Player
{
    // 테스트용 임시 스크립트 — 골드 지급/동행 영입의 실제 인게임 트리거(퀘스트/이벤트/상점)가
    // 생기면 이 파일은 삭제한다.
    // 버튼이 부를 수 있도록 public 진입점을 두고, 에디터 편의용 단축키도 함께 유지한다.
    public class PlayerDebugCommands : NetworkBehaviour
    {
        private static GameNetworkManager Manager => (GameNetworkManager)NetworkManager.singleton;

        #region 버튼 진입점 (DebugButtonRelay가 호출)

        public void RequestStartingGold() { if (isOwned) CmdGrantStartingGold(); }

        public void RequestWeaponTestKit() { if (isOwned) CmdGrantWeaponTestKit(); }

        #endregion

        // 안드로이드는 하드웨어 키보드가 없으면 Keyboard.current가 null이라 반드시 가드해야 한다.
        private void Update()
        {
            if (!isLocalPlayer) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f7Key.wasPressedThisFrame) RequestStartingGold();
            if (keyboard.f12Key.wasPressedThisFrame) RequestWeaponTestKit();
        }

        #region Command (서버에서 실행)

        [Command]
        private void CmdGrantStartingGold()
        {
            Manager.DebugGrantStartingGold(connectionToClient);
        }

        [Command]
        private void CmdGrantWeaponTestKit()
        {
            Manager.DebugGrantWeaponTestKit(connectionToClient);
        }
        #endregion
    }
}