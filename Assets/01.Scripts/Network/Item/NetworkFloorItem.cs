using UnityEngine;
using Mirror;
using Cleverous.VaultInventory;

namespace MySecondWorld.Network
{
    [RequireComponent(typeof(Collider))]
    public class NetworkFloorItem : NetworkBehaviour
    {
        [SerializeField] private RootItem _item;
        [SerializeField] private int _stackSize = 1;
        // "이 바닥 아이템이 이미 누군가에게 지급됐는가"를 기억하는 스위치
        // _isPickedUp 플래그는 서버가 NetworkServer.Destroy 처리 전 네트워크 왕복 시간 동안
        // 중복 CmdPickup 호출이 들어와도 인벤토리에 '중복 지급되지 않도록' 막는 서버측 가드
        private bool _isPickedUp;

        private void OnTriggerEnter(Collider other)
        {
            // identity.isLocalPlayer 체크로 '자기 자신의 플레이어가 닿았을 때'만 Command 호출
            if (!other.TryGetComponent(out NetworkIdentity identity) || !identity.isLocalPlayer) return;
            CmdPickup();
        }

        [Command(requiresAuthority = false)]
        private void CmdPickup(NetworkConnectionToClient sender = null)
        {
            // 아직 아무도 주워가지 않았다면 처리 시작
            if (_isPickedUp) return;
            if (_item == null) return;
            if (sender?.identity == null) return;
            
            // 호출자의 NetworkIdentity에서 Inventory 컴포넌트 찾기
            if (!sender.identity.TryGetComponent(out Cleverous.VaultInventory.Inventory inventory)) return;
            
            // leftover = 요청한 개수 중 인벤토리에 못 들어간 수량
            int leftover = inventory.DoAdd(new RootItemStack(_item, _stackSize));
            
            // 못 들어간 게 있으면 아이템을 그대로 남겨둔다 (재시도 가능)
            if (leftover > 0) return;

            // 모두 들어갔으면 지급 완료 → 필드 아이템 제거
            _isPickedUp = true;
            NetworkServer.Destroy(gameObject);
        }
    }
}