using UnityEngine;
using MySecondWorld.Combat.Player;

namespace MySecondWorld.UI
{
    /// <summary>내 플레이어가 스폰되면 화면 고정 HP바에 물려준다.</summary>
    [RequireComponent(typeof(HealthBarUI))]
    public class LocalPlayerHealthBinder : MonoBehaviour
    {
        private HealthBarUI _bar;

        private void Awake() => _bar = GetComponent<HealthBarUI>();

        private void OnEnable()
        {
            // HUD가 먼저 살아 있을 수도, 플레이어가 이미 스폰돼 있을 수도 있다. 양쪽 다 처리한다.
            if (PlayerHealthState.Local != null) _bar.Bind(PlayerHealthState.Local);
            PlayerHealthState.LocalReady += Bind;
        }

        private void OnDisable()
        {
            PlayerHealthState.LocalReady -= Bind;
            _bar.Unbind();
        }

        private void Bind(PlayerHealthState state) => _bar.Bind(state);
    }
}