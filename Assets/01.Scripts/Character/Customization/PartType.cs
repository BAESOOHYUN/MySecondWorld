
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
        Primary,
        Secondary,
        Skin
    }

    public enum Gender
    {
        Male,
        Female
    }
}