namespace MySecondWorld.Customization
{
    // NetworkInventoryActions(MySecondWorld.Network 어셈블리)가 구현한다.
    // Customization → Network 역방향 참조를 피하려고 인터페이스로 분리했다
    // (Network가 이미 Customization을 참조하므로 반대 방향을 추가하면 순환 참조가 생김).
    public interface IInventoryActionsProvider
    {
        void DiscardItem(int slotIndex);
    }
}