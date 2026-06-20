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

        [ContextMenu("Set Default Colors")]
        private void SetDefaultColors()
        {
            var d = DefaultAppearance;
            d.PrimaryColor = Color.white;
            d.SecondaryColor = Color.white;
            d.LeatherPrimaryColor = new Color(0.55f, 0.35f, 0.15f);
            d.LeatherSecondaryColor = new Color(0.35f, 0.22f, 0.08f);
            d.MetalPrimaryColor = new Color(0.75f, 0.75f, 0.75f);
            d.MetalSecondaryColor = new Color(0.55f, 0.55f, 0.55f);
            d.MetalDarkColor = new Color(0.25f, 0.25f, 0.25f);
            d.HairColor = new Color(0.18f, 0.10f, 0.04f);
            d.SkinColor = new Color(0.90f, 0.75f, 0.60f);
            d.StubbleColor = new Color(0.40f, 0.30f, 0.22f);
            d.ScarColor = new Color(0.80f, 0.62f, 0.62f);
            d.EyesColor = new Color(0.28f, 0.50f, 0.80f);
            d.BodyArtColor = Color.white;
            DefaultAppearance = d;
        }
    }

}