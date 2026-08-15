using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MySecondWorld.Bundle
{
    /// <summary>
    /// "라벨 번들 하나 로드/언로드" 담당
    /// </summary>
    public class AddressableLabelLoader
    {
        private Dictionary<string, AsyncOperationHandle> _handles = new();
        private Dictionary<string, int> _refCounts = new();

        public async UniTask LoadLabelAsync(string label, CancellationToken ct, Action<float> onProgress = null)
        {
            // label = "zone_dungeon_1"
            // label = "character_parts"

            // 참조 카운트 증가
            if (!_refCounts.ContainsKey(label))
                _refCounts[label] = 0;        // 처음 오는 경우만 0 초기화

            _refCounts[label]++;              // A 진입: 0→1, B 진입: 1→2

            // 이미 로드됐으면 끝 (B 진입 시 여기서 return)
            if (_handles.ContainsKey(label))
            {
                Debug.Log($"[Bundle] '{label}' 이미 로드됨 (refCount={_refCounts[label]})");
                return;
            }

            // Addressables 초기화 완료 대기
            await Addressables.InitializeAsync().ToUniTask();
           
            // 캐시 유무 확인
            var size = await Addressables.GetDownloadSizeAsync(label).ToUniTask(cancellationToken: ct);
            Debug.Log(size > 0
                ? $"[Bundle] '{label}' 캐시 없음 → 서버 다운로드 ({size} bytes)"
                : $"[Bundle] '{label}' 캐시 있음 → 즉시 로드");

            // 처음 로드할 때만 여기까지 옴 (플레이어A 진입 시)
            var newHandle = Addressables.DownloadDependenciesAsync(label, false); // false는 "자동 Release 안 함" 옵션
            _handles[label] = newHandle;                                          // handle 저장 (나중에 Release 때 필요)
            
            // 완료될 때까지 매 프레임 진행률 폴링 — AIModelDownloader와 동일 패턴
            while (!newHandle.IsDone)
            {
                onProgress?.Invoke(newHandle.PercentComplete);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (newHandle.Status == AsyncOperationStatus.Failed)
            {
                // 실패한 핸들을 캐시에 남겨두면 재시도 시 "이미 로드됨"으로 오판해 재다운로드를 건너뛴다 — 반드시 정리
                Addressables.Release(newHandle);
                _handles.Remove(label);
                _refCounts[label]--;
                throw newHandle.OperationException ?? new Exception($"'{label}' 다운로드 실패");
            }

            onProgress?.Invoke(1f);
            Debug.Log($"[Bundle] '{label}' 로드 완료");
        }

        public void ReleaseLabel(string label)
        {
            _refCounts[label]--;
            Debug.Log($"[Bundle] '{label}' refCount={_refCounts[label]}");

            if (_refCounts[label] <= 0)
            {
                Addressables.Release(_handles[label]);
                _handles.Remove(label);
                _refCounts.Remove(label);
                Debug.Log($"[Bundle] '{label}' 언로드 완료");
            }
        }
    }
}

// 에셋번들 로드 시나리오

//[플레이어A 진입] LoadLabelAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 0 → 1 (처음이라 0 초기화 후 증가)
//  └→ _handles에 없음 → 번들 다운로드 시작
//  └→ _handles["zone_dungeon_1"] = handle 저장
//  └→ await (다운로드 완료 대기)

//[플레이어B 진입] LoadLabelAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 1 → 2 (카운트 증가)
//  └→ _handles에 이미 있음 → return ← 다운로드 안 함![플레이어A 진입] LoadLabelAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 0 → 1 (처음이라 0 초기화 후 증가)
//  └→ _handles에 없음 → 번들 다운로드 시작
//  └→ _handles["zone_dungeon_1"] = handle 저장
//  └→ await (다운로드 완료 대기)

//[플레이어B 진입] LoadLabelAsync("zone_dungeon_1")
//  └→ _refCounts["zone_dungeon_1"] = 1 → 2 (카운트 증가)
//  └→ _handles에 이미 있음 → return ← 다운로드 안 함!
