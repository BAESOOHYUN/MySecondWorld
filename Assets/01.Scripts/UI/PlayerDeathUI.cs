using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Combat.Player;

namespace MySecondWorld.UI
{
    public class PlayerDeathUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _respawnButton;

        private void Start()
        {
            _panel.SetActive(false);
            _respawnButton.onClick.AddListener(OnRespawnClicked);

            if (PlayerHealthState.Local != null)
                Subscribe(PlayerHealthState.Local);
            PlayerHealthState.LocalReady += Subscribe;
        }

        private void OnDestroy()
        {
            PlayerHealthState.LocalReady -= Subscribe;
            if (PlayerHealthState.Local != null)
                Unsubscribe(PlayerHealthState.Local);
            _respawnButton.onClick.RemoveListener(OnRespawnClicked);
        }

        private void Subscribe(PlayerHealthState state)
        {
            state.OnLocalDied += ShowPanel;
            state.OnLocalRespawned += HidePanel;
        }

        private void Unsubscribe(PlayerHealthState state)
        {
            state.OnLocalDied -= ShowPanel;
            state.OnLocalRespawned -= HidePanel;
        }

        private void ShowPanel() => _panel.SetActive(true);

        private void HidePanel()
        {
            _panel.SetActive(false);
            _respawnButton.interactable = true;
        }

        private void OnRespawnClicked()
        {
            _respawnButton.interactable = false; // 중복클릭 방지
            PlayerHealthState.Local?.CmdRequestRespawn();
        }
    }
}