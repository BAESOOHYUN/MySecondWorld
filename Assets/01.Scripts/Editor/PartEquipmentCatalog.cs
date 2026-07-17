using System;
using System.Collections.Generic;
using UnityEditor;
using MySecondWorld.Customization;

namespace MySecondWorld.Editor
{
    // 그룹 B/C 파츠 순회 대상 + GeneratedTitle 조합 로직.
    // ItemAssetBakeHelper(SO 직접 베이킹)와 PartEquipmentCsvGenerator(감사용 CSV 내보내기)가 공유.

    public class PartEquipmentCatalog : EditorWindow
    {

        #region 상수 · 설정 테이블
        public static readonly PartType[] GroupBPartTypes =
        {
            PartType.Torso, PartType.ArmUpperLeft, PartType.ArmUpperRight,
            PartType.ArmLowerLeft, PartType.ArmLowerRight,
            PartType.HandLeft, PartType.HandRight,
            PartType.Hips, PartType.LegLeft, PartType.LegRight,
        };

        public static readonly PartType[] GroupCPartTypes =
        {
            PartType.HeadCoverings, PartType.BackAttachment,
            PartType.ShoulderAttachLeft, PartType.ShoulderAttachRight,
            PartType.ElbowAttachLeft, PartType.ElbowAttachRight,
            PartType.KneeAttachLeft, PartType.KneeAttachRight,
            PartType.HelmetAttachment, PartType.HipsAttachment,
        };

        private static readonly Dictionary<PartType, string> PartTypeKoreanNames = new()
        {
            { PartType.Torso, "상의" },
            { PartType.ArmUpperLeft, "왼쪽 상완 보호구" },
            { PartType.ArmUpperRight, "오른쪽 상완 보호구" },
            { PartType.ArmLowerLeft, "왼쪽 하완 보호구" },
            { PartType.ArmLowerRight, "오른쪽 하완 보호구" },
            { PartType.HandLeft, "왼손 장갑" },
            { PartType.HandRight, "오른손 장갑" },
            { PartType.Hips, "하의" },
            { PartType.LegLeft, "왼쪽 다리 보호구" },
            { PartType.LegRight, "오른쪽 다리 보호구" },
            { PartType.HeadCoverings, "머리 장식" },
            { PartType.BackAttachment, "등 장식" },
            { PartType.ShoulderAttachLeft, "왼쪽 어깨 장식" },
            { PartType.ShoulderAttachRight, "오른쪽 어깨 장식" },
            { PartType.ElbowAttachLeft, "왼쪽 팔꿈치 보호구" },
            { PartType.ElbowAttachRight, "오른쪽 팔꿈치 보호구" },
            { PartType.KneeAttachLeft, "왼쪽 무릎 보호구" },
            { PartType.KneeAttachRight, "오른쪽 무릎 보호구" },
            { PartType.HelmetAttachment, "투구 장식" },
            { PartType.HipsAttachment, "허리 장식" },
        };

        private static readonly Dictionary<string, string> SubTypeKoreanNames =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "Leather", "가죽" }, { "Plate", "판금" }, { "Chainmail", "사슬" },
            { "Cloth", "천" }, { "Fur", "모피" }, { "Metal", "금속" },
            { "Wood", "나무" }, { "Bone", "뼈" }, { "Iron", "철" },
            { "Gold", "금" }, { "Silver", "은" }, { "Bronze", "청동" },
            { "Steel", "강철" },
        };

        // 서브타입이 실제 재질이 아니라 '없음'을 뜻하는 더미값일 때 감지용
        // "이건 애초에 의미 있는 서브타입이 아니다"를 걸러내고, 걸리면 서브타입 세그먼트 자체를 통째로 생략해서 "남성 상의 15호"처럼 깔끔하게 만든다
        private static readonly HashSet<string> PlaceHolderSubTypeTokens =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "No_Elements", "None", "Default", "NA", ""
        };
        #endregion


        public static string BuildTitle(PartType partType, string genderTag, string subType, int index, bool isGourpC)
        {
            string koreanName = PartTypeKoreanNames.TryGetValue(partType, out var name) ? name : partType.ToString();
            string subTypeSegment = ResolveSubtypeSegment(subType);
            string indexSegment = $"{index:00}호";
            var segments = new List<string>();
            if (!isGourpC)
                segments.Add(genderTag == "Male" ? "남성" : "여성");
            segments.Add(koreanName);
            if (!string.IsNullOrEmpty(subTypeSegment))
                segments.Add(subTypeSegment);
            segments.Add(indexSegment);

            return string.Join(" ", segments);
        }

        //  더미값 판정/번역/폴백 로직. BuildTitle 쪽 코드를 깔끔하게 유지해주는 역할
        private static string ResolveSubtypeSegment(string subType)
        {
            // ubType이 "No_Elements"/"None"/"" 같은 "서브타입 없음"을 뜻하는 값이면
            // 세그먼트를 아예 비워서 BuildTitle이 그 세그먼트를 통째로 생략
            if (PlaceHolderSubTypeTokens.Contains(subType ?? string.Empty))
                return string.Empty;
            // Leather" → "가죽"처럼 SubTypeKoreanNames에 등록된 값이면 번역된 한국어를 돌려줌
            // 번역 사전에 없는 낯선 영문 서브타입이면(누락됐거나 새로 추가된 재질), 번역 못 했다고 세그먼트를 비우거나 에러 내지 않고 원본 영문을 그대로 씀
            return SubTypeKoreanNames.TryGetValue(subType, out var translated) ? translated : subType;
        }


    }
}