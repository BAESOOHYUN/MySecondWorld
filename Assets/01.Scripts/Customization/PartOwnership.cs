
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    // 소유 판정 · 소유 검증 · 장착 칸 대응을 한 곳으로 모았다. 클라(UI)와 서버가 같은 규칙을 쓰게 하려는 것.
    // 규칙은 여기 한 벌뿐이고, 다른 점은 "소유 여부를 누구에게 물어보나"(hasItem 대리자: 클라는 Provider, 서버는 Inventory)뿐이다.
    // (서버 CmdSetAppearance도 2026-11-09부터 이 클래스의 ValidateOwnership을 쓴다)
    //
    //  함수               | 묻는 것                                         | 부르는 곳
    //  -------------------+-------------------------------------------------+---------------------------------------------
    //  IsPartOwned        | 이 파츠 후보의 아이템을 갖고 있나 (좌우 짝 포함)  | 클라 CharacterCustomizeUI (자물쇠 표시)
    //  ValidateOwnership  | 이 외형 전체에서 자격 없는 파츠는 어디인가        | 서버 CmdSetAppearance, 클라 로드 시 자가 정화
    //  IsItemForPart      | 이 가방 칸의 아이템이 이 파츠의 근거인가 (짝 포함) | 서버 ValidateEquippedPartSlots, 클라 ReconcileEquippedSlots

    public static class PartOwnership
    {
        #region  소유 판정

        public static bool IsPartOwned(AvatarPartKeyTableSO table, PartType part, Gender gender, int index,
                               System.Func<EquipmentItem, bool> hasItem)
        {
            if (IsOwnedSelf(table, part, gender, index, hasItem)) return true;

            // 미러 쌍은 OR — 반대쪽만 소유해도 해금
            return MirroredPartPairs.TryGetMirror(part, out var mirror)
                && IsOwnedSelf(table, mirror, gender, index, hasItem);
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

        #region 장착 칸 대응

        // 묻는 것 : 이 가방 칸의 아이템이, 이 파츠(part, index)를 입고 있는 근거로 인정되는가
        // 하는 일 : 자기 파츠의 요구 아이템이면 true, 아니면 좌우 짝 파츠의 요구 아이템이어도 true
        // 입력    : 파츠 표, 파츠 종류, 성별, 모양 번호, 가방 칸의 아이템(빈 칸이면 null)
        //
        // 만든 이유 (버그):
        //   좌우 짝 파츠는 왼쪽·오른쪽이 서로 다른 아이템이다.
        //   그런데 '왼손 장갑' 하나만 장착해도 오른손 모양까지 같이 입혀지고, 두 파츠가 같은 가방 칸을 기록한다.
        //   예전엔 오른손을 "오른손 장갑이 그 칸에 있나"로만 검사해서 항상 "틀림"이 되었고,
        //   왼손 장갑을 땅에 버리면 페이퍼돌 슬롯과 캐릭터는 오른손 장갑만 낀 모습으로 보였다.

        // 서버는 "맞는 것만 남기는" 검증으로 사용 NetworkCharacterAppearance.ValidateEquippedPartSlots
        // 클라는 "맞지 않는 것을 지우는" 정리로 사용 CharacterAppearanceService.ReconcileEquippedSlots
        public static bool IsItemForPart(AvatarPartKeyTableSO table, PartType part, Gender gender, int index,
                                     EquipmentItem item)
        {
            if (item == null) return false;
            // ① 이 파츠 자신의 요구 아이템이 item(그 칸의 아이템)과 같으면 인정
            if (table.GetRequiredItem(part, gender, index) == item) return true;
            // ② 아니면 좌우 짝 파츠의 요구 아이템(같은 모양 번호)이 item과 같은지 본다. 같으면 인정
            return MirroredPartPairs.TryGetMirror(part, out var mirror)
                && table.GetRequiredItem(mirror, gender, index) == item;
        }

        #endregion
    } 
}