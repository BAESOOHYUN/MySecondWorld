using UnityEngine;
using VContainer;
using MySecondWorld.Network;

namespace MySecondWorld.Customization
{
    public class CharacterSaveService
    {
        // 생성자 인젝션에서는 필드를 readonly로 선언할 수 있다.
        private readonly CharacterAppearanceSO _defaultSO;

        // 순수 C# 클래스는 VContainer가 직접 new로 만들 수 있기 때문에 VContainer가 new로 만듦
        // → 생성자에 바로 주입 가능 → [Inject] 생성자 인젝션
        [Inject]
        public CharacterSaveService(CharacterAppearanceSO defaultSO)
        {  
            _defaultSO = defaultSO; 
        }

        // 계정(이름)마다 별도 키 — 같은 기기에서 여러 계정을 테스트해도 안 섞이게
        private static string Key => $"CharacterAppearance_{PlayerSession.LocalPlayerName ?? "default"}";

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

        // 서버엔 없지만 내 기기엔 옷이 저장되어 있는 경우,
        // PlayerPrefs를 서버로 보내서 서버 세이브에 저장한다. (배포전 테스트 환경에서의 방어코드)
        public bool HasSaved() => PlayerPrefs.HasKey(Key);
    }
}