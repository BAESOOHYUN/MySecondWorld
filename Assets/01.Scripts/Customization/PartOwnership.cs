
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    // 소유 판정과 검증을 한 곳으로 모았다.
    // 같은 규칙이 두 군데에 따로 있습니다
    // 다른 점은 "소유 여부를 누구에게 물어보나"(클라는 Provider, 서버는 Inventory)뿐이다.

    //           |   클라 CharacterCustomizeUI    |  서버 CmdSetAppearance
    // --------------------------------------------------------------------
    // 소유 판정 |  IsPartOwned(미러 쌍 포함) →  |   t.GetRequiredItem + 
    //           |   _inventoryProvider.IsOwned   |   _inventory.Contains
    // --------------------------------------------------------------------
    // 검증 반복 |    ValidateOwnership           |   같은 모양의 foreach (성별이 바뀌면 0)

    public static class PartOwnership
    {
        #region  소유 판정

        public static bool IsPartOwned(AvatarPartKeyTableSO table, PartType part, Gender gender, int index,
                               System.Func<EquipmentItem, bool> hasItem)
        {
            if (IsOwnedSelf(table, part, gender, index, hasItem)) return true;

            // 미러 쌍은 OR — 반대쪽만 소유해도 해금
            return MirroredPartPairs.TryGetMirror(part, out var mirror)
                && IsOwnedSelf(table, part, gender, index, hasItem);
        }


        // 커스터마이징 UI에서 썸네일을 그릴 때, 그 파츠 후보가 요구하는 아이템을 플레이어가 갖고 있는지 확인해서
        // 없으면 자물쇠 아이콘(SetLocked)을 씌움. 클라이언트 쪽 "미리 보여주되 잠금 표시"용.
        public static bool IsOwnedSelf(AvatarPartKeyTableSO table, PartType part, Gender gender, int index,
                               System.Func<EquipmentItem, bool> hasItem)
        {
            // 맨몸(0)·없음(-1)은 기본 해금
            if (index <= 0) return true;

            var requiredItem = table.GetRequiredItem(part, gender, index);
            return requiredItem == null || hasItem(requiredItem);
        }

        #endregion


        #region 소유 검증

        // 묻는 것 : 이 외형을 입을 자격이 있나 (그 아이템을 갖고 있나)
        // 하는 일 : 자격 없는 파츠를 되돌림
        // 입력    : 외형 데이터

        // 미소유 파츠를 fallback 값으로 되돌린 사본을 반환한다.
        // fallback 지정 시: 그 값(보통 "직전 저장값")으로 되돌림 — 서버 CmdSetAppearance와 동일하게
        //   "거부하고 이전 상태 유지" 의미. fallback의 그 값마저 미소유면(예: 그사이 아이템 상실) 맨몸(0)까지 떨어뜨림.
        // fallback 생략 시: 곧바로 맨몸(0)으로 되돌림 — 되돌아갈 "직전"이 없는 자기 자신 검증(로드 시 자가 정화)용.
        public static CharacterAppearanceData ValidateOwnership(AvatarPartKeyTableSO table, CharacterAppearanceData data,
                                        System.Func<EquipmentItem, bool> hasItem, CharacterAppearanceData? fallback = null)
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                if (data[part] <= 0) continue;  // 0번(맨몸) 및 -1(없음)만 무료 통과
                if (IsPartOwned(table, part, data.Gender, data[part], hasItem)) continue; // 이미 정당하게 소유한 아이템이면 검증 불필요.

                // 성별이 바뀌었으면 직전 값의 번호는 다른 성별의 옷이라 되돌아갈 곳이 못 된다 → 맨몸(0)
                bool genderChanged = fallback.HasValue && fallback.Value.Gender != data.Gender;
                // fallback 있으면 fallback으로 되돌림. 없으면 0
                int fallbackIndex = (fallback.HasValue && !genderChanged) ? fallback.Value[part] : 0;
                // fallback 있는데 허용된 아이템이 아닌 경우 0.   ex) lock된 아이템
                if (fallbackIndex > 0 && !IsPartOwned(table, part, fallback.Value.Gender, fallbackIndex, hasItem))
                    fallbackIndex = 0; // fallback 값조차 지금은 미소유면 더 물러날 곳이 없어 맨몸으로 되돌림

                data[part] = fallbackIndex;
            }

            // 투구 장식 규칙 = 투구 장식은 헬멧(No_Elements)일 때만 허용
            if (data.HelmetAttachment > 0
                && !MutualExclusionResolver.IsHelmetAttachmentAllowed(data, data.Gender, table))
                data.HelmetAttachment = 0;

            return data;
        }

        #endregion
    } 
}