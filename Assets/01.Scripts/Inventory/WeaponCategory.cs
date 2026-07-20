namespace MySecondWorld.Inventory
{
    public enum WeaponCategory
    {
        None,      // 미설정/파싱 실패 안전 기본값 (0번) — NecklaceHanging 등 의도적 미매핑 값도 여기로 떨어짐
        Sword,     // 검(한손/대검 포함)
        Dagger,    // 단검
        Axe,       // 도끼
        Polearm,   // 장병기(창/할버드/글레이브)
        Blunt,     // 둔기류 레거시 라벨(Dungeon/FantasyHeroCharacters 패키지) — 2026-07-20: MainHand 대상 제외
        Hammer,    // 망치류(2H 그립) — 2026-07-20: 적합한 애니메이션 없어 MainHand 대상 제외
        Mace,      // 철퇴/곤봉류(1H 그립) — 2026-07-20: 적합한 애니메이션 없어 MainHand 대상 제외
        Staff,     // 장지팡이 — 카탈로그는 유지, 2026-07-20: MainHand 대상 제외(캐스터는 Jewelry로 대체)
        Sceptre,   // 왕홀(단지팡이) — Staff와 동일 사유로 MainHand 대상 제외
        Shield,    // 방패 — OffHand 대상(이번 Phase 범위 밖)
        Knuckle,   // 너클 — 2026-07-20: 적합한 애니메이션 없어 MainHand 대상 제외
        Bow,       // 활 — 궁수 직업 MainHand
        Quiver,    // 화살통 — 장비형 액세서리, 이번 Phase MainHand 대상 아님
        Ring,      // 반지 — 캐스터 직업 MainHand, 손가락 소켓 부착
        Bracelet,  // 팔찌 — 캐스터 직업 MainHand, 손목 소켓 부착
        Necklace,  // 목걸이(Flat만) — 캐스터 직업 MainHand, 목 소켓 부착
        // NecklaceHanging(펜던트 목걸이)은 의도적으로 여기 없음 — item-draft.csv Category 값이
        // "NecklaceHanging"인 채로 남아있어 Enum.TryParse가 실패 → Category가 None으로
        // 떨어지고, CmdEquipMainHand 허용 목록에 None이 없어 장착이 자동 거부됨(2026-07-20)
    }
}