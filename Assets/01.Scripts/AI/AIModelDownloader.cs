using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Application;
using MessagePipe;
using MySecondWorld.AI;

// UnityWebRequest 대신 HttpClient를 쓰는 이유:
// UnityWebRequest는 Range 헤더(이어받기)를 공식 지원하지 않고,
// DownloadHandlerFile이 파일 이어쓰기(Append)를 지원하지 않는다.
// HttpClient는 Range 헤더 + FileStream Append 조합으로 이어받기가 가능하다.
public class AIModelDownloader
{
    private readonly IPublisher<AIModelDownloadProgressEvent> _publisher;
    private readonly AIModelConfigSO _config;
    private readonly IDeviceCapability _device;

    private string _selectedFileName;
    public string SelectedFileName => _selectedFileName;

    public AIModelDownloader(
        IPublisher<AIModelDownloadProgressEvent> publisher,
        AIModelConfigSO config,
        IDeviceCapability device)
    {
        _publisher = publisher;
        _config = config;
        _device = device;
    }

    public async UniTask DownloadAsync(CancellationToken ct)
    {
        // 기기 RAM 확인 → 4B/1B 모델 선택
        _device.Evaluate();

        if (!_device.IsCapable)
        {
            Debug.LogWarning($"[AIModelDownloader] 로컬 추론 불가 — {_device.Reason}");
            _publisher.Publish(new AIModelDownloadProgressEvent(0f, false));
            return;
        }

        // RAM에 따라 대형(4B) 또는 소형(1B) 모델 선택
        bool useLarge   = _device.RecommendedModelSize == ModelSize.Large;
        string url      = useLarge ? _config.modelUrl      : _config.smallModelUrl;
        string fileName = useLarge ? _config.modelFileName : _config.smallModelFileName;
        _selectedFileName = fileName;
        Debug.Log($"[AIModelDownloader] 시작 — {_device.Reason} → {fileName}");

        string savePath = Path.Combine(Application.persistentDataPath, fileName);

        await UniTask.SwitchToThreadPool();

        // HEAD 요청으로 실제 파일 크기 조회 — SO에 하드코딩 불필요, 모델 교체 시 URL만 바꾸면 됨
        long totalBytes = 0;
        try
        {
            using var headClient = new HttpClient();
            if (!string.IsNullOrEmpty(_config.huggingFaceToken))
                headClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config.huggingFaceToken);
            using var headResponse = await headClient
                .SendAsync(new HttpRequestMessage(HttpMethod.Head, url), ct)
                .ConfigureAwait(false);
            totalBytes = headResponse.Content.Headers.ContentLength ?? 0;
            Debug.Log($"[AIModelDownloader] 파일 크기: {totalBytes / 1024 / 1024}MB");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIModelDownloader] 파일 크기 조회 실패 — 진행률 표시 불가: {e.Message}");
        }

        // 파일이 이미 완전히 받아져 있으면 스킵
        long existingBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;
        if (existingBytes > 0 && (totalBytes <= 0 || existingBytes >= totalBytes * 0.99f))
        {
            await UniTask.SwitchToMainThread();
            Debug.Log($"[AIModelDownloader] 이미 존재 — 스킵: {savePath}");
            _publisher.Publish(new AIModelDownloadProgressEvent(1f, true));
            return;
        }
        if (existingBytes > 0)
        {
            Debug.Log($"[AIModelDownloader] 이어받기 — {existingBytes / 1024 / 1024}MB 부터");
            float resumeProgress = totalBytes > 0 ? (float)existingBytes / totalBytes : 0f;
            await UniTask.SwitchToMainThread();
            _publisher.Publish(new AIModelDownloadProgressEvent(resumeProgress, false));
            await UniTask.SwitchToThreadPool();
        }

        const int maxRetries = 10;
        int retry = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            long currentBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(30);

            if (!string.IsNullOrEmpty(_config.huggingFaceToken))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config.huggingFaceToken);

            if (currentBytes > 0)
                client.DefaultRequestHeaders.Range = new RangeHeaderValue(currentBytes, null);

            bool completed = false;
            try
            {
                using var response = await client
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                FileStream fileStream = null;
                try
                {
                    fileStream = new FileStream(
                        savePath,
                        currentBytes > 0 ? FileMode.Append : FileMode.Create,
                        FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

                    byte[] buffer = new byte[81920];
                    long   downloaded = currentBytes;
                    int    bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        downloaded += bytesRead;

                        float progress = totalBytes > 0 ? (float)downloaded / totalBytes : 0f;

                        await UniTask.SwitchToMainThread();
                        _publisher.Publish(new AIModelDownloadProgressEvent(progress, false));
                        await UniTask.SwitchToThreadPool();
                    }

                    completed = true;
                }
                finally
                {
                    fileStream?.Close();
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 사용자가 명시적으로 취소한 경우는 재시도 없이 바로 던짐
            }
            catch (Exception e) when (retry < maxRetries)
            {
                retry++;
                long resumeBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;
                float resumeProgress = totalBytes > 0 ? (float)resumeBytes / totalBytes : 0f;
                Debug.LogWarning($"[AIModelDownloader] 연결 끊김 ({retry}/{maxRetries}) — 3초 후 재시도: {e.Message}");

                await UniTask.SwitchToMainThread();
                _publisher.Publish(new AIModelDownloadProgressEvent(resumeProgress, false));
                await UniTask.Delay(3000, cancellationToken: ct);
                await UniTask.SwitchToThreadPool();
                continue;
            }

            if (completed) break;
        }

        await UniTask.SwitchToMainThread();
        Debug.Log($"[AIModelDownloader] 다운로드 완료: {savePath}");
        _publisher.Publish(new AIModelDownloadProgressEvent(1f, true));
    }
}
