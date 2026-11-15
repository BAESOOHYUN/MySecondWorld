using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.Combat.Player;

namespace MySecondWorld.UI
{
    // Attack 버튼 위 원형 게이지 이미지(Image Type=Filled)에 fillAmount를 밀어준다.
    // HP바가 PlayerHealthState.Local을 쓰는 것과 같은 패턴 — JoyStickCanvas가 UIManager 밑에서
    // 항상 먼저 떠 있고, 플레이어는 나중에(Mirror 스폰으로) 나타나므로 static Local로 연결한다.
    public class AimGaugeHUD : MonoBehaviour
    {
        [SerializeField] private Image _gaugeImage;

        private void Update()
        {
            if (_gaugeImage == null) return;

            var local = PlayerAttackController.Local;
            _gaugeImage.fillAmount = local != null ? local.AimGauge01 : 0f;
        }
    }
}