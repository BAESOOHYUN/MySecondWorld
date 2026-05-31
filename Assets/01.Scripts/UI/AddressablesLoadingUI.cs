using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using MessagePipe;
using MySecondWorld.Bundle;
using TMPro;

namespace MySecondWorld.UI
{
    public class AddressablesLoadingUI : MonoBehaviour
    {
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private Button _retryButton;

        private Action _retryCallback;

        private void Awake()
        {
            _retryButton.onClick.AddListener(OnRetryClicked);
            _retryButton.gameObject.SetActive(false);  // 기본 숨김
        }
        private void OnDestroy()
        {
            _retryButton.onClick.RemoveListener(OnRetryClicked);
        }

        public void UpdateProgress(float progress, bool isError = false)
        {
            gameObject.SetActive(progress < 1f);  // 스스로 켜고 끔
            _progressSlider.value = Mathf.Clamp01(progress);
            _progressText.text = $"에셋번들 다운로드 중...\n{Mathf.Min(100, (int)(progress * 100))}%";
            if (isError)
                _retryButton.gameObject.SetActive(true);// 에러일 때만 활성화
        }

        public void SetRetryCallback(Action callback)
        {
            _retryCallback = callback;
        }

        public void OnRetryClicked()
        {
            _retryButton.gameObject.SetActive(false);
            _retryCallback?.Invoke(); // ← 이때 ClientBundleSyncService.OnBundleRequired 에서 등록한 람다 실행
        }
    }
}

//번들 다운로드 실패
//  └→ ClientBundleSyncService: 실패한 레이블 저장 + IsError 발행
//RetryButton 클릭
//  └→ NeedsRetry 발행
//       └→ ClientBundleSyncService: 저장해둔 레이블로 LoadZoneAsync 재시도
