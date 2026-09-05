namespace MySecondWorld.Customization
{
    // NetworkWeaponEquip(MySecondWorld.Network 어셈블리)이 구현한다.
    // IInventoryActionsProvider와 동일한 이유(Network→Customization 역참조 방지)로 인터페이스를 분리
    // (Network가 이미 Customization을 참조하므로 반대 방향을 추가하면 순환 참조가 생김).
    public interface IWeaponEquipActionsProvider
    {
        void EquipMainHand(int weaponDbKey);
        void UnequipMainHand();
        int CurrentMainHandDbKey { get; }
        event System.Action OnMainHandStateChanged; // 장착/해제 시 발행 — InventoryUI가 버튼 라벨 갱신용으로 구독
    }
}