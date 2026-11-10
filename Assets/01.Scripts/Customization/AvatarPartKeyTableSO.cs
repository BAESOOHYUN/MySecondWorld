using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.Customization
{
    // PartType별로 Addressable 주소 리스트를 Inspector에서 직접 채우는 방식
    // 서버 검증(IsValidIndex)의 count도 여기서 가져올 수 있고, Addressables 키 매핑도 같은 곳에서 해결
    [CreateAssetMenu(fileName = "AvatarPartKeyTableSO",
                     menuName = "Character/Avatar Part Key Table")]
    public class AvatarPartKeyTableSO : ScriptableObject
    {
        // PartType은 25개. List<PartEntry>().Count = 25개
        // 몸 파츠 14개(Hair~LegRight)에 공용 액세서리 11개(Ear~HipsAttachment)를 더한 수
        [Serializable]
        public class PartEntry
        {
            public PartType PartType;

            // List index가 곧 CharacterAppearanceData의 int 인덱스
            
            // 성별별 후보 목록 (해당 성별에서만 등장하는 파츠)
            public List<AssetReferenceGameObject> MaleParts = new();
            public List<AssetReferenceGameObject> FemaleParts = new();
            // 성별 무관 공용 후보 목록 (액세서리류)
            public List<AssetReferenceGameObject> AllGenderParts = new();

            // 파츠 리스트와 "동일 인덱스" 대응. Populator가 서브폴더 이름에서 채움.
            // ex) Head: "All_Elements" / "No_Elements", HeadCoverings: "Base_Hair" / "No_FacialHair" / "No_Hair"
            public List<string> MalePartSubTypes = new();
            public List<string> FemalePartSubTypes = new();
            public List<string> AllGenderPartSubTypes = new();

            // 파츠 리스트와 "동일 인덱스"의 썸네일. Populator가 같은 패스에서 이름 매칭으로 채움.
            public List<string> MaleThumbnailNames = new();
            public List<string> FemaleThumbnailNames = new();
            public List<string> AllGenderThumbnailNames = new();

            // 인벤토리 > 아이템 장착 관련
            // 파츠 리스트와 "동일 인덱스" 대응. null이면 "요구 아이템 없음"
            // "Torso 파츠 후보 중 3번(MaleParts[3])을 입으려면, 인벤토리에 이 EquipmentItem을 갖고 있어야 한다"는 자격 조건을 가리킴
            // ex) 그룹 A 파츠는 전부 비워둠, 그룹 B/C PartType(Hand, Arm, Torso, Legs 등) 안에서도 index 0은 "맨몸/기본 상태"라 무료
            public List<EquipmentItem> MaleRequiredItems = new();
            public List<EquipmentItem> FemaleRequiredItems = new();
            public List<EquipmentItem> AllGenderRequiredItems = new();
        }

        [Header("썸네일 아틀라스")]
        public AssetReferenceT<SpriteAtlas> ThumbnailAtlas;

        public List<PartEntry> Entries = new();


        // ─────── 리스트 통째로 가져오기 (UI가 후보 목록 쭉 훑을 때 씀) ─────── 

        // Gender에 맞는 전용 리스트 + 공용 리스트를 합쳐서 반환
        // 순서를 고정해야 인덱스가 클라이언트/서버 양쪽에서 항상 같은 의미를 가짐
        // GetCandidates가 매번 Concat+ToList로 새 리스트를 만드는 점은 호출 빈도가 낮으면(파츠 변경 시점에만) 문제 없지만,
        // 매 프레임 호출되는 곳에서 쓰면 GC 부담이 생길 수 있다
        public List<AssetReferenceGameObject> GetCandidates(PartType part, Gender gender)
        {
            var entry = Entries.Find(e => e.PartType == part);
            if (entry == null)
                return new List<AssetReferenceGameObject>();
            var genderList = gender == Gender.Male ? entry.MaleParts : entry.FemaleParts;
            return genderList.Concat(entry.AllGenderParts).ToList();
        }

        // GetCandidates와 동일한 순서(전용 + AllGender)로 합쳐서 반환.
        // → 반환 리스트의 index i 가 GetCandidates(part, gender)[i] 와 같은 파츠를 가리킨다.
        public List<string> GetThumbnailNames(PartType part, Gender gender)
        {
            var entry = Entries.Find(e => e.PartType == part);
            if (entry == null)
                return new List<string>();
            var genderList =
                gender == Gender.Male ? 
                entry.MaleThumbnailNames : entry.FemaleThumbnailNames;
            return genderList.Concat(entry.AllGenderThumbnailNames).ToList();
        }

        public int GetCount(PartType part, Gender gender) => GetCandidates(part, gender).Count;

        // 이 후보(index)가 성별 전용인지 공용(AllGender)인지 알려 준다.
        // 목록은 "성별 전용 → 공용" 순서라서, 전용 개수보다 작으면 전용이다.
        // true = 특정 성별 전용, false = 공용
        public bool IsGenderSpecific(PartType part, Gender gender, int index)
        {
            var entry = Entries.Find(e => e.PartType == part);
            if (entry == null) return false;
            int specificCount = (gender == Gender.Male ? entry.MaleParts : entry.FemaleParts).Count;
            return index >= 0 && index < specificCount;
        }

        // ── 성별 번호 대응 ──────────────────────────────────────────────
        // 같은 옷이 남자 목록과 여자 목록에서 다른 번호를 가질 수 있어, 성별을 바꿀 때 번호를 환산한다.
        //
        // 목록은 "성별 전용(맨몸 포함) → 공용" 순서다. 예) 남자 전용 4칸, 여자 전용 3칸, 공용 2개
        //
        //  번호 | 남자 목록   | 여자 목록
        //  -----+-------------+-------------
        //   0   | 맨몸        | 맨몸
        //   1   | 남 전용 A   | 여 전용 A
        //   2   | 남 전용 B   | 여 전용 B
        //   3   | 남 전용 C   | 공용 X
        //   4   | 공용 X      | 공용 Y
        //   5   | 공용 Y      | (없음)
        //
        // 전용 칸 수(fromCount/toCount)는 맨몸 칸을 포함해서 센다. 위 예: 남 4, 여 3.
        // from = 지금 번호가 속한 성별, to = 옮겨 갈 성별
        //
        // 규칙 (남자 → 여자로 바꾼다면)
        //  - 번호 >= fromCount (공용): 앞의 전용 칸 수가 달라 밀린다 → index - fromCount + toCount
        //      남 4(X) → 4 - 4 + 3 = 여 3,  남 5(Y) → 5 - 4 + 3 = 여 4
        //  - 번호 < fromCount (전용): 여자 전용에도 그 번호가 있으면(번호 < toCount) 같은 번호, 없으면 -1
        //      남 1(A) → 여 1,  남 2(B) → 여 2,  남 3(C) → -1 (여자엔 짝이 없음)
        //  - 0(맨몸)·-1(없음), 성별이 같을 때: 그대로
        public int MapIndexToGender(PartType part, Gender from, Gender to, int index)
        {
            if (index <= 0 || from == to) return index;       // 맨몸·없음, 같은 성별은 환산 불필요

            var entry = Entries.Find(e => e.PartType == part); // 이 파츠 종류의 남/여/공용 목록 묶음
            if (entry == null) return -1;

            int fromCount = (from == Gender.Male ? entry.MaleParts : entry.FemaleParts).Count; // 출발 쪽 전용 칸 수
            int toCount = (to == Gender.Male ? entry.MaleParts : entry.FemaleParts).Count;     // 도착 쪽 전용 칸 수

            if (index >= fromCount) return index - fromCount + toCount; // 공용: 공용 안 순서를 도착 쪽 공용 시작 위치에 얹는다
            return index < toCount ? index : -1;                        // 전용: 같은 번호, 도착 쪽에 없으면 -1
        }

        // ─────── 인덱스 하나로 콕 집어 조회하기 (실제 장착/적용 시점에 씀) ─────── 

        // 프리팹을 가리키는 실제 키
        // data.Head == 3 이라면
        // → GetCandidates(PartType.Head, data.Gender) 리스트의 인덱스 3번 원소
        // → 그게 가리키는 AssetReferenceGameObject로 Addressables.InstantiateAsync() 호출
        public AssetReferenceGameObject GetAssetReference(PartType part, Gender gender, int index)
        {
            var candidates = GetCandidates(part, gender);
            if (index < 0 || index >= candidates.Count)
                return null;
            return candidates[index];
        }

        // GetCandidates/GetThumbnailNames와 동일 순서(전용 + AllGender)로 서브타입 반환
        public string GetSubType(PartType part, Gender gender, int index)
        {
            var entry = Entries.Find(e => e.PartType == part);
            if (entry == null) 
                return string.Empty;
            var genderList = 
                gender == Gender.Male ? 
                entry.MalePartSubTypes : entry.FemalePartSubTypes;
            var combined = genderList.Concat(entry.AllGenderPartSubTypes).ToList();
            return (index >= 0 && index < combined.Count) ? combined[index] : string.Empty;
        }

        // "이 파츠 후보를 쓰려면 인벤토리에 어떤 아이템을 갖고 있어야 하는가"를 조회하는 함수
        // GetCandidates/GetAssetReference와 동일 순서(전용 + AllGender)로 요구 아이템 반환
        // GetRequiredItem은 범위를 벗어나도 예외 없이 null을 돌려준다.
        // 범위를 벗어난 번호는 null → "요구 아이템 없음" → 무료로 통과
        public EquipmentItem GetRequiredItem(PartType part, Gender gender, int index)
        {
            var entry = Entries.Find(e => e.PartType == part);
            if (entry == null)
                return null;
            var genderList = gender == Gender.Male ?
                entry.MaleRequiredItems : entry.FemaleRequiredItems;
            var combined = genderList.Concat(entry.AllGenderRequiredItems).ToList();
            return (index >= 0 && index < combined.Count) ? combined[index] : null;
        }

        // 아이템 → (PartType, index) 역방향 조회. 인벤토리 "장착" 버튼에서 사용.
        // GetRequiredItem과 반대 방향 — 못 찾으면 false.
        // "이 아이템이 어느 PartType 소속인지조차 모르는" 역방향 조회라서 entry를 하나씩 열어보면서
        // 그 안의 RequiredItems 리스트에 이 아이템이 있는지 뒤지는 수밖에 없다.
        public bool TryFindPartIndex(EquipmentItem item, Gender gender, out PartType part, out int index)
        {
            // "이 아이템을 얻으면 어느 파츠 슬롯이 풀리는가"를 조회.
            foreach (var entry in Entries) //25개의 PartsType 상자
            {
                var genderList = gender == Gender.Male ? entry.MaleRequiredItems : entry.FemaleRequiredItems;
                // combined에서 item을 찾음
                var combined = genderList.Concat(entry.AllGenderRequiredItems).ToList();
                int i = combined.IndexOf(item);
                // 그 아이템이 어느 상자(파츠)안에 있는지 찾음
                if (i >= 0)
                {
                    part = entry.PartType;
                    index = i;
                    return true;
                }
            }

            part = default;
            index = -1;
            return false;
        }

        /// <summary>
        /// 해당 아이템이 특정 성별의 캐릭터에게 적합한지 확인한다.
        /// 반대 성별 전용 요구 아이템 목록에 포함되어 있다면 false를 반환한다.
        /// </summary>
        public bool IsItemMatchingGender(EquipmentItem item, Gender gender)
        {
            var g = GetItemGender(item); return g == null || g == gender;
        }

        // 이 아이템이 어느 성별 전용인지 알려 준다. 공용·무기·못 찾으면 null.
        public Gender? GetItemGender(EquipmentItem item)
        {
            if (item == null) return null;
            foreach (var entry in Entries)
            {
                if (entry.MaleRequiredItems != null && entry.MaleRequiredItems.Contains(item)) return Gender.Male;
                if (entry.FemaleRequiredItems != null && entry.FemaleRequiredItems.Contains(item)) return Gender.Female;
            }
            return null;
        }


#if UNITY_EDITOR
        [ContextMenu("Auto-Populate Thumbnail Names")]
        private void AutoPopulateThumbnailNames()
        {
            foreach (var entry in Entries)
            {
                entry.MaleThumbnailNames = entry.MaleParts
                    .Select(r => GetAssetName(r) + "_Static").ToList();
                entry.FemaleThumbnailNames = entry.FemaleParts
                    .Select(r => GetAssetName(r) + "_Static").ToList();
                entry.AllGenderThumbnailNames = entry.AllGenderParts
                    .Select(r => GetAssetName(r) + "_Static").ToList();
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[KeyTable] 썸네일 이름 자동 채우기 완료");
        }

        private string GetAssetName(AssetReferenceGameObject r)
        {
            if (r == null || string.IsNullOrEmpty(r.AssetGUID)) return string.Empty;
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(r.AssetGUID);
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        // 에디터에서 우클릭 메뉴로 실행. EquipmentSlotMap의 HeadCoverings 슬롯이 기대는 약속을 확인한다.
        [ContextMenu("Validate Bare Head Has No Required Item")]
        private void ValidateBareHeadHasNoRequiredItem()
        {
            var entry = Entries.Find(e => e.PartType == PartType.Head);
            if (entry == null) { Debug.LogWarning("[AvatarPartKeyTable] Head 항목이 없음", this); return; }

            int bad = CountBareFacesWithRequiredItem(entry.MalePartSubTypes, entry.MaleRequiredItems, "Male")
                    + CountBareFacesWithRequiredItem(entry.FemalePartSubTypes, entry.FemaleRequiredItems, "Female")
                    + CountBareFacesWithRequiredItem(entry.AllGenderPartSubTypes, entry.AllGenderRequiredItems, "AllGender");

            if (bad == 0) Debug.Log("[AvatarPartKeyTable] 맨얼굴 요구 아이템 점검 통과", this);


        }
        // 맨얼굴(서브타입 All_Elements)인데 요구 아이템이 연결된 칸을 세어 경고한다. 0이면 정상.
        //
        // 맨얼굴은 누구나 쓰는 무료 얼굴형이라 요구 아이템이 없어야(null) 한다.
        // 요구 아이템이 있는 건 투구(No_Elements)뿐이다.
        // EquipmentSlotMap.HeadCoverings 슬롯은 이 약속(맨얼굴 = 아이템 없음)에 기대고 있다.
        //
        // subTypes[i]와 required[i]는 같은 번호끼리 한 후보를 이룬다. (AvatarPartKeyTableSO의 남/여/공용 목록 공통 규칙)
        // label은 어느 목록인지(Male/Female/AllGender) 로그에 보여 주는 이름이다.
        // 리스트 하나를 검사해 문제 개수를 돌려주는 함수
        // 서브타입 목록과 요구 아이템 목록을 같은 번호끼리 짝지어, 맨얼굴인데 요구 아이템이 달린 칸을 센다.
        // label은 어느 목록인지(Male/Female/AllGender) 로그에 보여 주기 위한 이름.
        private int CountBareFacesWithRequiredItem(IReadOnlyList<string> subTypes,
                            IReadOnlyList<EquipmentItem> required, string label)
        {
            int bad = 0;
            for (int i = 0; i < subTypes.Count && i < required.Count; i++)
            {
                if (subTypes[i] == PartSubTypes.HeadAllElements && required[i] != null)
                {
                    Debug.LogWarning($"[AvatarPartKeyTable] 맨얼굴(Head {label}[{i}])에 요구 아이템이 달려 있음: {required[i].name}", this);
                    bad++;
                }
            }
            return bad;
        }

#endif
    }
}