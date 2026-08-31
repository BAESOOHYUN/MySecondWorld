using Mirror;
using UnityEngine;
using MySecondWorld.Network.Player;

namespace MySecondWorld.UI
{
    // 디버그 버튼 → 로컬 플레이어의 PlayerDebugCommands 중계.
    // 버튼은 DontDestroyOnLoad 캔버스에 있고 플레이어는 런타임 스폰이라
    // 인스펙터 사전 연결이 불가능하다. 누를 때마다 로컬 플레이어를 찾아온다.
    public class DebugButtonRelay : MonoBehaviour
    {
        public void OnGrantWeaponTestKitClicked() => WithLocalPlayer(c => c.RequestWeaponTestKit());
        public void OnGrantGoldClicked() => WithLocalPlayer(c => c.RequestStartingGold());

        private void WithLocalPlayer(System.Action<PlayerDebugCommands> action)
        {
            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null)
            {
                Debug.LogWarning("[Debug] 로컬 플레이어가 아직 스폰되지 않았습니다");
                return;
            }
            if (!localPlayer.TryGetComponent(out PlayerDebugCommands cmds))
            {
                Debug.LogWarning("[Debug] 플레이어 프리팹에 PlayerDebugCommands가 없습니다", localPlayer);
                return;
            }
            action(cmds);
        }
    }
}