using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.Combat.Player;
using MySecondWorld.Network;

namespace MySecondWorld.UI
{
    // CombatHUDCanvas/PlayerHUD/PlayerInfo에 붙는다. 로컬 플레이어 스폰 시 얼굴을 한 번 캡처한다.
    public class PlayerHUDController : MonoBehaviour
    {
        [SerializeField] private RawImage _portraitImage;
        [SerializeField] private TextMeshProUGUI _usernameText;
        [SerializeField] private int _portraitSize = 256;
        [SerializeField] private float _faceHeight = 1.6f;
        [SerializeField] private float _distance = 0.6f;

        private RenderTexture _portraitRT;

        private void Awake()
        {
            _portraitRT = new RenderTexture(_portraitSize, _portraitSize, 16);
            if (_portraitImage != null) _portraitImage.texture = _portraitRT;
            if (_usernameText == null)
            {
                var userObj = transform.Find("UserName");
                if (userObj != null) _usernameText = userObj.GetComponent<TextMeshProUGUI>();
            }
        }

        private void Start()
        {
            UpdateUsername();
        }

        private void OnEnable()
        {
            MySecondWorld.Network.NetworkCharacterAppearance.LocalAppearanceReady += HandleAppearanceReady;
            UpdateUsername();
        }

        private void OnDisable()
        {
            MySecondWorld.Network.NetworkCharacterAppearance.LocalAppearanceReady -= HandleAppearanceReady;
        }

        private void OnDestroy()
        {
            if (_portraitRT != null) _portraitRT.Release();
        }

        public void UpdateUsername()
        {
            if (_usernameText == null) return;

            string username = PlayerSession.LocalPlayerName;
            if (string.IsNullOrWhiteSpace(username))
            {
                username = PlayerPrefs.GetString("playerName", "Guest");
            }

            _usernameText.text = username;
        }

        private void HandleAppearanceReady()
        {
            UpdateUsername();
            CaptureNextFrameAsync().Forget();
        }

        // 벗은 파츠는 Destroy(=Addressables.ReleaseInstance)가 프레임 끝에야 실제로 사라진다.
        // 같은 프레임에 찍으면 "벗기 직전 모습"이 찍히므로 한 프레임 뒤에 촬영한다.
        private async UniTaskVoid CaptureNextFrameAsync()
        {
            await UniTask.NextFrame(destroyCancellationToken);

            var local = PlayerHealthState.Local;
            if (local != null)
                PortraitCapture.Instance?.Capture(local.Anchor, _portraitRT, _faceHeight, _distance);
        }
    }
}