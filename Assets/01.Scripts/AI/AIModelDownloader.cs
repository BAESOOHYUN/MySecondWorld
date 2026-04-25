using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Application = UnityEngine.Application;
using MessagePipe;
using MySecondWorld.AI;

public class AIModelDownloader
{
    private readonly IPublisher<AIModelDownloadProgressEvent> _publisher;
    private readonly AIModelConfigSO _config;
    private readonly IDeviceCapability _device;

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
        // 기기 역량 평가
        _device.Evaluate();

        // 다운로드 없이 조기 반환
        if (!_device.IsCapable)
        {
            Debug.LogWarning($"[AIModelDownloader] 로컬 추론 불가 — {_device.Reason}");
            _publisher.Publish(new AIModelDownloadProgressEvent(0f, false));
            return;
        }

        // 모델 URL/파일명 분기
        bool useLarge = _device.RecommendedModelSize == ModelSize.Large;
        string url = useLarge ? _config.modelUrl: _config.smallModelUrl;
        string fileName = useLarge ? _config.modelFileName: _config.smallModelFileName;
        Debug.LogWarning($"[AIModelDownloader] useLarge {useLarge}, url {url}, {_device.Reason} → {fileName}");

        // 파일이 이미 있으면 스킵
        string savePath = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(savePath))
        {
            _publisher.Publish(new AIModelDownloadProgressEvent(1f, true));
            return;
        }

        // UnityWebRequest로 다운로드
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerFile(savePath);
        req.SendWebRequest();

        // 진행률마다 _publisher.Publish()
        try
        {
            while (!req.isDone)
            {
                _publisher.Publish(new AIModelDownloadProgressEvent(req.downloadProgress, false));
                await UniTask.Delay(500, cancellationToken: ct);
            }
        }
        finally                
        {
            // finally는 ct 취소로 예외가 던져져도 반드시 실행된다.
            // req.isDone이 아직 false면 = 아직 다운로드 중이면 Abort()로 HTTP 요청을 중단
            // ct.Cancel() → UniTask.Delay가 예외 던짐 → while 루프 탈출 → finally 블록 실행
            // → req.Abort()로 실제 네트워크 요청도 중단.
            if (!req.isDone)
                req.Abort();
        }

        // 완료
        if (req.result == UnityWebRequest.Result.Success)
        {
            _publisher.Publish(new AIModelDownloadProgressEvent(1f, true));
            Debug.Log($"[AIModelDownloader] {fileName} 다운로드 완료");
        }
        else
        {
            throw new System.Exception($"다운로드 실패 : {req.error}");
        }
    }

}
