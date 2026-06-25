namespace MySecondWorld.Customization
{
    public readonly struct CharacterAppearanceChangedEvent // UI → Customizer 방향
    {
        public readonly CharacterAppearanceData Data;
        public CharacterAppearanceChangedEvent(CharacterAppearanceData data)
        {
            Data = data;
        }
    }
    public readonly struct CharacterAppearanceSavedEvent { } // 저장 완료 토스트 신호용
}
