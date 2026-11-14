using MySecondWorld.Inventory.Items;

namespace MySecondWorld.AI
{
    // 추천 카드(MySecondWorld.UI 어셈블리의 ItemRecommendCardUI)가 구현한다. IShopUI와 같은 목적(AI → UI 순환 참조 회피)
    public interface IItemRecommendUI
    {
        // shopNetId: 이 상품을 파는 NPC의 netId (구매 요청에 실려 서버가 거래를 검증한다)
        void ShowRecommend(EquipmentItem item, uint shopNetId);
        void HideRecommend();
    }
}