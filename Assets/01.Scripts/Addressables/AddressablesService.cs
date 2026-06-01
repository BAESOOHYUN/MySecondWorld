using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using MessagePipe;
using MySecondWorld.Network;

namespace MySecondWorld.Bundle
{
    /// <summary>
    /// Addressables 시스템 전체를 관리하는 서비스
    /// 인증 완료 → AuthStateChangedEvent(true) 발행
    ///   └→ AddressablesService가 이 이벤트 구독
    ///        └→ ValidateCatalogHashAsync() 자동 호출
    ///             └→ CheckForCatalogUpdates() → 서버 버전과 비교 → 필요시 업데이트
    /// </summary>
    public class AddressablesService : IDisposable
    {
        private IDisposable _authDisposable;
        private IDisposable _progressDisposable;

        public AddressablesService(
            ISubscriber<AuthStateChangedEvent> authSub,
            ISubscriber<AddressablesProgressEvent> progressSub)
        {
            // 인증 완료 시 catalog hash 자동 체크
            _authDisposable = authSub.Subscribe(async e =>
            {
                if (!e.IsAuthenticated) 
                    return;
                await ValidateCatalogHashAsync(CancellationToken.None);

            });
            _progressDisposable = progressSub.Subscribe(async e =>
            {
                if(!e.NeedsRetry) 
                    return;
                await ValidateCatalogHashAsync(CancellationToken.None);
            });
        }

        public void Dispose()
        {
            _authDisposable?.Dispose();
            _progressDisposable?.Dispose();
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            await Addressables.InitializeAsync().ToUniTask(cancellationToken: ct);
        }

        public async UniTask<bool> ValidateCatalogHashAsync(CancellationToken ct)
        {
            // 업데이트 필요한 목록을 반환하는 메서드
            var catalogs = await Addressables.CheckForCatalogUpdates(false).ToUniTask(cancellationToken: ct);

            // 목록이 비어있으면 업데이트 없음
            if (catalogs.Count == 0)
                return false;

            // 목록이 있으면 → UpdateCatalogsAsync(목록) 호출
            await Addressables.UpdateCatalogs(catalogs).ToUniTask(cancellationToken: ct);

            return true;
        }

        public UniTask<bool> CleanupUnusedCacheAsync(CancellationToken ct = default)
        {
            // 앱 활성 상태에서는 즉시 삭제 불가 → 다음 시작 시 처리 예약
            PlayerPrefs.SetInt("ClearCacheOnStart", 1);
            PlayerPrefs.Save();
            Debug.Log("[Addressables] 캐시 삭제 예약됨 (앱 재시작 시 적용)");
            return UniTask.FromResult(true);
        }
    }
}