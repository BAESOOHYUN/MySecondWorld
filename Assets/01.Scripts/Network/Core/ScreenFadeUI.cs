using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 화면 전체를 덮는 페이드 연출용 상시 캔버스.
    /// GameNetworkManager가 직접 보유(UIManager 미경유 — MySecondWorld.UI가 Network를 참조하는
    /// 단방향 구조라 순환참조 방지, PLAN/CONTEXT 결정 1 참고).
    /// </summary>
    public class ScreenFadeUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        public async UniTask FadeOut(float duration)
        {
            await Fade(0f, 1f, duration);
            _canvasGroup.blocksRaycasts = true; // 페이드아웃 완료 후 로드 중 입력 차단
        }

        public async UniTask FadeIn(float duration)
        {
            await Fade(1f, 0f, duration);
            _canvasGroup.blocksRaycasts = false;
        }

        private async UniTask Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _canvasGroup.alpha = to;
                return;
            }

            float elapsed = 0f;
            var ct = this.GetCancellationTokenOnDestroy();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                // PlayerLoopTiming.Update: MonoBehaviour의 Update()가 실행되는 바로 그 시점
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            _canvasGroup.alpha = to;
        }
    }
}
