using UnityEngine;
using MySecondWorld.Combat.Companion;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 내 컴패니언이 있을 때만 HP바를 보여준다.
    /// 컴패니언은 소환·해제되므로, 없을 때 빈 바가 남아 있으면 안 된다.
    /// </summary>
    [RequireComponent(typeof(HealthBarUI))]
    public class LocalCompanionHealthBinder : MonoBehaviour
    {
        private HealthBarUI _bar;
        private CanvasGroup _group;
        private CompanionServerState _state;

        private void Awake()
        {
            _bar = GetComponent<HealthBarUI>();

            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();

            // HP바가 탭을 삼키면 안 된다.
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void OnEnable()
        {
            CompanionServerState.LocalCompanionReady += Attach;
            CompanionServerState.LocalCompanionLost += Detach;

            // HUD가 먼저일 수도, 컴패니언이 이미 있을 수도 있다.
            if (CompanionServerState.LocalCompanion != null) Attach(CompanionServerState.LocalCompanion);
            else Refresh();
        }

        private void OnDisable()
        {
            CompanionServerState.LocalCompanionReady -= Attach;
            CompanionServerState.LocalCompanionLost -= Detach;
            Detach();
        }

        private void Attach(CompanionServerState state)
        {
            Detach();

            _state = state;
            _state.AliveChanged += HandleAliveChanged;
            _bar.Bind(state);
            Refresh();
        }

        private void Detach()
        {
            if (_state != null)
            {
                _state.AliveChanged -= HandleAliveChanged;
                _state = null;
            }

            _bar.Unbind();
            Refresh();
        }

        private void HandleAliveChanged(bool isAlive) => Refresh();

        // SetActive로 숨기면 이 컴포넌트도 같이 꺼져서 다시 켤 방법이 없어진다.
        // ScreenFollowUI와 같은 이유로 알파를 쓴다.
        private void Refresh() => _group.alpha = (_state != null && _state.IsAlive) ? 1f : 0f;
    }
}