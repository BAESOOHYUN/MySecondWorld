using UnityEngine;
using UnityEngine.UI;
using Mirror;
using MySecondWorld.Combat.Companion;
using MySecondWorld.Network.Player;
using MySecondWorld.Customization;

namespace MySecondWorld.UI
{
    public class CompanionHUDController : MonoBehaviour
    {
        [SerializeField] private string _recruitCompanionId = "Ari"; // 컴패니언 선택 UI 생기기 전까지 고정값

        [SerializeField] private RawImage _portraitImage;
        [SerializeField] private int _portraitSize = 256;
        [SerializeField] private float _faceHeight = 1.6f;
        [SerializeField] private float _distance = 0.6f;

        private RenderTexture _portraitRT;
        private MySecondWorld.Customization.CharacterCustomizerBase _companionCustomizer;

        private void Awake()
        {
            _portraitRT = new RenderTexture(_portraitSize, _portraitSize, 16);
            if (_portraitImage != null) _portraitImage.texture = _portraitRT;
        }

        private void OnDestroy()
        {
            if (_portraitRT != null) _portraitRT.Release();
        }

        private void OnEnable()
        {
            CompanionServerState.LocalCompanionReady += HandleCompanionReady;
            CompanionServerState.LocalCompanionLost += HandleCompanionLost;

            if (CompanionServerState.LocalCompanion != null)
                HandleCompanionReady(CompanionServerState.LocalCompanion);
        }

        private void OnDisable()
        {
            CompanionServerState.LocalCompanionReady -= HandleCompanionReady;
            CompanionServerState.LocalCompanionLost -= HandleCompanionLost;
        }
        private void HandleCompanionReady(CompanionServerState state)
        {
            if (state.TryGetComponent(out _companionCustomizer))
                _companionCustomizer.AppearanceApplied += HandleAppearanceApplied;

            // 혹시 이미 조립이 끝난 뒤라면(구독이 늦은 경우) 지금 바로 한 번 시도
            PortraitCapture.Instance?.Capture(state.Anchor, _portraitRT, _faceHeight, _distance);
        }

        private void HandleAppearanceApplied()
        {
            var companion = CompanionServerState.LocalCompanion;
            if (companion != null)
                PortraitCapture.Instance?.Capture(companion.Anchor, _portraitRT, _faceHeight, _distance);
        }

        private void HandleCompanionLost()
        {
            if (_companionCustomizer != null)
            {
                _companionCustomizer.AppearanceApplied -= HandleAppearanceApplied;
                _companionCustomizer = null;
            }

            if (_portraitRT == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = _portraitRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }

        #region 버튼 진입점
       
        // 따라다니기
        public void OnToggleFollowClicked() => WithLocalPlayer(c => c.RequestToggleFollow());

        // 초상화 버튼 하나 = 소환/소환해제 토글. 생사와 무관 — 보이면 감추고, 숨겨져 있으면 내 위치로 다시 보여준다.
        public void OnSummonToggleClicked()
        {
            var companion = CompanionServerState.LocalCompanion;

            if (companion == null)
                WithLocalPlayer(c => c.RequestRecruit(_recruitCompanionId));
            else if (companion.IsPresent)
                WithLocalPlayer(c => c.RequestDismiss());
            else
                WithLocalPlayer(c => c.RequestSummon());
        }

        private void WithLocalPlayer(System.Action<NetworkCompanionCommands> action)
        {
            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null)
            {
                Debug.LogWarning("[Companion] 로컬 플레이어가 아직 스폰되지 않았습니다");
                return;
            }
            if (!localPlayer.TryGetComponent(out NetworkCompanionCommands cmds))
            {
                Debug.LogWarning("[Companion] 플레이어 프리팹에 NetworkCompanionCommands가 없습니다", localPlayer);
                return;
            }
            action(cmds);
        }
        #endregion
    }
}
