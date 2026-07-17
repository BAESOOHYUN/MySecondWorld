using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    // 로컬 플레이어의 Inventory 참조를 들고 있는 루트 스코프 싱글톤.
    // CharacterCustomizeUI(ProjectLifetimeScope)는 PlayerScope의 Inventory를 직접 주입받을 수 없어서,
    // NetworkCharacterAppearance가 로컬 플레이어로 스폰될 때 여기에 등록해두고 UI가 조회한다.
    public class LocalPlayerInventoryProvider
    {
        private Cleverous.VaultInventory.Inventory _inventory;
        public void SetLocal(Cleverous.VaultInventory.Inventory inventory) => _inventory = inventory;
        public bool IsOwned(EquipmentItem requiredItem)
        {
            if (requiredItem == null) return true; // 요구 아이템 없음 = 무료 착용
            return _inventory != null && _inventory.Contains(requiredItem, 1);
        }
    }
}