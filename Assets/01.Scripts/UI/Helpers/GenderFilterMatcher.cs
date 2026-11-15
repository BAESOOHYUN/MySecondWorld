using MySecondWorld.Customization;
using MySecondWorld.Inventory;

namespace MySecondWorld.UI
{
    // 성별 필터에서 "이 아이템을 보여 줄까"를 판단하는 단 하나의 기준.
    // 인벤토리와 상점이 같이 쓴다.
    public static class GenderFilterMatcher
    {
        // itemGender가 null이면 공용(또는 장비가 아님) → 어떤 필터에서도 보인다.
        public static bool Matches(CharacterGenderFilter filter, Gender? itemGender)
        {
            if (filter == CharacterGenderFilter.All || itemGender == null) return true;
            return filter == CharacterGenderFilter.Female
                ? itemGender == Gender.Female
                : itemGender == Gender.Male;
        }
    }
}