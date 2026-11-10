namespace MySecondWorld.Customization
{
    // AvatarPartKeyTableSO의 서브타입 문자열 약속을 한곳에 모은 상수.
    // 문자열을 글자 그대로 쓰면 오타가 나도 컴파일러가 못 잡고, 조용히 판정이 빗나간다.
    public static class PartSubTypes
    {
        // Head 파츠: 얼굴 요소(눈·눈썹·귀·머리카락 등)를 다 보여 주는 맨얼굴 / 투구 때문에 가려 주는 형태
        public const string HeadAllElements = "All_Elements";
        public const string HeadNoElements = "No_Elements";

        // HeadCoverings 파츠: 머리카락을 가리는 후드·헬멧류 / 수염을 가리는 복면류
        public const string CoveringNoHair = "No_Hair";
        public const string CoveringNoFacialHair = "No_FacialHair";
    }
}