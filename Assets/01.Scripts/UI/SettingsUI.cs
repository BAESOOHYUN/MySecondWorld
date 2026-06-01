using Cysharp.Threading.Tasks;
using MySecondWorld.Bundle;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using VContainer;

namespace MySecondWorld.UI
{
    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private Button _clearCacheButton;
        [SerializeField] private TextMeshProUGUI _feedbackText;

        [Inject] private AddressablesService _addressablesService;

        private void Start()
        {
            _clearCacheButton.onClick.AddListener(OnClearCacheClicked);
        }

        private void OnEnable()
        {
            _feedbackText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _clearCacheButton.onClick.RemoveListener(OnClearCacheClicked);
        }

        private void OnClearCacheClicked()
        {
            CleanCacheAsync().Forget();
        }

        private async UniTaskVoid CleanCacheAsync()
        {
#if UNITY_EDITOR
            _feedbackText.text = "에디터 미지원. 폰에서 테스트하세요.";
            _feedbackText.gameObject.SetActive(true);
            return;
#endif
            bool result =
                await _addressablesService.CleanupUnusedCacheAsync(destroyCancellationToken);
             _feedbackText.text = result ? "캐시 정리 완료." : "정리 실패.";
            _feedbackText.gameObject.SetActive(true);
        }
    }
}