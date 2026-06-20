using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using VContainer;
using MySecondWorld.Customization;
using MySecondWorld.UI;

namespace MySecondWorld.Network
{
    public class NetworkCharacterAppearance : NetworkBehaviour
    {
        private const string PrefKeyCustomized = "CharacterCustomized";

        [SyncVar(hook = nameof(OnAppearanceChanged))]
        private CharacterAppearanceData _appearance;

        private ObservableCharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _avatarPartKeyTable; 
        private CharacterCustomizeUI _customizeUI;

        [Inject]
        public void Construct(
            ObservableCharacterCustomizer customizer,
            AvatarPartKeyTableSO avatarPartKeyTable,
            CharacterCustomizeUI customizeUI)
        {
            _customizer = customizer;
            _avatarPartKeyTable = avatarPartKeyTable;
            _customizeUI = customizeUI;
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

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (!PlayerPrefs.HasKey(PrefKeyCustomized))
                _customizeUI.OpenForFirstLaunch();
        }
    }
}
