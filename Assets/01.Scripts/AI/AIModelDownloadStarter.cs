using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

// IStartable은 VContainer가 씬 시작 시 자동으로 Start()를 호출해주는 인터페이스
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

            // 다운로드 완료 후 파일명을 PlayerPrefs에 저장
            // → MainScene의 LLMInitializer가 읽어서 llm.model에 절대경로 주입
            PlayerPrefs.SetString("SelectedModelFileName", _downloader.SelectedFileName);
            PlayerPrefs.Save();

            Debug.Log($"[AIModelDownloadStarter] 다운로드 완료 → {_downloader.SelectedFileName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AIModelDownloadStarter] 모델 다운로드 실패: {e.Message}");
        }
    }
}
