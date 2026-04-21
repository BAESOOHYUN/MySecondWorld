using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MessagePipe;
using VContainer;
using MySecondWorld.AI;

public class AIModelDownloadUI: MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TextMeshProUGUI _progressText;

    [Inject]
    public void Construct(ISubscriber<AIModelDownloadProgressEvent> sub)
    {
        sub.Subscribe(OnProgress).AddTo(destroyCancellationToken);
    }

    public void OnProgress(AIModelDownloadProgressEvent evt)
    {
        if (evt.IsCompleted) 
        {
            Hide(); 
            return; 
        }

        _panel.SetActive(true);
        _progressBar.value = evt.Progress;
        _progressText.text = $"AI 모델을 다운로드 중입니다.\n(최초 한 번만 다운로드 됩니다.)\n{(int)(evt.Progress * 100)}%";
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }
}
