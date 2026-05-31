using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MySecondWorld.Bundle
{
    /// <summary>
    /// "Zone 번들 하나 로드/언로드" 담당
    /// </summary>
    public class ZoneAddressableLoader
    {
        private Dictionary<string, AsyncOperationHandle> _handles = new();
        private Dictionary<string, int> _refCounts = new();

        public async UniTask LoadZoneAsync(string label, CancellationToken ct)
        {
            // label = "zone_dungeon_1"

            // 참조 카운트 증가
            if (!_refCounts.ContainsKey(label))
                _refCounts[label] = 0;        // 처음 오는 경우만 0 초기화

            _refCounts[label]++;              // A 진입: 0→1, B 진입: 1→2

            // 이미 로드됐으면 끝 (B 진입 시 여기서 return)
            if (_handles.ContainsKey(label))  
                return;

            // Addressables 초기화 완료 대기
            await Addressables.InitializeAsync().ToUniTask();

            // 처음 로드할 때만 여기까지 옴 (플레이어A 진입 시)
            var newHandle = Addressables.DownloadDependenciesAsync(label, false); // false는 "자동 Release 안 함" 옵션
            _handles[label] = newHandle;                                          // handle 저장 (나중에 Release 때 필요)
            await newHandle.ToUniTask(cancellationToken: ct);                     // 다운로드 완료 대기
        }

        public void ReleaseZone(string label)
        {
            _refCounts[label]--;

            if (_refCounts[label] <= 0)
            {
                Addressables.Release(_handles[label]);
                _handles.Remove(label);
                _refCounts.Remove(label);
            }
        }
    }
}

// 에셋번들 로드 시나리오

//[플레이어A 진입] LoadZoneAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 0 → 1 (처음이라 0 초기화 후 증가)
//  └→ _handles에 없음 → 번들 다운로드 시작
//  └→ _handles["zone_dungeon_1"] = handle 저장
//  └→ await (다운로드 완료 대기)

//[플레이어B 진입] LoadZoneAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 1 → 2 (카운트 증가)
//  └→ _handles에 이미 있음 → return ← 다운로드 안 함![플레이어A 진입] LoadZoneAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 0 → 1 (처음이라 0 초기화 후 증가)
//  └→ _handles에 없음 → 번들 다운로드 시작
//  └→ _handles["zone_dungeon_1"] = handle 저장
//  └→ await (다운로드 완료 대기)

//[플레이어B 진입] LoadZoneAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 1 → 2 (카운트 증가)
//  └→ _handles에 이미 있음 → return ← 다운로드 안 함!
