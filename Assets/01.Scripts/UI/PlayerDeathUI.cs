using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Combat.Player;

namespace MySecondWorld.UI
{
    public class PlayerDeathUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _respawnButton;     // 제자리 부활
        [SerializeField] private Button _townRespawnButton; // 마을 부활

        private void Start()
        {
            _panel.SetActive(false);
            if (_respawnButton != null)
                _respawnButton.onClick.AddListener(OnImmediateRespawnClicked);
            if (_townRespawnButton != null)
                _townRespawnButton.onClick.AddListener(OnTownRespawnClicked);

            if (PlayerHealthState.Local != null)
                Subscribe(PlayerHealthState.Local);
            PlayerHealthState.LocalReady += Subscribe;
        }

        private void OnDestroy()
        {
            PlayerHealthState.LocalReady -= Subscribe;
            if (PlayerHealthState.Local != null)
                Unsubscribe(PlayerHealthState.Local);
            if (_respawnButton != null)
                _respawnButton.onClick.RemoveListener(OnImmediateRespawnClicked);
            if (_townRespawnButton != null)
                _townRespawnButton.onClick.RemoveListener(OnTownRespawnClicked);
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
            SetButtonsInteractable(true);
        }

        private void OnImmediateRespawnClicked()
        {
            SetButtonsInteractable(false);
            PlayerHealthState.Local?.CmdRequestRespawn(toTown: false); // 제자리 부활
        }

        private void OnTownRespawnClicked()
        {
            SetButtonsInteractable(false);
            PlayerHealthState.Local?.CmdRequestRespawn(toTown: true);  // 마을 부활
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_respawnButton != null) _respawnButton.interactable = interactable;
            if (_townRespawnButton != null) _townRespawnButton.interactable = interactable;
        }
    }
}