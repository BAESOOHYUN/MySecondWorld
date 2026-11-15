using UnityEngine;

namespace MySecondWorld.UI
{
    /// <summary>
    /// 월드 대상의 화면 좌표를 따라다니는 캔버스 UI.
    ///
    /// 월드스페이스 캔버스를 대상 프리팹에 붙이지 않는 이유:
    /// 몬스터 순수 프리팹은 Assets/Models 아래 37개이고 gitignore 대상이라,
    /// 캔버스를 붙이면 37번 반복 + 유실 시 37번 재적용이 된다.
    /// 화면 캔버스 하나가 좌표만 따라가면 프리팹을 아예 안 건드린다.
    /// </summary>
    public class ScreenFollowUI : MonoBehaviour
    {
        [SerializeField, Tooltip("대상 피벗 기준 오프셋(m). 머리 위에 띄우려면 Y를 올린다")]
        private Vector3 _worldOffset = new(0f, 2f, 0f);

        [SerializeField, Tooltip("최대 표시 거리(m). 이 이상 떨어지면 HPBar 숨김")]
        private float _maxDistance = 25f;
        private RectTransform _rect;
        private RectTransform _parentRect;
        private CanvasGroup _canvasGroup;
        private Camera _canvasCamera;      // Overlay 캔버스면 반드시 null
        private Transform _target;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parentRect = transform.parent as RectTransform;

            if (_parentRect == null)
            {
                Debug.LogError("[ScreenFollowUI] 부모가 RectTransform이 아닙니다 — 캔버스 아래에 두세요.", this);
                enabled = false;
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ScreenFollowUI] 부모에 Canvas가 없습니다.", this);
                enabled = false;
                return;
            }

            // Overlay 모드에 카메라를 넘기면 좌표 변환이 어긋난다.
            canvas = canvas.rootCanvas;
            _canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // SetActive로 숨기면 LateUpdate가 멈춰서 다시 켜질 수 없다. 알파로 숨긴다.
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 이 UI가 탭을 삼키면 안 된다 — 오늘 하루 날린 그 버그(안 보이는 UI가 OnTap을 막음)와 같은 종류다.
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            SetVisible(false);
        }

        private void LateUpdate()
        {
            // 풀 반납은 파괴가 아니라 SetActive(false)라서 null 검사만으론 못 거른다.
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                SetVisible(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) { SetVisible(false); return; }

            // 타겟과 거리 체크 (안하면 거리가 아주 멀어도 HPBar가 보이는 참사가 일어난다)
            float distance = Vector3.Distance(cam.transform.position, _target.position);
            if (distance > _maxDistance)
            {
                SetVisible(false);
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(_target.position + _worldOffset);

            // z <= 0 은 카메라 뒤. 그대로 두면 화면 반대편에 유령처럼 찍힌다.
            if (screen.z <= 0f) { SetVisible(false); return; }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, screen, _canvasCamera, out Vector2 local))
            {
                SetVisible(false);
                return;
            }

            _rect.localPosition = local;
            SetVisible(true);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (target == null) SetVisible(false);
        }

        private void SetVisible(bool value)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = value ? 1f : 0f;
        }
    }
}