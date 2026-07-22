using UnityEngine;
using Mirror;
using VContainer;
using MySecondWorld.Customization;
using Cleverous.VaultSystem;

namespace MySecondWorld.Network
{
    // 인벤토리 UI/상점/퀘스트 보상이 사용하는 서버 Command 모음.
    // NetworkWeaponEquip/NetworkCharacterAppearance와 마찬가지로
    // Inventory와 같은 GameObject(플레이어 프리팹)에 부착한다.
    // Phase가 늘어날 때마다 이 파일에 Command를 추가한다.
    [RequireComponent(typeof(Cleverous.VaultInventory.Inventory))]
    public class NetworkInventoryActions : NetworkBehaviour, IInventoryActionsProvider
    {
        private LocalPlayerInventoryProvider _localInventoryProvider;
        private Cleverous.VaultInventory.Inventory _inventory;

        [Inject]
        public void Construct(
            LocalPlayerInventoryProvider localInventoryProvider)
        {
            // ProjectLifetimeScope가 초기화될 때, 딱 한 번 → LocalPlayerInventoryProvider가 DI 컨테이너에 "빈 상자"로 등록됨
            // 로그인 후 Mirror가 캐릭터 프리팹을 Instantiate할 때마다 → PlayerScope(자식)가 초기화되면서 그 안의 NetworkInventoryActions.Construct()가 호출됨
            // (ProjectLifetimeScope 초기화시점에서 만들어둔 "빈 상자"(LocalPlayerInventoryProvider)가 주입되어 들어옴
            _localInventoryProvider = localInventoryProvider;
        }

        private void Awake()
        {
            _inventory = GetComponent<Cleverous.VaultInventory.Inventory>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            _localInventoryProvider.SetActions(this);
        }

        // IInventoryActionsProvider 구현 — InventoryUI는 이 메서드만 알면 되고, Command는 사실은 몰라도 된다
        public void DiscardItem(int slotIndex) => CmdDiscardItem(slotIndex);
        public void TradeItem(int payItemDbKey, int receiveItemDbKey) => CmdTradeItem(payItemDbKey, receiveItemDbKey);

        [Command] // 아이템 삭제
        private void CmdDiscardItem(int slotIndex)
        {
            if (_inventory == null) return;
            var stack = _inventory.Get(slotIndex);
            if (stack?.Source == null) return; // 빈 슬롯 방어

            // 전량 버리기(간단 구현 — 부분 버리기는 범위 밖)
            _inventory.DoTake(slotIndex, stack.StackSize);
        }

        [Command] // 물물교환
        private void CmdTradeItem(int payItemDbKey, int receiveItemDbKey)
        {
            if (_inventory == null) return;
            if (Vault.Get(payItemDbKey) is not Cleverous.VaultInventory.RootItem payItem) return;
            if (Vault.Get(receiveItemDbKey) is not Cleverous.VaultInventory.RootItem receiveItem) return;
            if (!_inventory.Contains(payItem, 1)) return; // 대가 아이템 미보유 → 거부(물물교환, 재화 시스템 없음)

            _inventory.DoTake(payItem, 1);
            _inventory.DoAdd(new Cleverous.VaultInventory.RootItemStack(receiveItem, 1));
        }
    }
}