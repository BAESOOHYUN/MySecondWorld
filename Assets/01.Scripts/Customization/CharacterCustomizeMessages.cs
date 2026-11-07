namespace MySecondWorld.Customization
{
    // 외형 시스템의 MessagePipe 이벤트 4개. 각 이벤트가 "누가 → 누구에게" 가는지 적어 두었다.

    // 보내는 쪽: ObservableCharacterCustomizer (파츠 적용이 끝나 외형이 바뀌었을 때, 바뀐 외형을 알린다)
    // 받는 쪽: 2026-11-08 기준 코드에 구독자가 없다 (PlayerScope에 브로커 등록만 되어 있다)
    public readonly struct CharacterAppearanceChangedEvent
    {
        public readonly CharacterAppearanceData Data;
        public CharacterAppearanceChangedEvent(CharacterAppearanceData data)
        {
            Data = data;
        }
    }

    // 용도: 외형 저장이 끝났다는 신호 (저장 완료 토스트용)
    // 보내는 쪽 · 받는 쪽: 2026-11-08 기준 둘 다 코드에 없다 (ProjectLifetimeScope에 브로커 등록만 되어 있다)
    public readonly struct CharacterAppearanceSavedEvent { }

    // 방향: 서비스·UI → Network (옷을 바꿀 때 서버로 가는 통로)
    // 보내는 쪽: CharacterAppearanceService (파츠 장착·해제·전체 저장), CharacterCustomizeUI (저장 버튼)
    // 받는 쪽: NetworkCharacterAppearance (내 캐릭터일 때만 CmdSetAppearance로 서버에 전달)
    public readonly struct CharacterAppearanceSyncRequestedEvent
    {
        public readonly CharacterAppearanceData Data;
        // 파츠별로 입은 인벤토리 칸 번호 (PartType 순서, 미장착·모름은 -1). null이면 칸 번호는 보내지 않는다
        public readonly int[] EquippedSlots;

        public CharacterAppearanceSyncRequestedEvent(CharacterAppearanceData data, int[] equippedSlots = null)
        {
            Data = data;
            EquippedSlots = equippedSlots;
        }
    }

    // 방향: Network → UI (위와 반대 방향)
    // 보내는 쪽: NetworkCharacterAppearance (서버에 외형이 없는 새 계정이 접속했을 때)
    // 받는 쪽: CharacterCustomizeUI (첫 설정 창을 연다)
    public readonly struct RequestInitialCustomizeEvent { }
}
