using System.Collections.Generic;
using R3;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.AI
{
    // ShopUI(MySecondWorld.UI 어셈블리)가 구현한다.
    // AI → UI 역방향 참조를 피하려고 인터페이스로 분리했다
    // (UI가 이미 AI를 참조하므로 반대 방향을 추가하면 순환 참조가 생김).
    // INPCDialogueUI와 동일한 목적.
    public interface IShopUI
    {
        Observable<Unit> OnClose { get; }
        // shopNetId: 이 상점 NPC의 netId. 거래 요청에 실려 서버가 "어느 상점인가"를 검증하는 데 쓴다
        void Open(IReadOnlyList<EquipmentItem> catalog, uint shopNetId);
        void Close();
        void RequestPurchase(EquipmentItem item, uint shopNetId); // 구매요청 (NPC Dialogue UI에서 LLM이 요구)
    }
}