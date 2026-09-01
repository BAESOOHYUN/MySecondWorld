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
        // null이면 해당 인덱스는 무료 착용(요구 아이템 없음)
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
#endif
    }
}