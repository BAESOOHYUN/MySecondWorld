using UnityEngine;
using UnityEngine.EventSystems;

namespace MySecondWorld.UI
{
    // 프리뷰 패널 RawImage에 부착
    // 가로 드래그 → 캐릭터 Y축 회전(턴테이블) / 세로 드래그 → 카메라 상하 궤도(피치)
    // 스크롤 → 포커스 포인트 기준 줌
    public class PreviewInteractionHandler : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] private Transform _characterRoot;  // PreviewCharacter 루트 (턴테이블 회전 대상)
        [SerializeField] private Camera _previewCamera;

        [SerializeField] private float _focusHeight = 1.0f; // 카메라가 바라볼 기준 높이 (가슴 정도). Play모드에서 보면서 조절

        [SerializeField] private float _rotateSpeed = 0.3f;

        [SerializeField] private float _pitchMin = -20f;
        [SerializeField] private float _pitchMax = 80f;

        [SerializeField] private float _zoomSpeed = 0.05f;  // 0.3 → 0.05 로 낮춤 (한 칸당 이동량 축소)
        [SerializeField] private float _zoomMin = 0.6f;       // 범위 자체를 넓힘 (더 가까이)
        [SerializeField] private float _zoomMax = 6.0f;       // 더 멀리

        private float _yaw;      // 카메라 수평 각도 (최초 배치 기준, 고정)
        private float _pitch;    // 카메라 수직 각도 (드래그로 변경)
        private float _distance; // 포커스 포인트로부터 카메라까지 거리 (스크롤로 변경)

        private Vector3 FocusPoint => _characterRoot.position + Vector3.up * _focusHeight;

        private void Start()
        {
            EnsureReferences();
            InitCameraOrbit();
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitCameraOrbit();
        }

        private void EnsureReferences()
        {
            if (_characterRoot == null)
            {
                var customizer = Object.FindAnyObjectByType<MySecondWorld.Customization.LocalCharacterCustomizer>();
                if (customizer != null)
                {
                    _characterRoot = customizer.transform;
                }
                else
                {
                    var pc = GameObject.Find("PreviewCharacter");
                    if (pc != null) _characterRoot = pc.transform;
                }
            }

            if (_previewCamera == null)
            {
                var rawImg = GetComponent<UnityEngine.UI.RawImage>();
                if (rawImg != null && rawImg.texture != null)
                {
                    var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                    foreach (var cam in cams)
                    {
                        if (cam.targetTexture != null && cam.targetTexture == rawImg.texture)
                        {
                            _previewCamera = cam;
                            break;
                        }
                    }
                }

                if (_previewCamera == null)
                {
                    var camGo = GameObject.Find("Preview Camera") ?? GameObject.Find("Camera");
                    if (camGo != null) _previewCamera = camGo.GetComponent<Camera>();
                }
            }
        }

        private void InitCameraOrbit()
        {
            if (_previewCamera == null || _characterRoot == null) return;

            // 씬에 배치된 카메라의 현재 위치로부터 distance/yaw/pitch 역산
            Vector3 offset = _previewCamera.transform.position - FocusPoint;
            _distance = Mathf.Clamp(offset.magnitude, _zoomMin, _zoomMax);

            Vector3 dir = offset.normalized;
            _pitch = Mathf.Clamp(Mathf.Asin(dir.y) * Mathf.Rad2Deg, _pitchMin, _pitchMax);
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            ApplyCameraOrbit(); // 포커스 포인트를 정확히 바라보도록 초기 스냅
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_characterRoot == null) EnsureReferences();
            if (_characterRoot == null) return;

            // 가로 드래그 → 캐릭터 자체 회전 (턴테이블)
            _characterRoot.Rotate(Vector3.up, -eventData.delta.x * _rotateSpeed, Space.World);

            // 세로 드래그 → 카메라 피치 (위아래 궤도 이동).
            _pitch = Mathf.Clamp(_pitch - eventData.delta.y * _rotateSpeed, _pitchMin, _pitchMax);
            ApplyCameraOrbit();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_previewCamera == null || _characterRoot == null) EnsureReferences();
            if (_previewCamera == null || _characterRoot == null) return;

            // 마우스/터치패드마다 휠 한 칸의 델타 크기가 달라서 ±1로 정규화 후 속도를 곱함
            // → 휠을 빠르게 굴리면 여러 번 호출돼서 누적되니, 한 칸당 이동량은 일정하게 유지됨
            float scrollStep = Mathf.Clamp(eventData.scrollDelta.y, -1f, 1f);

            _distance = Mathf.Clamp(
                _distance - scrollStep * _zoomSpeed,
                _zoomMin, _zoomMax);

            ApplyCameraOrbit();
        }

        private void ApplyCameraOrbit()
        {
            float pitchRad = _pitch * Mathf.Deg2Rad;
            float yawRad = _yaw * Mathf.Deg2Rad;
            float horizontalRadius = Mathf.Cos(pitchRad) * _distance;

            Vector3 dir = new Vector3(
                horizontalRadius * Mathf.Sin(yawRad),
                Mathf.Sin(pitchRad) * _distance,
                horizontalRadius * Mathf.Cos(yawRad));

            Vector3 focus = FocusPoint;
            _previewCamera.transform.position = focus + dir;
            _previewCamera.transform.LookAt(focus);
        }
    }
}