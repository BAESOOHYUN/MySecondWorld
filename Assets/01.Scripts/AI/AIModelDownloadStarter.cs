using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;
using Application = UnityEngine.Application;

// IStartable은 VContainer가 씬 시작 시 자동으로 Start()를 호출해주는 인터페이스
public class AIModelDownloadStarter : IStartable, IDisposable
{
    private readonly AIModelDownloader _downloader;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public AIModelDownloadStarter(AIModelDownloader downloader)
    {
        _downloader = downloader;
        // VContainer Dispose는 GameObject OnDestroy 단계에서 호출되어 메인 스레드 정지가 임박한 시점이다.
        // Application.quitting에서 미리 cancel해야 진행 중인 UnityWebRequest 작업이
        // OperationCanceledException으로 즉시 중단되어 정상 종료된다.
        Application.quitting += OnAppQuitting;
    }

    private void OnAppQuitting()
    {
        try { _cts.Cancel(); } catch { }
    }

    public async void Start()
    {
        // 첫 번째 try-catch가 완전히 끝난 후에 두 번째 try-catch가 실행
        try
        {
            await _downloader.DownloadLLMAsync(_cts.Token);

            // null 체크 추가 (서버 모드 시 SelectedFileName = null로 만들기)
            if (!string.IsNullOrEmpty(_downloader.SelectedFileName))
            {
                // 다운로드 완료 후 파일명을 PlayerPrefs에 저장
                // → MainScene의 LLMInitializer가 읽어서 llm.model에 절대경로 주입
                PlayerPrefs.SetString("SelectedModelFileName", _downloader.SelectedFileName);
                PlayerPrefs.Save();

                Debug.Log($"[AIModelDownloadStarter] LLM 다운로드 완료 → {_downloader.SelectedFileName}");
            }
            else
            {
                // 서버 모드 — 이전 세션의 잔여 경로값 제거. PlayerPrefs를 명시적으로 지우는 게 더 안전
                PlayerPrefs.DeleteKey("SelectedModelFileName");
                PlayerPrefs.Save();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[AIModelDownloadStarter] 모델 다운로드 실패: {e.Message}");
        }

        try
        {
            await _downloader.DownloadSTTAsync(_cts.Token);
            Debug.Log("[AIModelDownloadStarter] STT 완료");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[AIModelDownloadStarter] STT 다운로드 실패: {e.Message}");
        }
    }

    public void Dispose()
    {
        Application.quitting -= OnAppQuitting;
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
    }
}
