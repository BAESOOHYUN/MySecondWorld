using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
    public class ClientBundleSyncService : MonoBehaviour
    {
        private string _lastFailedLabel;
        private IDisposable _retrySubscription;
        private readonly ZoneAddressableLoader _zoneLoader;
        private readonly IPublisher<AddressablesProgressEvent> _progressPub;

        public ClientBundleSyncService (
            ZoneAddressableLoader zoneLoader,
            IPublisher<AddressablesProgressEvent> progressPub,
            ISubscriber<AddressablesProgressEvent> progressSub)
        {
            _zoneLoader = zoneLoader;
            _progressPub = progressPub;
            _retrySubscription = progressSub.Subscribe(async evt =>
            {
                if (!evt.NeedsRetry || string.IsNullOrEmpty(_lastFailedLabel))
                    return;

                await OnBundleRequired(new BundleRequiredMessage { zoneLabel = _lastFailedLabel }, CancellationToken.None);
            });
        }

        // ServerZoneService.PlayerEnteredZone 에서 BundleRequiredMessage 받고
        public async UniTask OnBundleRequired(BundleRequiredMessage msg, CancellationToken ct)
        {
            if (NetworkServer.active) // Host면 스킵
                return;

            _progressPub.Publish(new AddressablesProgressEvent { Progress = 0f });
            var label = new AssetLabelReference();

            try
            {
                await _zoneLoader.LoadZoneAsync(msg.zoneLabel, ct); //번들 다운로드
                _progressPub.Publish(new AddressablesProgressEvent { Progress = 1f });
                NetworkClient.Send(new BundleReadyMessage { zoneLabel = msg.zoneLabel }); // > GameNetworkManager.OnStartServer
            }
            catch
            {
                _lastFailedLabel = msg.zoneLabel;
                _progressPub.Publish(new AddressablesProgressEvent { IsError = true });
            }
        }
    }
}