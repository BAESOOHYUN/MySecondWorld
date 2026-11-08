using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    using Inventory = Cleverous.VaultInventory.Inventory;
    // 로컬 플레이어의 Inventory 참조를 들고 있는 루트 스코프 싱글톤.
    // CharacterCustomizeUI(ProjectLifetimeScope)는 PlayerScope의 Inventory를 직접 주입받을 수 없어서(부모→자식 참조 불가),
    // 이 클래스를 부모 쪽에 "빈 상자"로 미리 등록해두고, 캐릭터가 스폰되면
    // 자식 쪽 컴포넌트들이 각자 자기 발로 이 상자를 찾아와 내용물을 채운다. 채워지는 건 두 가지:
    //   1) NetworkCharacterAppearance.OnStartLocalPlayer() → SetLocal(_inventory)
    //      : Inventory 원본 참조 (IsOwned 체크, InventoryUI 슬롯 조회용)
    //   2) NetworkInventoryActions.OnStartLocalPlayer() → SetActions(this)
    //      : 버리기/거래/퀘스트보상 Command 진입점(IInventoryActionsProvider)
    // CharacterCustomizeUI는 그 상자(LocalPlayerInventoryProvider)를 들고 있다가, 상자를 열어볼 때(IsOwned 호출 시점) 안에 든 게 있으면 쓴다
    public class LocalPlayerInventoryProvider
    {
        private Inventory _inventory;

        // InventoryUI/ShopUI가 슬롯 목록을 직접 순회하기 위해 필요
        public Inventory Inventory => _inventory;
        public event System.Action OnProvided; // 로컬 플레이어의 인벤토리/액션이 채워졌을 때

        public void SetLocal(Inventory inventory)
        {
            _inventory = inventory;
            OnProvided?.Invoke();
        }

        // InventoryUI/ShopUI가 Command(버리기/거래/퀘스트보상)를 호출하기 위해 필요
        // NetworkInventoryActions가 OnStartLocalPlayer에서 채워준다.(SetLocal과 동일한 글루 패턴)
        public IInventoryActionsProvider Actions { get; private set; }
        public void SetActions(IInventoryActionsProvider actions) => Actions = actions;


        // InventoryUI가 무기/액세서리 즉시 장착을 호출하기 위해 필요
        // NetworkWeaponEquip이 OnStartLocalPlayer에서 채워준다.(SetActions와 동일한 글루 패턴)
        public IWeaponEquipActionsProvider WeaponActions { get; private set; }
        public void SetWeaponActions(IWeaponEquipActionsProvider actions)
        {
            WeaponActions = actions;
            OnProvided?.Invoke();
        }

        public bool IsOwned(EquipmentItem requiredItem)
        {
            if (requiredItem == null) return true; // 요구 아이템 없음 = 무료 착용
            return _inventory != null && _inventory.Contains(requiredItem, 1);
        }        
    }
}