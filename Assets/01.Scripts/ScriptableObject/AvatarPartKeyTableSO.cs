using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

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
        }

        public List<PartEntry> Entries = new();

        // Gender에 맞는 전용 리스트 + 공용 리스트를 합쳐서 반환
        // 순서를 고정해야 인덱스가 클라이언트/서버 양쪽에서 항상 같은 의미를 가짐
        // etCandidates가 매번 Concat+ToList로 새 리스트를 만드는 점은 호출 빈도가 낮으면(파츠 변경 시점에만) 문제 없지만,
        // 매 프레임 호출되는 곳에서 쓰면 GC 부담이 생길 수 있다
        public List<AssetReferenceGameObject> GetCandidates(PartType part, Gender gender)
        {
            var entry = Entries.Find(e => e.PartType == part);
            if (entry == null)
                return new List<AssetReferenceGameObject>();
            var genderList = gender == Gender.Male ? entry.MaleParts : entry.FemaleParts;
            return genderList.Concat(entry.AllGenderParts).ToList();
        }


        public int GetCount(PartType part, Gender gender)
        {
            return GetCandidates(part, gender).Count;
        }

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
    }
}