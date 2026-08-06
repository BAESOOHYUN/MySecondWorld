using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Combat.Companion
{
    // 월드스페이스 Canvas 자식으로 배치. UIManager 미경유(디제틱(게임 세계 안에 실제로 존재하는 것) UI).
    public class CompanionHealthBarUI : MonoBehaviour
    {
        [SerializeField] private CompanionServerState _serverState;
        [SerializeField] private Health _health; // MaxHP 조회용 — 로컬 값이 StatsSO 기반이라 모든 클라이언트 동일
        [SerializeField] private Image _fillImage;

        private void Start()
        {
            if (_serverState == null || _health == null || _fillImage == null)
            {
                Debug.LogError($"CompanionHealthBarUI 참조 누락 on {gameObject.name}", this);
                enabled = false;
                return;
            }

            UpdateBar(_serverState.CurrentHP);
        }

        private void OnEnable()
        {
            if (_serverState != null) _serverState.HPChanged += UpdateBar;
        }

        private void OnDisable()
        {
            if (_serverState != null) _serverState.HPChanged -= UpdateBar;
        }

        private void UpdateBar(float currentHP)
        {
            if (_health.MaxHP > 0f) _fillImage.fillAmount = currentHP / _health.MaxHP;
        }
    }
}
