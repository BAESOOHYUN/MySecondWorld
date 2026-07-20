using System;
using System.Collections.Generic;
using MySecondWorld.Customization;
using MySecondWorld.Inventory;

namespace MySecondWorld.Editor
{
    // 그룹 B/C 파츠 순회 대상 + GeneratedTitle 조합 로직.
    // ItemAssetBakeHelper(SO 직접 베이킹)와 PartEquipmentCsvGenerator(감사용 CSV 내보내기)가 공유.

    public class PartEquipmentCatalog
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

        // 그룹 A(Head, Hair, Eyebrow, FacialHair, Ear)는 원칙적으로 이 카탈로그의 순회 대상이 아님
        // (인벤토리 대상 아님, 타고난 생김새). 단 Head의 No_Elements 서브타입만 헬멧 장착 조건으로
        // 예외적으로 잠금 대상 — 그룹 B/C처럼 "PartType 전체가 동일 규칙"이 아니라
        // "PartType 안의 특정 서브타입만"이라 배열로 묶지 않고 단일 상수로 명시.
        public const PartType HeadLockPartType = PartType.Head;
        public const string HeadLockSubType = "No_Elements";

        private static readonly Dictionary<PartType, string> PartTypeKoreanNames = new()
        {
            { PartType.Torso, "상의" },
            { PartType.ArmUpperLeft, "상완 보호구" },
            { PartType.ArmUpperRight, "상완 보호구" },
            { PartType.ArmLowerLeft, "하완 보호구" },
            { PartType.ArmLowerRight, "하완 보호구" },
            { PartType.HandLeft, "장갑" },
            { PartType.HandRight, "장갑" },
            { PartType.Hips, "하의" },
            { PartType.LegLeft, "다리 보호구" },
            { PartType.LegRight, "다리 보호구" },
            { PartType.HeadCoverings, "머리 장식" },
            { PartType.BackAttachment, "등 장식" },
            { PartType.ShoulderAttachLeft, "어깨 장식" },
            { PartType.ShoulderAttachRight, "어깨 장식" },
            { PartType.ElbowAttachLeft, "팔꿈치 보호구" },
            { PartType.ElbowAttachRight, "팔꿈치 보호구" },
            { PartType.KneeAttachLeft, "무릎 보호구" },
            { PartType.KneeAttachRight, "무릎 보호구" },
            { PartType.HelmetAttachment, "투구 장식" },
            { PartType.HipsAttachment, "허리 장식" },
            // Head는 그룹 A지만 No_Elements 서브타입만 헬멧 장착용으로 잠금 대상 — 그룹 B/C와 동일 패턴 재사용
            { PartType.Head, "헬멧" },
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
            "No_Elements", "None", "Default", "NA", "",
            "Base_Hair", "No_FacialHair", "No_Hair", "Helmet",
        };

        // 재질(한글) → 등급 배율. CSV Material 컬럼에 이 13종 중 하나를 그대로 씀
        private static readonly Dictionary<string, float> MaterialMultipliers =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "나무", 0.8f }, { "뼈", 0.8f }, { "천", 1.0f }, { "가죽", 1.2f },
            { "청동", 1.3f }, { "철", 1.5f }, { "사슬", 1.7f }, { "금속", 1.6f },
            { "강철", 1.9f }, { "은", 2.1f }, { "판금", 2.3f }, { "금", 2.5f },
        };

        // 스탯 종류. 매핑에 없으면(Head 락 등) 스탯 없음
        private static readonly Dictionary<PartType, StatType> PartTypeToStatType = new()
        {
            { PartType.Torso, StatType.Defense },
            { PartType.ArmUpperLeft, StatType.Attack }, { PartType.ArmUpperRight, StatType.Attack },
            { PartType.ArmLowerLeft, StatType.Attack }, { PartType.ArmLowerRight, StatType.Attack },
            { PartType.HandLeft, StatType.AttackSpeed }, { PartType.HandRight, StatType.AttackSpeed },
            { PartType.Hips, StatType.Defense },
            { PartType.LegLeft, StatType.MoveSpeed }, { PartType.LegRight, StatType.MoveSpeed },
            { PartType.ElbowAttachLeft, StatType.Defense }, { PartType.ElbowAttachRight, StatType.Defense },
            { PartType.KneeAttachLeft, StatType.Defense }, { PartType.KneeAttachRight, StatType.Defense },
            { PartType.ShoulderAttachLeft, StatType.Defense }, { PartType.ShoulderAttachRight, StatType.Defense },
            { PartType.BackAttachment, StatType.MagicDefense },
            { PartType.HeadCoverings, StatType.MagicAttack },
            { PartType.HelmetAttachment, StatType.Luck },
            { PartType.HipsAttachment, StatType.Luck },
        };

        // 스탯 종류별 기준값(전투 시스템 확정 전 초안)
        private static readonly Dictionary<StatType, float> BasePerStatType = new()
        {
            { StatType.Defense, 5f }, { StatType.Attack, 4f }, { StatType.MagicAttack, 4f },
            { StatType.MagicDefense, 5f }, { StatType.AttackSpeed, 0.1f },
            { StatType.MoveSpeed, 0.3f }, { StatType.Luck, 2f },
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

        // BuildDraftStats: 계산 단계
        // Value = 부위 기준값 × 재질 배율. 부위 매핑이 없으면(Head 락) 빈 리스트,
        // 재질이 13종 표에 없으면 배율 1.0(사람이 CSV에서 수기 보정하는 걸 전제로 한 안전한 기본값)
        public static List<StatModifier> BuildDraftStats(PartType partType, string material)
        {
            if (!PartTypeToStatType.TryGetValue(partType, out var statType))
                return new List<StatModifier>();

            float baseValue = BasePerStatType.TryGetValue(statType, out var b) ? b : 0f;
            float multiplier = MaterialMultipliers.TryGetValue(material ?? string.Empty, out var m) ? m : 1.0f;

            return new List<StatModifier>
            {
                new StatModifier { Type = statType, ModifierType = ModifierType.Flat, Value =baseValue * multiplier}
            };
        }

        //  더미값 판정/번역/폴백 로직. BuildTitle 쪽 코드를 깔끔하게 유지해주는 역할
        private static string ResolveSubtypeSegment(string subType)
        {
            // subType이 "No_Elements"/"None"/"" 같은 "서브타입 없음"을 뜻하는 값이면
            // 세그먼트를 아예 비워서 BuildTitle이 그 세그먼트를 통째로 생략
            if (PlaceHolderSubTypeTokens.Contains(subType ?? string.Empty))
                return string.Empty;
            // Leather" → "가죽"처럼 SubTypeKoreanNames에 등록된 값이면 번역된 한국어를 돌려줌
            // 번역 사전에 없는 낯선 영문 서브타입이면(누락됐거나 새로 추가된 재질), 번역 못 했다고 세그먼트를 비우거나 에러 내지 않고 원본 영문을 그대로 씀
            return SubTypeKoreanNames.TryGetValue(subType, out var translated) ? translated : subType;
        }

        // 더미값(플레이스홀더)이 아닌데 SubTypeKoreanNames에 없는 서브타입인지 판별.
        // ResolveSubtypeSegment와 같은 판정 기준을 재사용해 진단 도구(ItemAssetBakeHelper)가
        // 실제 베이킹 로직과 어긋나지 않게 함
        public static bool IsMissingTranslation(string subType)
        {
            if (PlaceHolderSubTypeTokens.Contains(subType ?? string.Empty))
                return false;
            return !SubTypeKoreanNames.ContainsKey(subType);
        }
    }
}