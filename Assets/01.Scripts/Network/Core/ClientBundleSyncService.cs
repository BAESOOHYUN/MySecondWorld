using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MessagePipe;
using Mirror;
using MySecondWorld.Bundle;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 서버에서 BundleRequiredMessage가 오면:
    /// AddressableLabelLoader.LoadLabelAsync() 호출 → 번들 다운로드
    /// (zoneLoader라는 이름과 달리 존/아이템 등 모든 라벨을 공용으로 처리하는 로더다)
    /// 진행률은 LoadLabelAsync의 onProgress 콜백으로 AddressablesLoadingUI.UpdateProgress()를
    /// 직접 호출해 표시한다 — AddressablesProgressEvent(MessagePipe)는 진행률 전달용이 아니라
    /// 아래 재시도 정책의 NeedsRetry 신호 전용이다.
    /// 완료되면 BundleReadyMessage를 서버에 ACK
    ///
    /// 재시도 정책: 실패 시 최대 MaxAutoRetries회까지는 NeedsRetry 이벤트로 자동 재시도.
    /// 소진되면 Retry 버튼을 띄우고, 버튼 클릭도 동일하게 NeedsRetry를 발행해 카운터를
    /// 0으로 리셋 후 같은 경로를 다시 탄다 — "짧은 시간 내 일시적 실패"는 자동으로,
    /// "사용자가 뭔가 고친 뒤 다시" 는 버튼으로 담당을 나눈 것.
    /// </summary>
    public class ClientBundleSyncService
    {
        private const int MaxAutoRetries = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
        private int _retryCount;
        private string[] _lastFailedLabels;
        private IDisposable _retrySubscription;
        private readonly AddressableLabelLoader _zoneLoader;
        private readonly IPublisher<AddressablesProgressEvent> _progressPub;
        private IUIManager _uiManager;

        public ClientBundleSyncService (
            AddressableLabelLoader zoneLoader,
            ISubscriber<AddressablesProgressEvent> progressSub,
            IPublisher<AddressablesProgressEvent> progressPub,
            IUIManager uiManager
            )
        {
            _zoneLoader = zoneLoader;
            _progressPub = progressPub;
            _retrySubscription = progressSub.Subscribe(async evt =>
            {
                if (!evt.NeedsRetry || _lastFailedLabels == null || _lastFailedLabels.Length == 0)
                    return;

                await OnBundleRequired(new BundleRequiredMessage { labels = _lastFailedLabels }, CancellationToken.None);
            });
            _uiManager = uiManager;
        }

        // ServerZoneService.PlayerEnteredZone 에서 BundleRequiredMessage 받고
        public async UniTask OnBundleRequired(BundleRequiredMessage msg, CancellationToken ct)
        {
            if (NetworkServer.active) // Host면 스킵
                return;

            var uiObj = _uiManager.GetOrCreateDynamicCanvas<AddressablesLoadingUI>();
            uiObj?.SetActive(true);
            var addressableUI =uiObj?.GetComponent<AddressablesLoadingUI>();

            // 버튼 클릭 = 자동재시도와 동일한 경로(NeedsRetry 발행)를 타되, 카운터를 리셋해서
            // "그 사이에 사용자가 뭔가 고쳤을 수 있다(와이파이 재연결, 로컬 서버 재기동 등)"는
            // 전제로 다시 MaxAutoRetries회를 새로 준다.
            addressableUI?.SetRetryCallback(() =>
            {
                _retryCount = 0;
                _progressPub.Publish(new AddressablesProgressEvent { NeedsRetry = true });
            });
            addressableUI?.UpdateProgress(0f);

            // try-catch는 번들 다운로드 실패 시 재시도 기능을 위한 것
            try
            {
                // 라벨 여러 개를 순서대로 받되, 진행률은 전체 대비 비율로 합쳐서 표시
                for (int i = 0; i < msg.labels.Length; i++)
                {
                    int index = i;
                    // 진행률 콜백 전달 — 다운로드 도중 UI가 실시간으로 갱신됨
                    await _zoneLoader.LoadLabelAsync(msg.labels[i], ct, p => addressableUI?.UpdateProgress((index + p) / msg.labels.Length));
                }
                addressableUI?.UpdateProgress(1f);
                _retryCount = 0;
                NetworkClient.Send(new BundleReadyMessage { labels = msg.labels }); // > GameNetworkManager.OnStartServer
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bundle] 다운로드 실패({_retryCount + 1}/{MaxAutoRetries + 1}회차): {e.Message}");
                _lastFailedLabels = msg.labels;

                if (_retryCount < MaxAutoRetries)
                {
                    _retryCount++;
                    await UniTask.Delay(RetryDelay, cancellationToken: ct);
                    _progressPub.Publish(new AddressablesProgressEvent { NeedsRetry = true });
                }
                else
                {
                    // 자동재시도 소진 — 사용자 개입 필요, Retry 버튼 노출
                    addressableUI?.UpdateProgress(0f, isError: true);
                }
            }
        }
    }
}

//addressableUI?.SetRetryCallback 람다 등록
//
//OnBundleRequired 실행
//  └→ SetRetryCallback(람다 등록만 해둠)  ← 지금은 실행 안 됨
//  └→ 번들 다운로드 시도
//       └→ 실패
//            └→ RetryButton 표시

//나중에 사용자가 RetryButton 클릭
//  └→ OnRetryClicked
//       └→ _retryCallback.Invoke()  ← 이때 람다 실행
//            └→ OnBundleRequired 다시 호출  ← 재시도


//OnBundleRequired (try-catch)
//
//LoadLabelAsync 실패(CDN 연결 실패, 파일 없음 등)
//  └→ catch
//       └→ _lastFailedLabels 저장
//       └→ IsError = true 발행
//            └→ AddressablesLoadingUI에서 RetryButton 표시
//                 └→ 클릭 시 저장된 _lastFailedLabel로 재시도