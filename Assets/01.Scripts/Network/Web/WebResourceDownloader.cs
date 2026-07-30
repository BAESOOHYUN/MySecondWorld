using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.Network.Web
{
    public class WebResourceDownloader
    {
        // in-flight 중복 차단 — 같은 URL을 동시에 여러 곳에서 요청해도 HTTP는 1번만
        private readonly Dictionary<string, UniTaskCompletionSource<byte[]>> _inFlight = new();
        public async UniTask<byte[]> DownloadAsync(
            string url, IProgress<float> progress = null, CancellationToken ct = default)
        {
            // 같은 URL이 이미 다운로드 중이면 진행중이던 Task를 함께 await
            // 예) A가 먼저 tcs-A 진행중, B도 같은 url넣고 tcs-A를 기다리다가, 완료시 동시에 결과 받음.
            if (_inFlight.TryGetValue(url, out var existing))
                return await existing.Task.AttachExternalCancellation(ct);

            // UniTaskCompletionSource : "결과를 나중에 직접 넣어줄 수 있는" Task 박스
            var tcs = new UniTaskCompletionSource<byte[]>();
            _inFlight[url] = tcs; // 박스를 딕셔너리에 올려두고

            try
            {
                // ...HTTP 다운로드...
                var bytes = await DownloadWithRetryAsync(url, progress, ct);
                tcs.TrySetResult(bytes); // 박스에 결과를 넣으면 성공 → 결과 전달
                return bytes; // await tcs.Task 하던 모든 호출자가 동시에 bytes를 받음
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex); // 실패 → 예외 전달
                throw;
            }
            finally
            {
                _inFlight.Remove(url);
            }
        }

        private async UniTask<byte[]> DownloadWithRetryAsync(
            string url, IProgress<float> progress, CancellationToken ct)
        {
            const int maxRetry = 3;
            var delay = TimeSpan.FromSeconds(1);

            // 지수 백오프 재시도 — 실패 시 1s → 2s → 4s 간격으로 3회 재시도
            for (int i = 0; i < maxRetry; i++)
            {
                try
                {
                    // HTTP 요청 시도
                    using var req = UnityWebRequest.Get(url);
                    var operation = req.SendWebRequest();

                    while (!operation.isDone)
                    {
                        progress?.Report(req.downloadProgress);
                        await UniTask.Yield(ct);
                    }

                    if (req.result != UnityWebRequest.Result.Success)
                        throw new Exception(req.error);

                    return req.downloadHandler.data;
                }
                catch (OperationCanceledException)
                {
                    throw; // CT 취소는 재시도 없이 바로 전파
                }
                catch when (i < maxRetry - 1) // i가 0이나 1일 때만
                {
                    await UniTask.Delay(delay, cancellationToken: ct);
                    delay *= 2;
                }
                // i가 2면 catch 안 잡힘 → 예외 위로 전파 (호출한 쪽으로 계속 거슬러 올라감)
            }
            throw new Exception($"[WebResourceDownloader] {maxRetry}회 재시도 후 실패: {url}");
        }
    }
}