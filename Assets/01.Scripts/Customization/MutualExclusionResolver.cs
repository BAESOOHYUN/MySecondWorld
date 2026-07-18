using System.Collections.Generic;

namespace MySecondWorld.Customization
{
    // ── 상호배제 규칙 분류 (2026-07-18 확정) ──────────────────────────────────────
    // Type-1 (임시 가림) : desired는 절대 안 바뀜. ComputeVisibleAppearance가 매번 새로 계산.
    //   - HelmetAttachment 착용 → (전제조건) Head가 No_Elements 상태로 강제 전환
    //   - HeadCoverings = No_Hair       → Hair 가림
    //   - HeadCoverings = No_FacialHair → FacialHair 가림
    //   - Head = No_Elements (직접 선택이든, 위 HelmetAttachment 전제조건으로 강제된 것이든)
    //     → Hair/Eyebrows/FacialHair/HeadCoverings 가림
    //     (Head 직접 선택 케이스는 기존엔 Type-2였으나 2026-07-18 확정으로 Type-1로 격상 —
    //      ApplyDirectSelectionRules에는 더 이상 이 케이스에 대한 분기가 없다. ComputeVisibleAppearance가
    //      desired.Head==No_Elements 조건으로 처리.)
    //
    // Type-2 (영구 취소) : desired 자체가 바뀜. ApplyDirectSelectionRules가 직접 수정.
    //   - 헬멧 쓴 채 Head(All_Elements) 직접 선택 → HelmetAttachment desired = 0
    //   - Hair 직접 선택       → HeadCoverings(No_Hair) desired = 0
    //   - FacialHair 직접 선택 → HeadCoverings(No_FacialHair) desired = 0
    //   - HeadCoverings/Hair/Eyebrows/FacialHair 직접 선택 시 desired.Head가 No_Elements면
    //     All_Elements로 전환 (이건 "Head를 직접 No_Elements(얼굴 부속 없는 헬멧용 헤드)로 바꿨을 때"와는 반대 방향 규칙이라 유지됨)
    // ─────────────────────────────────────────────────────────────────────────────
    public static class MutualExclusionResolver
    {
        // 여러 subtype 후보 중 사용자가 마지막으로 고른 인덱스가 있으면 그걸, 없으면 첫 후보를 반환.
        // 그냥 "몇 번 인덱스인지" 조회만 하는 순수 함수 — lastSelectedIndex는 읽기 전용으로만 참조.
        public static int FindSubtypeIndex(
            PartType part,
            Gender gender,
            string targetSubtype,
            IReadOnlyDictionary<(PartType part, string subtype), int> lastSelectedIndex,
            AvatarPartKeyTableSO keyTable)
        {
            int count = keyTable.GetCount(part, gender);

            // 사용자가 이전에 직접 고른 적 있는 해당 subtype이 있으면 그걸 우선 사용
            if (lastSelectedIndex.TryGetValue((part, targetSubtype), out int remembered)
                && remembered >= 0 && remembered < count
                && keyTable.GetSubType(part, gender, remembered) == targetSubtype)
            {
                return remembered;
            }

            // 사용자가 이전에 직접 고른 적 없으면(lastSelectedIndex)
            // 전체 후보 리스트를 처음부터 훑으면서 GetSubType(i) == targetSubtype인 첫 번째 인덱스를 찾음 
            // lastSelectedIndex(기억해둔 값)에 해당(part, subtype) 조합이 아직 없을 때를 위한 폴백 탐색
            for (int i = 0; i < count; i++)
            {
                if (keyTable.GetSubType(part, gender, i) == targetSubtype)
                    return i;
            }

            // 후보를 하나도 못 찾은 극단적 케이스(키 테이블 설정 누락) — 조용히 폴백.
            return count > 0 ? 0 : -1;
        }


        // desired = "사용자의 선택 의도" (저장됨, 지속됨)
        // visible = "그 의도를 지금 이 순간 화면에 구현한 결과" (저장 안 됨, 매번 재계산, "미리보기"보다는 "실제 표시값"에 가까움)


        // Stage 1 — desired를 직접 수정하는 "영구 취소" 규칙.
        // changedPart/newIndex는 "방금 사용자가 요청한 변경"이며,
        // 호출 전에 이미 desired[changedPart] = newIndex는 반영되어 있어야 한다(호출자 책임).
        public static void ApplyDirectSelectionRules(
            ref CharacterAppearanceData desired,
            PartType changedPart,
            int newIndex,
            Gender gender,
            AvatarPartKeyTableSO keyTable,
            IReadOnlyDictionary<(PartType part, string subtype), int> lastSelectedIndex)
        { 
            switch (changedPart)
            {

                case PartType.Head:
                    {
                        string subtype = keyTable.GetSubType(PartType.Head, gender, newIndex);
                        if (subtype == "All_Elements")
                            desired.HelmetAttachment = 0;// 헬멧 쓴 채 새 얼굴형 고르면 헬멧 영구 해제

                        // subtype == "No_Elements(헬멧)"인 경우 → 아무 것도 안 함(2026-07-18 확정).
                        // Hair/Eyebrows/FacialHair/HeadCoverings의 desired는 그대로 둔다.
                        // ComputeVisibleAppearance가 desired.Head==No_Elements 조건으로 visible만 가린다.
                    }
                    break;

                case PartType.HeadCoverings when newIndex > 0: // 악세사리 (모자, 복면, 후드 등) 선택 시
                    {
                        if (keyTable.GetSubType(PartType.Head, gender, desired.Head) == "No_Elements") // 헬멧 쓰고 있었으면
                            desired.Head = FindSubtypeIndex(PartType.Head, gender, "All_Elements", lastSelectedIndex, keyTable); // 맨얼굴로 돌림

                        desired.HelmetAttachment = 0; // 헤드커버와 헬멧장신구는 동시 착용 불가 — 새 헤드커버를 고르면 헬멧 취소
                    }
                    break;

                case PartType.Hair when newIndex >= 0:
                    {
                        if (keyTable.GetSubType(PartType.HeadCoverings, gender, desired.HeadCoverings) == "No_Hair") // 악세사리로 후드 쓰고 있었으면
                            desired.HeadCoverings = 0; // 후드 착용 해제

                        if (keyTable.GetSubType(PartType.Head, gender, desired.Head) == "No_Elements")
                            desired.Head = FindSubtypeIndex(PartType.Head, gender, "All_Elements", lastSelectedIndex, keyTable);
                    }
                    break;

                case PartType.Eyebrows when newIndex >= 0:
                    {
                        if (keyTable.GetSubType(PartType.Head, gender, desired.Head) == "No_Elements")
                            desired.Head = FindSubtypeIndex(PartType.Head, gender, "All_Elements", lastSelectedIndex, keyTable);
                    }
                    break;

                case PartType.FacialHair when newIndex >= 0:
                    {
                        if (keyTable.GetSubType(PartType.HeadCoverings, gender, desired.HeadCoverings) == "No_FacialHair") // 악세사리로 복면 쓰고 있었으면
                            desired.HeadCoverings = 0; //복면 착용 해제

                        if (keyTable.GetSubType(PartType.Head, gender, desired.Head) == "No_Elements")
                            desired.Head = FindSubtypeIndex(PartType.Head, gender, "All_Elements", lastSelectedIndex, keyTable);
                    }
                    break;

                    // PartType.HelmetAttachment: 아무 것도 안 함
                    // — 헬멧은 순수 Type-1이라 desired의 다른 필드를 절대 건드리지 않는다.
                    // 이게 버그 3(헬멧 취소)이 사라지는 이유.
            }
        }

        // Stage 2 — desired는 절대 변형하지 않고 매번 새 값을 반환하는 "임시 가림" 규칙.
        // 상태를 전혀 기억하지 않는 순수 함수 — 몇 번을 호출해도 같은 desired에 대해 항상 같은 visible을 반환한다.
        public static CharacterAppearanceData ComputeVisibleAppearance(
            CharacterAppearanceData desired,
            Gender gender,
            AvatarPartKeyTableSO keyTable,
            IReadOnlyDictionary<(PartType part, string subtype), int> lastSelectedIndex)
        {
            var visible = desired; // struct 복사

            bool helmetWorn = desired.HelmetAttachment > 0;
            bool headHidesElements = keyTable.GetSubType(PartType.Head, gender, desired.Head) == "No_Elements";
            bool hcHidesHair = desired.HeadCoverings > 0
                && keyTable.GetSubType(PartType.HeadCoverings, gender, desired.HeadCoverings) == "No_Hair";
            bool hcHidesFacialHair = desired.HeadCoverings > 0
                && keyTable.GetSubType(PartType.HeadCoverings, gender, desired.HeadCoverings) == "No_FacialHair";

            // 헬멧 장식물을 착용한 것이든, 사용자가 직접 고른 No_Elements 헤드든 구분 없이 동일하게 처리
            bool faceHidden = helmetWorn || headHidesElements;

            // faceHidden=false                       → 가릴 필요 없음, desired.Head 그대로.
            // faceHidden=true & headHidesElements     → 이미 desired.Head 자체가 No_Elements(직접 선택) → 그대로 사용.
            // faceHidden=true & !headHidesElements    → helmetWorn 때문에 가려야 하는데 desired.Head는 아직
            //                                            All_Elements로 남아있음 → No_Elements 후보를 새로 찾아 대신 보여줌.
            visible.Head = faceHidden
                ? (headHidesElements ? desired.Head : FindSubtypeIndex(PartType.Head, gender, "No_Elements", lastSelectedIndex, keyTable))
                : desired.Head;

            // 얼굴이 가려졌거나(faceHidden), 후드(HeadCoverings=No_Hair)가 덮고 있으면 -1(없음), 아니면 desired 그대로
            visible.Hair = (faceHidden || hcHidesHair) ? -1 : desired.Hair;

            // 눈썹은 HeadCoverings 중 눈썹만 따로 가리는 subtype이 없음(No_Eyebrows 없음) — faceHidden만으로 판단
            visible.Eyebrows = faceHidden ? -1 : desired.Eyebrows;

            // 얼굴이 가려졌거나(faceHidden), 복면(HeadCoverings=No_FacialHair)이 덮고 있으면 -1(없음), 아니면 desired 그대로
            visible.FacialHair = (faceHidden || hcHidesFacialHair) ? -1 : desired.FacialHair;

            // 얼굴이 가려지면 헤드커버 자체도 벗김(0=착용 안 함) — 씌울 얼굴(Head)이 없어졌으니 그 위에 덧씌울 것도 없음
            visible.HeadCoverings = faceHidden ? 0 : desired.HeadCoverings;

            // HelmetAttachment는 얼굴을 가리는 "원인" 쪽이지 가려지는 "대상"이 아니므로 항상 desired 그대로 통과
            visible.HelmetAttachment = desired.HelmetAttachment;

            return visible;
        }
    }
}