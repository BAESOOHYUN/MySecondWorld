using R3;

namespace MySecondWorld.AI
{
    // NPCMenuUI(MySecondWorld.UI 어셈블리)가 구현한다. INPCDialogueUI/IShopUI와 동일한 목적(순환 참조 회피).
    // 모든 NPC가 공유하는 전역 패널 하나 — NPC별 인스턴스가 아니다.
    public interface INPCMenuUI
    {
        Observable<Unit> OnSelectTalk { get; }
        Observable<Unit> OnSelectShop { get; }
        Observable<Unit> OnClose { get; }
        void Open();
        void Close();
    }
}
