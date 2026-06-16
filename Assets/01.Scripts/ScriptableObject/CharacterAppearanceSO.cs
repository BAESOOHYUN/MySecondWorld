using UnityEngine;

namespace MySecondWorld.Customization
{
    // "기본값/프리셋" 데이터를 담는 자산
    // "기본 외형 1개" 또는 필요하다면 "NPC 종족별 프리셋 몇 개"처럼 원하는 개수만큼 생성
    // 예: DefaultCharacterAppearance.asset 1개, 나중에 GuardPreset.asset, VillagerPreset.asset 추가 가능
    [CreateAssetMenu(fileName = "CharacterAppearanceSO",
                     menuName = "Character/Appearance Preset",
                     order = 0)]
    public class CharacterAppearanceSO : ScriptableObject
    {
        public CharacterAppearanceData DefaultAppearance;
    }
}