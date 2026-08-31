using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.Foundation;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 대상 하나의 HP를 슬라이더로 보여준다.
    /// IHealthView 구현체면 몬스터·플레이어·컴패니언 뭐든 붙는다.
    ///
    /// 붙는 방식은 셋이라 이 클래스는 Bind/Unbind만 하고 누가 물려주는지는 모른다:
    ///   · 몬스터   — MonsterHpBarLayer가 스폰마다 물려줌
    ///   · 플레이어 — LocalPlayerHealthBinder
    ///   · 컴패니언 — ParentHealthBinder (자기 부모에서 찾음)
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField, Tooltip("에셋팩 슬라이더")]
        private Slider _slider;

        [SerializeField, Tooltip("\"78/100\" 표시. 안 쓰면 비워두면 된다")]
        private TextMeshProUGUI _hpText;

        // [SerializeField] private Image _fillImage;   // ↓ 셰이더 방식으로 되돌릴 때 함께 부활

        private ScreenFollowUI _follow;   // 화면 고정 바(플레이어 HUD)에는 없다
        private IHealthView _view;

        private void Awake()
        {
            _follow = GetComponent<ScreenFollowUI>();

            if (_slider == null)
            {
                Debug.LogError($"[HealthBarUI] _slider 미할당 — {name}", this);
                enabled = false;
                return;
            }

            // 에셋 프리팹의 기본값이 0~1이 아닐 수 있다. 아니면 게이지가 엉뚱하게 찬다.
            _slider.minValue = 0f;
            _slider.maxValue = 1f;

            // 끌어서 HP를 바꾸면 안 되고, 켜져 있으면 이 슬라이더가 탭도 삼킨다.
            _slider.interactable = false;
        }

        public void Bind(IHealthView view)
        {
            Unbind();
            if (view == null) return;

            _view = view;
            _follow?.SetTarget(view.Anchor);

            Refresh();                  // HPChanged는 '변할 때만' 오므로 지금 값을 먼저 반영
            _view.HPChanged += Refresh;
        }

        public void Unbind()
        {
            if (_view != null)
            {
                _view.HPChanged -= Refresh;
                _view = null;
            }
            _follow?.SetTarget(null);
        }

        // 현재/최대를 한 번에 다시 읽는다 — 짝이 어긋난 값이 화면에 뜨지 않게.
        private void Refresh()
        {
            if (_view == null) return;

            float max = _view.MaxHP;
            float cur = _view.CurrentHP;

            if (_slider != null)
                _slider.value = max > 0f ? Mathf.Clamp01(cur / max) : 0f;

            if (_hpText != null)
                _hpText.text = $"{cur:N0}/{max:N0}";   // 5,400/10,000 형식
        }

        #region 보류 — UI/DiagonalFill 셰이더 채우기 방식
        // 에셋팩 슬라이더로 갈아타면서 뺐다. 셰이더(DiagonalFill.shader)와
        // 머티리얼(HPBarSliderMat)은 프로젝트에 그대로 남아 있으므로, 되살리려면 아래를 복구하면 된다.
        //
        // 되살릴 때 필요한 것:
        //   1. 위의 _fillImage 필드 주석 해제
        //   2. Awake의 _slider 분기 뒤에 아래 초기화 추가
        //   3. SetFill에 분기 추가
        //
        // ⚠️ 머티리얼 복제가 핵심이다. Renderer.material과 달리 Graphic.material은
        //    인스턴스를 자동 생성하지 않아서, 복제하지 않으면 모든 HP바가 같은 _FillAmount를
        //    공유한다 — 한 마리를 때리면 화면의 모든 바가 같이 줄어든다.
        //
        // private static readonly int FillAmount = Shader.PropertyToID("_FillAmount");
        // private Material _fillMaterial;
        //
        // // Awake에서:
        // if (_fillImage != null && _fillImage.material.HasProperty(FillAmount))
        // {
        //     _fillMaterial = new Material(_fillImage.material);
        //     _fillImage.material = _fillMaterial;
        // }
        //
        // // OnDestroy에서 (복제본은 직접 치워야 샌다):
        // if (_fillMaterial != null) Destroy(_fillMaterial);
        //
        // // SetFill에서:
        // if (_fillMaterial != null) _fillMaterial.SetFloat(FillAmount, normalized);
        // else if (_fillImage != null) _fillImage.fillAmount = normalized;   // 기본 Filled 이미지 대응
        #endregion
    }
}