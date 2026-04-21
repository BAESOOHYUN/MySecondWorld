using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

//IStartable은 VContainer가 씬 시작 시 자동으로 Start()를 호출해주는 인터페이스
public class AIModelDownloadStarter : IStartable
{
    private readonly AIModelDownloader _downloader;
    public AIModelDownloadStarter(AIModelDownloader downloader)
    {
        _downloader = downloader;
    }

    public async void Start()
    {
        try
        {
            await _downloader.DownloadAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            Debug.LogError($"모델 다운로드 실패: {e.Message}");
        }
    }
}