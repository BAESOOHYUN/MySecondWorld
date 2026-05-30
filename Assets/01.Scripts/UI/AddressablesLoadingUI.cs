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

        [Inject] private ISubscriber<AddressablesProgressEvent> _subscriber;
        [Inject] private IPublisher<AddressablesProgressEvent> _publisher;

        private IDisposable _subscription;


        private void OnEnable()
        {
            _subscription = _subscriber.Subscribe(evt =>
            {
                _progressSlider.value = Mathf.Clamp01(evt.Progress);
                _progressText.text = $"에셋번들 다운로드 중...\n{Mathf.Min(100, (int)(evt.Progress * 100))}%";

                if (evt.IsError)
                    _retryButton.gameObject.SetActive(true); // 에러일 때만 활성화, 비활성화는 OnRetryClicked에서
            });

            _retryButton.onClick.AddListener(OnRetryClicked);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _retryButton.onClick.RemoveListener(OnRetryClicked);
        }

        public void OnRetryClicked()
        {
            _publisher.Publish(new AddressablesProgressEvent { NeedsRetry = true });
        }
    }
}

//번들 다운로드 실패
//  └→ ClientBundleSyncService: 실패한 레이블 저장 + IsError 발행
//RetryButton 클릭
//  └→ NeedsRetry 발행
//       └→ ClientBundleSyncService: 저장해둔 레이블로 LoadZoneAsync 재시도
