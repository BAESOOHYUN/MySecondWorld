using Cysharp.Threading.Tasks;
using Mirror;
using VContainer;
using MySecondWorld.Customization;

namespace MySecondWorld.Network
{
    public class NetworkCharacterAppearance : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnAppearanceChanged))]
        private CharacterAppearanceData _appearance;

        private CharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _avatarPartKeyTable;

        [Inject]
        public void Construct(CharacterCustomizer customizer, AvatarPartKeyTableSO avatarPartKeyTable)
        {
            _customizer = customizer;
            _avatarPartKeyTable = avatarPartKeyTable;
        }

        [Command]
        public void CmdSetAppearance(CharacterAppearanceData data)
        {
            var t = _avatarPartKeyTable;

            // IsDefined 이 값이 enum에 실제로 정의된 값인지 검증 (해킹 등으로 이상한 값 들어올 가능성 방어)
            if (!System.Enum.IsDefined(typeof(Gender), data.Gender))
                data.Gender = _appearance.Gender;

            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                int count = t.GetCount(part, data.Gender);
                data[part] = Clamp(data[part], count, _appearance[part]);
            }

            _appearance = data;
        }

        private void OnAppearanceChanged(CharacterAppearanceData old, CharacterAppearanceData next)
        {
            _customizer.ApplyAppearance(next).Forget();
        }

        private static int Clamp(int value, int count, int fallback)
            => value >= 0 && value < count ? value : fallback;
    }
}
