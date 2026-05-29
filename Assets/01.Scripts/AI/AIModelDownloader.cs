using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using MessagePipe;
using MySecondWorld.AI;

// Range 헤더는 SetRequestHeader로 수동 설정, 이어받기는 DownloadHandlerFile(append: true)로 지원.
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

    public async UniTask DownloadLLMAsync(CancellationToken ct)
    {
        if (_device.UseServerMode)
        {
            Debug.Log("[AIModelDownloader] 서버 모드 — LLM 다운로드 스킵");
            _publisher.Publish(new AIModelDownloadProgressEvent(1f, true, "LLM"));
        }

        // 기기 RAM 확인 → 4B/1B 모델 선택
        _device.Evaluate();

        if (!_device.IsCapable)
        {
            Debug.LogWarning($"[AIModelDownloader] 로컬 추론 불가 — {_device.Reason}");
            _publisher.Publish(new AIModelDownloadProgressEvent(0f, false, "LLM"));
            return;
        }

        bool useLarge = _device.RecommendedModelSize == ModelSize.Large;
        string url = useLarge ? _config.modelUrl : _config.smallModelUrl;
        string fileName = useLarge ? _config.modelFileName : _config.smallModelFileName;
        _selectedFileName = fileName;

        Debug.Log($"[AIModelDownloader] 시작 — {_device.Reason} → {fileName}");
        await DownloadFileAsync(url, fileName, "LLM", ct);
    }

    public async UniTask DownloadSTTAsync(CancellationToken ct)
    {
        if (_device.UseServerMode)
        {
            Debug.Log("[AIModelDownloader] 서버 모드 — STT 다운로드 스킵");
            _publisher.Publish(new AIModelDownloadProgressEvent(1f, true, "STT"));
        }

        await DownloadFileAsync(_config.sttModelUrl, _config.sttModelFileName, "STT", ct);
    }

    private async UniTask DownloadFileAsync(string url, string fileName, string label, CancellationToken ct)
    {
        string savePath = Path.Combine(Application.persistentDataPath, fileName);

        // HEAD 요청으로 실제 파일 크기 조회
        long totalBytes = await FetchTotalBytesAsync(url, label, ct);
        if (totalBytes > 0)
            Debug.Log($"[AIModelDownloader] {label} 파일 크기: {totalBytes / 1024 / 1024}MB");

        // 파일이 이미 완전히 받아져 있으면 스킵
        long existingBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;
        if (existingBytes > 0)
        {
            if (totalBytes <= 0)
            {
                // HEAD 요청 실패 → 크기 검증 불가, 재다운로드
                Debug.LogWarning($"[AIModelDownloader] {label} 파일 크기 확인 실패 — 기존 파일({existingBytes / 1024 / 1024}MB) 무결성 검증 불가, 재다운로드");
            }
            else if (existingBytes >= totalBytes * 0.99f)
            {
                // 99% 이상 → 완전한 파일로 간주
                Debug.Log($"[AIModelDownloader] {label} 이미 존재 ({existingBytes / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB) — 스킵: {savePath}");
                _publisher.Publish(new AIModelDownloadProgressEvent(1f, true, label));
                return;
            }
            else
            {
                // 크기 불일치 → 불완전한 파일, 이어받기
                Debug.LogWarning($"[AIModelDownloader] {label} 불완전한 파일 감지 ({existingBytes / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB) — 이어받기");
            }
        }
        // 이어받기 구간 계산
        if (existingBytes > 0)
        {
            Debug.Log($"[AIModelDownloader] {label} 이어받기 — {existingBytes / 1024 / 1024}MB 부터");
            float resumeProgress = totalBytes > 0 ? (float)existingBytes / totalBytes : 0f;
            _publisher.Publish(new AIModelDownloadProgressEvent(resumeProgress, false, label));
        }

        const int maxRetries = 10;
        int retry = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested(); // ← 취소 받으면 여기서 예외 터짐. 다운로드 시작 전 확인

            long currentBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;

            try
            {
                await TryDownloadChunkAsync(url, savePath, currentBytes, totalBytes, label, ct);
                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e) when (retry < maxRetries)
            {
                retry++;
                long resumeBytes = File.Exists(savePath) ? new FileInfo(savePath).Length : 0;
                float resumeProgress = totalBytes > 0 ? (float)resumeBytes / totalBytes : 0f;
                Debug.LogWarning($"[AIModelDownloader] {label} 연결 끊김 ({retry}/{maxRetries}) — 3초 후 재시도: {e.Message}");
                _publisher.Publish(new AIModelDownloadProgressEvent(resumeProgress, false, label));
                await UniTask.Delay(3000, cancellationToken: ct);
            }
        }

        Debug.Log($"[AIModelDownloader] {label} 다운로드 완료: {savePath}");
        _publisher.Publish(new AIModelDownloadProgressEvent(1f, true, label));
    }

    private async UniTask<long> FetchTotalBytesAsync(string url, string label, CancellationToken ct)
    {
        try
        {
            using var req = UnityWebRequest.Head(url);
            if (!string.IsNullOrEmpty(_config.huggingFaceToken))
                req.SetRequestHeader("Authorization", "Bearer " + _config.huggingFaceToken);
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);
            if (req.result != UnityWebRequest.Result.Success) return 0;
            string len = req.GetResponseHeader("Content-Length");
            return long.TryParse(len, out var n) ? n : 0;
        }
        catch (OperationCanceledException) 
        { 
            throw;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIModelDownloader] {label} 파일 크기 조회 실패: {e.Message}");
            return 0;
        }
    }

    private async UniTask TryDownloadChunkAsync(
        string url, string savePath, long currentBytes, long totalBytes, string label, CancellationToken ct)
    {
        using var req = UnityWebRequest.Get(url);
        if (!string.IsNullOrEmpty(_config.huggingFaceToken))
            req.SetRequestHeader("Authorization", "Bearer " + _config.huggingFaceToken);
        if (currentBytes > 0)
            req.SetRequestHeader("Range", "bytes=" + currentBytes + "-");

        // append:true → 이어받기 지원. removeFileOnAbort:false → 중단 시 파일 보존(다음 재시도에 이어받기 가능)
        var handler = new DownloadHandlerFile(savePath, append: currentBytes > 0);
        handler.removeFileOnAbort = false;
        req.downloadHandler = handler;
        req.disposeDownloadHandlerOnDispose = true;

        var op = req.SendWebRequest();

        // 진행률 폴링 — 메인 스레드만 사용
        while (!op.isDone)
        {
            if (ct.IsCancellationRequested) //다운로드 진행 중 중단
            {
                req.Abort();
                throw new OperationCanceledException(ct);
            }

            float progress = totalBytes > 0
                ? (float)(currentBytes + (long)req.downloadedBytes) / totalBytes
                : 0f;
            _publisher.Publish(new AIModelDownloadProgressEvent(progress, false, label));

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"{req.result}: {req.error}");
    }
}
