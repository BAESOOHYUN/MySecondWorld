using UnityEngine;
using Mirror;
using Cleverous.VaultInventory;

namespace MySecondWorld.Network
{
    [RequireComponent(typeof(Cleverous.VaultInventory.Inventory))]
    public class NetworkQuestRewardStub : NetworkBehaviour
    {
        [SerializeField] private RootItem _item;
        [SerializeField] private int _stackSize = 1;
        private Cleverous.VaultInventory.Inventory _inventory;
        private bool _isClaimed; // 이미 보상을 수령했는가? '중복 지급되지 않도록' 막는 서버측 가드
        private void Awake()
        {
            _inventory = GetComponent<Cleverous.VaultInventory.Inventory>();
        }

        public void ClaimReward() => CmdClaimQuestReward();

        [Command]
        private void CmdClaimQuestReward()
        {
            if (_isClaimed) return;
            if (_item == null) return;

            int leftover = _inventory.DoAdd(new RootItemStack(_item, _stackSize));
            if (leftover > 0) return;

            _isClaimed = true;
        }
    }
}