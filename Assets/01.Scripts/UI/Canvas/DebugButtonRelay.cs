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
        public void OnGrantGoldClicked() => WithLocalPlayer(c => c.RequestStartingGold());
        public void OnGrantWeaponTestKitClicked() => WithLocalPlayer(c => c.RequestWeaponTestKit());
        public void OnGrantArmorTestKitClicked() => WithLocalPlayer(c => c.RequestArmorTestKit());

        // 동작 실패 원인
        // 클라이언트 버튼 클릭 ➜ CmdGrantArmorTestKit 패킷을 AWS EC2 서버로 전송.
        // 하지만 AWS EC2 서버(mysecondworld.x86_64) 는 이전 빌드 상태이므로, CmdGrantArmorTestKit 명령어를 알지 못해 인벤토리에 아이템을 지급하지 못했습니다.
        // 인벤토리에 아이템이 들어오지 않았으니, 커스터마이징 UI의 자물쇠가 계속 잠겨있는 상태로 유지된 것
        // TODO : 서버 번들 빌드후 이쪽으로 버튼 이벤트 연결을 수정.
        // public void OnGrantArmorClicked() => WithLocalPlayer(c => c.RequestArmorTestKit());

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