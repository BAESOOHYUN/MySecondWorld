using UnityEngine;

namespace MySecondWorld.UI
{
    // 초상화 전용 카메라 1개를 재사용한다. 상시 렌더 금지 — 필요한 시점(스폰/장비 변경 등)에만 Capture() 호출.
    // 다른 스크립트(CompanionHUDController 등)의 OnEnable보다 Instance가 먼저 세팅되도록 강제
    [DefaultExecutionOrder(-100)]
    public class PortraitCapture : MonoBehaviour
    {
        public static PortraitCapture Instance { get; private set; }

        [SerializeField] private Camera _camera;
        [SerializeField] private float _fieldOfView = 25f;

        private void Awake()
        {
            Instance = this;
            if (_camera == null) _camera = GetComponent<Camera>();
            _camera.enabled = false;
            _camera.fieldOfView = _fieldOfView;
        }

        public void Capture(Transform subject, RenderTexture destination, float faceHeight, float distance)
        {
            if (subject == null || destination == null) return;

            Vector3 facePos = subject.position + Vector3.up * faceHeight;
            _camera.transform.position = facePos + subject.forward * distance;
            _camera.transform.LookAt(facePos);

            _camera.targetTexture = destination;
            _camera.Render();
            _camera.targetTexture = null;
        }
    }
}