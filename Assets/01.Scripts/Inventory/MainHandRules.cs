using System.Collections.Generic;

namespace MySecondWorld.Inventory
{
    // 메인핸드로 장착할 수 있는 무기 종류. 서버(검증)와 UI(장착 버튼 표시)가 같은 기준을 쓰도록 한 곳에 둔다.
    public static class MainHandRules
    {
        // 이 Category 이외에는 MainHand 장착 자체를 거부한다(2026-07-20 스코프 조정 —
        // 둔기류/너클/Staff/Sceptre는 맞는 애니메이션이 없어 이번 Phase 제외).
        // TODO: 해당 카테고리의 애니메이션이 생기면 허용
        private static readonly HashSet<WeaponCategory> Allowed = new()
        {
            WeaponCategory.Sword, WeaponCategory.Dagger, WeaponCategory.Axe, WeaponCategory.Polearm,
            WeaponCategory.Bow,
            WeaponCategory.Ring, WeaponCategory.Bracelet, WeaponCategory.Necklace,
        };

        public static bool IsAllowed(WeaponCategory category) => Allowed.Contains(category);
    }
}