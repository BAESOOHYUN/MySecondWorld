using System.Collections.Generic;

namespace MySecondWorld.Customization
{
    public enum EquipmentSlotType
    {
        // 1. 방어구 / 의상 파츠 (7종)
        HeadCoverings,    // 투구 / 모자
        Torso,            // 상체 / 상의
        ArmUpper,         // 상완 보호구
        ArmLower,         // 하완 보호구
        Hands,            // 손 / 장갑
        Hips,             // 힙 / 하의
        Legs,             // 다리 / 신발 (LegLeft/Right)

        // 2. 악세사리 (5종 + 위의 Knees = 6종)
        HelmetAttachment, // 투구 장식
        ShoulderAttach,   // 어깨 장식
        ElbowAttach,      // 팔꿈치 보호구
        Knees,            // 무릎 각반 (KneeAttach)
        BackAttachment,   // 등 장식 (날개/망토)
        HipsAttachment,   // 허리 장식

        // 3. 하단 상시 고정 무기 (2종)
        Weapon,           // 주무기
        Shield            // 보조무기 / 방패 / 화살통
    }
    public static class EquipmentSlotMap
    {
        private static readonly Dictionary<EquipmentSlotType, PartType[]> SlotToParts = new()
        {
            // HeadCoverings 슬롯은 [Head, HeadCoverings] 두 파츠를 묶는다. 슬롯에 "무엇을 입었나"는 파츠들의 요구 아이템으로 판단하는데,
            // Head는 맨얼굴(All_Elements)에 요구 아이템이 없다(null)는 데이터 약속에 기댄다. (No_Elements가 헬멧)
            // 맨얼굴에 요구 아이템이 달리면 항상 입은 것으로 잡혀 슬롯이 안 비워진다.
            // 그 약속은 AvatarPartKeyTableSO의 "Validate Bare Head Has No Required Item"으로 점검한다.
            { EquipmentSlotType.HeadCoverings,    new[] {  PartType.Head, PartType.HeadCoverings } },
            { EquipmentSlotType.Torso,            new[] { PartType.Torso } },
            { EquipmentSlotType.ArmUpper,         new[] { PartType.ArmUpperLeft,   PartType.ArmUpperRight } },
            { EquipmentSlotType.ArmLower,         new[] { PartType.ArmLowerLeft,   PartType.ArmLowerRight } },
            { EquipmentSlotType.Hands,            new[] { PartType.HandLeft,      PartType.HandRight } },
            { EquipmentSlotType.Hips,             new[] { PartType.Hips } },
            { EquipmentSlotType.Legs,             new[] { PartType.LegLeft,        PartType.LegRight } },
            { EquipmentSlotType.HelmetAttachment, new[] { PartType.HelmetAttachment } },
            { EquipmentSlotType.ShoulderAttach,   new[] { PartType.ShoulderAttachLeft, PartType.ShoulderAttachRight } },
            { EquipmentSlotType.ElbowAttach,      new[] { PartType.ElbowAttachLeft, PartType.ElbowAttachRight } },
            { EquipmentSlotType.Knees,            new[] { PartType.KneeAttachLeft, PartType.KneeAttachRight } },
            { EquipmentSlotType.BackAttachment,   new[] { PartType.BackAttachment } },
            { EquipmentSlotType.HipsAttachment,   new[] { PartType.HipsAttachment } },
        };
        private static readonly Dictionary<PartType, EquipmentSlotType> PartToSlot = BuildPartToSlot();
        public static IReadOnlyList<PartType> GetParts(EquipmentSlotType slot) // 표에 없으면 빈 배열
            => SlotToParts.TryGetValue(slot, out var parts) ? parts : System.Array.Empty<PartType>();

        public static bool TryGetSlot(PartType part, out EquipmentSlotType slot) // PartToSlot 조회
            => PartToSlot.TryGetValue(part, out slot);

        private static Dictionary<PartType, EquipmentSlotType> BuildPartToSlot() // SlotToParts를 뒤집어 채움
        {
            var map = new Dictionary<PartType, EquipmentSlotType>();
            foreach (var pair in SlotToParts)    // 슬롯 하나씩
            {
                foreach (var part in pair.Value) // 그 슬롯의 파츠를 하나씩
                {
                    map[part] = pair.Key;        // 파츠 → 슬롯으로 뒤집어 기록
                }
            }
            return map;
        }
    }   
}