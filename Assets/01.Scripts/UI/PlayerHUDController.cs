using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Combat.Player;

namespace MySecondWorld.UI
{
    // CombatHUDCanvas/PlayerHUD/PlayerInfo에 붙는다. 로컬 플레이어 스폰 시 얼굴을 한 번 캡처한다.
    public class PlayerHUDController : MonoBehaviour
    {
        [SerializeField] private RawImage _portraitImage;
        [SerializeField] private int _portraitSize = 256;
        [SerializeField] private float _faceHeight = 1.6f;
        [SerializeField] private float _distance = 0.6f;

        private RenderTexture _portraitRT;

        private void Awake()
        {
            _portraitRT = new RenderTexture(_portraitSize, _portraitSize, 16);
            if (_portraitImage != null) _portraitImage.texture = _portraitRT;
        }

        private void OnEnable()
        {
            MySecondWorld.Network.NetworkCharacterAppearance.LocalAppearanceReady += HandleAppearanceReady;
        }

        private void OnDisable()
        {
            MySecondWorld.Network.NetworkCharacterAppearance.LocalAppearanceReady -= HandleAppearanceReady;
        }

        private void OnDestroy()
        {
            if (_portraitRT != null) _portraitRT.Release();
        }

        private void HandleAppearanceReady()
        {
            var local = PlayerHealthState.Local;
            if (local != null)
                PortraitCapture.Instance?.Capture(local.Anchor, _portraitRT, _faceHeight, _distance);
        }
    }
}