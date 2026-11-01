namespace MySecondWorld.Customization
{
    // NetworkInventoryActions(MySecondWorld.Network 어셈블리)가 구현한다.
    // Customization → Network 역방향 참조를 피하려고 인터페이스로 분리했다
    // (Network가 이미 Customization을 참조하므로 반대 방향을 추가하면 순환 참조가 생김).
    public interface IInventoryActionsProvider
    {
        // slotIndex: 버릴 인벤토리 칸 번호
        void DiscardItem(int slotIndex);

        // 물물교환. payInventorySlotIndex는 대가로 내는 아이템이 든 인벤토리 칸 번호.
        //  - 판매(장비를 내고 코인을 받음): 어느 칸이 팔리는지가 중요하므로 실제 칸 번호를 보낸다.
        //  - 구매(코인을 내고 장비를 받음): 코인은 어느 칸에서 빼도 같으므로 -1("칸을 지정하지 않음")을 보낸다.
        void TradeItem(int payItemDbKey, int receiveItemDbKey, int payInventorySlotIndex);

        // slotIndex: 사용할 인벤토리 칸 번호
        void UseItem(int slotIndex);
    }
}