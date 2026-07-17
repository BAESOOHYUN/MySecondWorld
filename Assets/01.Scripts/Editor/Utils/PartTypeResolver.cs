using System.Collections.Generic;
using System.Text.RegularExpressions;
using MySecondWorld.Customization;


namespace MySecondWorld.Editor
{
    // "Female_00_Head" / "Male_04_Arm_Upper_Right" / "00_Ear" 처럼
    // Synty 원본 폴더/파츠 이름에서 (성별_)번호_ 접두사를 뗀 뒤 PartType enum과 대소문자 무시 매칭.
    // AvatarPartKeyTablePopulator(폴더 스캔)와 CharacterPresetImporter(프리셋 임포트)가
    // 반드시 같은 규칙으로 PartType을 매겨야 KeyTable 인덱스와 프리셋 인덱스가 어긋나지 않는다.
    public static class PartTypeResolver
    {
        private static readonly Regex CategoryPattern = new(@"^(?:Female_|Male_|All_)?\d+_(.+)$");

        private static readonly Dictionary<string, PartType> CategoryAliases = new()
        {
            { "Head_Attachment", PartType.HelmetAttachment },
            { "Shoulder_Attachment_Right", PartType.ShoulderAttachRight },
            { "Shoulder_Attachment_Left", PartType.ShoulderAttachLeft },
            { "Elbow_Attachment_Right", PartType.ElbowAttachRight },
            { "Elbow_Attachment_Left", PartType.ElbowAttachLeft },
            { "Knee_Attachement_Right", PartType.KneeAttachRight }, // 폴더명 오타(Attachement) 그대로 키로 사용
            { "Knee_Attachement_Left", PartType.KneeAttachLeft },
            { "Extra", PartType.Ear }, // All_12_Extra 안엔 Elf_Ear만 존재
        };

        public static bool TryResolvePartType(string folderName, out PartType partType)
        {
            var match = CategoryPattern.Match(folderName);
            var category = match.Success ? match.Groups[1].Value : folderName;
            var normalized = category.Replace("_", "");

            if (System.Enum.TryParse(normalized, true, out partType))
                return true;
            return CategoryAliases.TryGetValue(category, out partType);
        }
    }
}
