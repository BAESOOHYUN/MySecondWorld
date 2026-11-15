using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MessagePipe;
using VContainer;
using MySecondWorld.AI;

namespace MySecondWorld.UI
{
    public class AIModelDownloadUI : MonoBehaviour
    {
        [SerializeField] private GameObject _canvas;
        [SerializeField] private Slider _llmProgressBar;
        [SerializeField] private TextMeshProUGUI _llmProgressText;
        [SerializeField] private Slider _sttProgressBar;
        [SerializeField] private TextMeshProUGUI _sttProgressText;

        [Inject]
        public void Construct(ISubscriber<AIModelDownloadProgressEvent> sub)
        {
            sub.Subscribe(OnProgress).AddTo(destroyCancellationToken);
        }

        private void Awake()
        {
#if UNITY_SERVER
    enabled = false;
#endif
        }

        public void OnProgress(AIModelDownloadProgressEvent evt)
        {
            _canvas.SetActive(true);

            if(evt.ModelLabel == "LLM")
            {
                _sttProgressBar.gameObject.SetActive(false);
                _llmProgressBar.value = Mathf.Clamp01(evt.Progress);
                _llmProgressText.text = $"LLM 모델 다운로드 중...\n(최초 한 번만 다운로드 됩니다.)\n{Mathf.Min(100, (int)(evt.Progress * 100))}%";
                if (evt.IsCompleted)
                {
                    _llmProgressBar.gameObject.SetActive(false);
                    _sttProgressBar.gameObject.SetActive(true);
                }
                return;
            }
            else if (evt.ModelLabel == "STT")
            {
                _llmProgressBar.gameObject.SetActive(false);
                _sttProgressBar.value = Mathf.Clamp01(evt.Progress);
                _sttProgressText.text = $"STT 모델 다운로드 중...\n(최초 한 번만 다운로드 됩니다.)\n{Mathf.Min(100, (int)(evt.Progress * 100))}%";
                if (evt.IsCompleted)
                {
                    _sttProgressBar.gameObject.SetActive(false);
                    Hide();
                }
                return;
            }
        }

        public void Hide()
        {
            _canvas.SetActive(false);
        }
    }
}