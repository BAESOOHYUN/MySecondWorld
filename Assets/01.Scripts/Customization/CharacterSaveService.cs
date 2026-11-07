using VContainer;

namespace MySecondWorld.Customization
{
    // 이 클라가 "지금 내 캐릭터의 외형"으로 알고 있는 값을 메모리에만 들고 있는다.
    // 진짜 값은 서버가 갖고 있고, 접속할 때 서버가 알려 주는 값으로 채운다. (디스크에는 쓰지 않는다)
    public class CharacterSaveService
    {
        // 생성자 인젝션에서는 필드를 readonly로 선언할 수 있다.
        private readonly CharacterAppearanceSO _defaultSO;
        private CharacterAppearanceData _current;
        private bool _hasCurrent; // false = 서버에서 받은 값이 아직 없다

        // 순수 C# 클래스는 VContainer가 직접 new로 만들 수 있기 때문에 VContainer가 new로 만듦
        // → 생성자에 바로 주입 가능 → [Inject] 생성자 인젝션
        [Inject]
        public CharacterSaveService(CharacterAppearanceSO defaultSO)
        {  
            _defaultSO = defaultSO; 
        }

        public void Save(CharacterAppearanceData data)
        {
            _current = data;
            _hasCurrent = true;
        }

        public CharacterAppearanceData Load()
            => _hasCurrent ? _current : _defaultSO.DefaultAppearance;


        // 계정을 바꿨을 때 이전 계정의 기억이 남지 않게 비운다
        public void Clear() => _hasCurrent = false;
    }
}