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

            if (size > 0)
            {
                float lastProgress = -1f;
                // 완료될 때까지 매 프레임 수신된 청크(DownloadedBytes)에 맞춰 진행률 폴링 — AIModelDownloader와 동일 패턴
                while (!newHandle.IsDone)
                {
                    var status = newHandle.GetDownloadStatus();
                    // status.TotalBytes가 0으로 집계되는 Addressables 버그 방지를 위해 사전 조회한 size를 분모로 사용
                    long totalBytes = size > 0 ? size : status.TotalBytes;
                    float currentProgress = totalBytes > 0
                        ? (float)status.DownloadedBytes / totalBytes
                        : status.Percent;

                    currentProgress = Mathf.Clamp01(Mathf.Max(currentProgress, status.Percent));
                    if (currentProgress > lastProgress)
                    {
                        lastProgress = currentProgress;
                        onProgress?.Invoke(lastProgress);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            else
            {
                // 이미 캐시되어 있는 경우 완료 대기
                await newHandle.ToUniTask(cancellationToken: ct);
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
            if (string.IsNullOrEmpty(label))
                return;

            if (!_refCounts.TryGetValue(label, out int currentCount))
            {
                Debug.LogWarning($"[Bundle] '{label}' ReleaseLabel 호출되었으나 참조 카운트가 없음  (중복 해제 의심, 무시)");
                return;
            }

            currentCount--;
            _refCounts[label] = currentCount;
            Debug.Log($"[Bundle] '{label}' refCount={_refCounts[label]}");

            if (currentCount <= 0)
            {
                if (_handles.TryGetValue(label, out var handle))
                    Addressables.Release(_handles[label]);
                // Dictionary.Remove(key)는 그 키가 없어도 예외 없이 그냥 false를 반환하는 안전한 연산
                _handles.Remove(label);
                _refCounts.Remove(label);
                Debug.Log($"[Bundle] '{label}' 언로드 완료");
            }
        }

        public void ReleaseAllZoneLabels(IEnumerable<string> labels)
        {
            if (labels == null) return;
            foreach (var label in labels)
            {
                if (string.IsNullOrEmpty(label)) continue;
                while (_refCounts.TryGetValue(label, out int count) && count > 0)
                {
                    ReleaseLabel(label);
                }
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
