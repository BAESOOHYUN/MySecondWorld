// 어셈블리는 Foundation(Save가 아래 칸에서 참조하도록 내림), 네임스페이스는 외형 도메인이라 Customization 유지
namespace MySecondWorld.Customization
{
    public enum PartType // 파츠 배열 인덱싱 키
    {
        // Male & Female
        Hair,
        Head,
        Eyebrows,
        FacialHair, //Famale에게는 없음
        Torso,
        ArmUpperRight,
        ArmUpperLeft,
        ArmLowerRight,
        ArmLowerLeft,
        HandLeft,
        HandRight,
        Hips,
        LegLeft,
        LegRight,

        // All_Gender_Parts 액세서리
        Ear,
        HeadCoverings,
        BackAttachment,
        ShoulderAttachLeft,
        ShoulderAttachRight,
        ElbowAttachLeft,
        ElbowAttachRight,
        KneeAttachLeft,
        KneeAttachRight,
        HelmetAttachment,
        HipsAttachment
    }

    public enum ColorTarget // 셰이더 프로퍼티명과 연결되는 색상 채널
    {
        Primary,           // _Color_Primary
        Secondary,         // _Color_Secondary
        LeatherPrimary,    // _Color_Leather_Primary
        LeatherSecondary,  // _Color_Leather_Secondary
        MetalPrimary,      // _Color_Metal_Primary
        MetalSecondary,    // _Color_Metal_Secondary
        MetalDark,         // _Color_Metal_Dark
        Hair,              // _Color_Hair
        Skin,              // _Color_Skin
        Stubble,           // _Color_Stubble
        Scar,              // _Color_Scar
        Eyes,              // _Color_Eyes
        BodyArt            // _Color_BodyArt
    }

    public enum Gender
    {
        Male,
        Female
    }
}