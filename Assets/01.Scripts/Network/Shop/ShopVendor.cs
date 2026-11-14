using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Network
{
    // 상점 NPC가 구현한다 (NPCInteractable). Network는 AI를 참조할 수 없어서
    // 서버가 "이 NPC가 뭘 파는가"를 읽을 통로를 인터페이스로 만든다
    public interface IShopVendor
    {
        IReadOnlyList<EquipmentItem> ShopCatalog { get; }
    }

    public static class ShopVendor
    {
        // [클라] 상점 NPC 오브젝트의 netId. NetworkIdentity가 없으면 0
        // AI 어셈블리가 Mirror를 직접 참조하지 않게 이 헬퍼로 감싼다
        public static uint GetNetId(GameObject vendor)
            =>vendor.TryGetComponent(out NetworkIdentity id) ? id.netId : 0;
        
        // [서버] netId로 상점 NPC를 찾는다. 없거나 상점이 아니면 false (조작된 값 방어)
        public static bool TryFind(uint netId, out IShopVendor vendor, out Transform transform)
        {
            vendor = null;
            transform = null;
            // netId == 0은 "스폰 안 된 값"이라 먼저 거름.
            if (netId == 0 || !NetworkServer.spawned.TryGetValue(netId, out var identity))
                return false;

            vendor = identity.GetComponent<IShopVendor>();
            transform = identity.transform;
            return vendor != null;
        }
    }
}

// GetComponentInParent와 GetComponentInChildren을 쓴 건
// NetworkIdentity가 NPC 루트에 있고 NPCInteractable이 같은 오브젝트거나 자식인 경우를 모두 받기 위해서다.