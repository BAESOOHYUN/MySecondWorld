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
        private bool _isClaimed;
        private void Awake()
        {
            _inventory = GetComponent<Cleverous.VaultInventory.Inventory>();
        }

        public void ClaimReward() => CmdClaimQuestReward();

        [Command]
        private void CmdClaimQuestReward()
        {
            if (_isClaimed) return;
            if (_rewardItem == null) return;

            int leftover = _inventory.DoAdd(new RootItemStack(_rewardItem, _rewardStackSize));
            if (leftover > 0) return;

            _isClaimed = true;
        }
    }
}