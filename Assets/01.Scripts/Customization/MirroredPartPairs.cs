using System.Collections.Generic;

namespace MySecondWorld.Customization
{
    // 좌우가 1:1 미러 쌍인 파츠 7쌍의 정적 테이블.
    // UI(CharacterCustomizeUI) 선택 레벨에서만 사용한다 — CharacterCustomizerBase 렌더러는
    // 이 테이블을 참조하지 않으므로 NPC 비대칭 외형에는 영향이 없다.
    public static class MirroredPartPairs
    {
        // Left가 UI 대표 탭, Right는 탭에서 숨기고 Left 선택에 따라 같은 index로 함께 적용
        public static readonly (PartType Left, PartType Right)[] Pairs =
        {
            (PartType.ArmUpperLeft,       PartType.ArmUpperRight),
            (PartType.ArmLowerLeft,       PartType.ArmLowerRight),
            (PartType.HandLeft,           PartType.HandRight),
            (PartType.LegLeft,            PartType.LegRight),
            (PartType.ShoulderAttachLeft, PartType.ShoulderAttachRight),
            (PartType.ElbowAttachLeft,    PartType.ElbowAttachRight),
            (PartType.KneeAttachLeft,     PartType.KneeAttachRight)
        };

        // 양방향 조회 테이블 (Left→Right, Right→Left 둘 다 등록)
        private static readonly Dictionary<PartType, PartType> MirrorLookup;

        // HashSet 두 개를 따로 둔 이유 :
        // IsLeft/IsRight를 Pairs 배열 선형 탐색으로 하면 552개 베이킹 루프에서 매번 7회 비교가 돈다.
        // 정적 생성자에서 한 번만 만들어 O(1)로 탐색
        private static readonly HashSet<PartType> LeftSet;
        private static readonly HashSet<PartType> RightSet;

        // 정적 생성자(static constructor)
        // MirroredPartPairs.MirroredPartPairs() 같은 호출은 문법적으로 불가능.
        // 부르는 주체가 오직 CLR(런타임) 하나뿐이고, 그마저도 자동으로 부른다.
        // 호출자가 존재하지 않는데 "누구에게 공개할지"를 적는 건 의미가 없다. 그래서 컴파일러가 아예 막는다.
        // 같은 이유로 매개변수도 못 받는다. 인자를 넘겨줄 사람이 없기 때문.
        static MirroredPartPairs()
        {
            MirrorLookup = new Dictionary<PartType, PartType>(Pairs.Length * 2);
            LeftSet = new HashSet<PartType>();
            RightSet = new HashSet<PartType>();
            foreach (var pair in Pairs)
            {
                MirrorLookup[pair.Left] = pair.Right;
                MirrorLookup[pair.Right] = pair.Left;
                LeftSet.Add(pair.Left);
                RightSet.Add(pair.Right);
            }
        }

        // 미러 쌍이면 반대쪽 PartType을 돌려준다. 단일 파츠면 false
        // TryGetMirror는 양방향 — Left→Right, Right→Left 둘 다 조회
        public static bool TryGetMirror(PartType partType, out PartType mirror)
        {
            return MirrorLookup.TryGetValue(partType, out mirror);
        }

        // 미러 쌍의 왼쪽(UI 대표) 여부
        public static bool IsLeft(PartType partType) => LeftSet.Contains(partType);

        // 미러 쌍의 오른쪽(UI 숨김·CSV skip 대상) 여부
        public static bool IsRight(PartType partType) => RightSet.Contains(partType);

        // 미러 쌍에 속하는지 여부(좌우 무관)
        public static bool IsMirrored(PartType partType) => MirrorLookup.ContainsKey(partType);
    }
}