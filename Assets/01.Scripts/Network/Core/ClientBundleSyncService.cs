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
    /// ZoneAddressableLoader.LoadZoneAsync() 호출 → 번들 다운로드
    /// 진행률 AddressablesProgressEvent로 발행 → UI가 받아서 표시
    /// 완료되면 BundleReadyMessage를 서버에 ACK
    /// </summary>
    public class ClientBundleSyncService
    {
        private string _lastFailedLabel;
        private IDisposable _retrySubscription;
        private readonly ZoneAddressableLoader _zoneLoader;
        private IUIManager _uiManager;

        public ClientBundleSyncService (
            ZoneAddressableLoader zoneLoader,
            ISubscriber<AddressablesProgressEvent> progressSub,
            IUIManager uiManager)
        {
            _zoneLoader = zoneLoader;
            _retrySubscription = progressSub.Subscribe(async evt =>
            {
                if (!evt.NeedsRetry || string.IsNullOrEmpty(_lastFailedLabel))
                    return;

                await OnBundleRequired(new BundleRequiredMessage { zoneLabel = _lastFailedLabel }, CancellationToken.None);
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

            //재귀처럼 보이지만 아님. SetRetryCallback은 콜백을 지금 실행하는 게 아니라 등록하는 것
            addressableUI?.SetRetryCallback(() =>
                                OnBundleRequired(new BundleRequiredMessage { zoneLabel = _lastFailedLabel}, default).Forget());
            
            addressableUI?.UpdateProgress(0f);
            
            // try-catch는 번들 다운로드 실패 시 재시도 기능을 위한 것
            try
            {
                await _zoneLoader.LoadZoneAsync(msg.zoneLabel, ct); //번들 다운로드
                addressableUI?.UpdateProgress(1f);
                NetworkClient.Send(new BundleReadyMessage { zoneLabel = msg.zoneLabel }); // > GameNetworkManager.OnStartServer
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bundle] 다운로드 실패: {e.Message}");
                _lastFailedLabel = msg.zoneLabel;
                addressableUI?.UpdateProgress(0f, isError:true);
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
//LoadZoneAsync 실패(CDN 연결 실패, 파일 없음 등)
//  └→ catch
//       └→ _lastFailedLabel 저장
//       └→ IsError = true 발행
//            └→ AddressablesLoadingUI에서 RetryButton 표시
//                 └→ 클릭 시 저장된 _lastFailedLabel로 재시도