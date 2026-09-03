using UnityEngine;
using VContainer;

namespace MySecondWorld.Customization
{
    public class CharacterSaveService
    {
        private const string Key = "CharacterAppearance";
        // 생성자 인젝션에서는 필드를 readonly로 선언할 수 있다.
        private readonly CharacterAppearanceSO _defaultSO;

        // 순수 C# 클래스는 VContainer가 직접 new로 만들 수 있기 때문에 VContainer가 new로 만듦
        // → 생성자에 바로 주입 가능 → [Inject] 생성자 인젝션
        [Inject]
        public CharacterSaveService(CharacterAppearanceSO defaultSO)
        {  
            _defaultSO = defaultSO; 
        }

        public void Save(CharacterAppearanceData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public CharacterAppearanceData Load()
        {
            if (!PlayerPrefs.HasKey(Key))
                return _defaultSO.DefaultAppearance;

            return JsonUtility.FromJson<CharacterAppearanceData>(PlayerPrefs.GetString(Key));
        }
    }
}