namespace MySecondWorld.Customization
{
    // 외형 시스템의 MessagePipe 이벤트 2개. 각 이벤트가 "누가 → 누구에게" 가는지 적어 두었다.

    // 방향: 서비스·UI → Network (옷을 바꿀 때 서버로 가는 통로)
    // 보내는 쪽: CharacterAppearanceService (파츠 장착·해제·전체 저장), CharacterCustomizeUI (저장 버튼)
    // 받는 쪽: NetworkCharacterAppearance (내 캐릭터일 때만 CmdSetAppearance로 서버에 전달)
    public readonly struct CharacterAppearanceSyncRequestedEvent
    {
        public readonly CharacterAppearanceData Data;
        // 파츠별로 입은 인벤토리 칸 번호 (PartType 순서, 미장착·모름은 -1). null이면 칸 번호는 보내지 않는다
        public readonly int[] EquippedPartSlots;

        public CharacterAppearanceSyncRequestedEvent(CharacterAppearanceData data, int[] equippedPartSlots = null)
        {
            Data = data;
            EquippedPartSlots = equippedPartSlots;
        }
    }

    // 방향: Network → UI (위와 반대 방향)
    // 보내는 쪽: NetworkCharacterAppearance (서버에 외형이 없는 새 계정이 접속했을 때)
    // 받는 쪽: CharacterCustomizeUI (첫 설정 창을 연다)
    public readonly struct RequestInitialCustomizeEvent { }
}
