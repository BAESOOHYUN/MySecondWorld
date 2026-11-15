using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.UI
{
    // 예/아니오 확인 팝업. ShopUI 등 여러 화면에서 공용으로 쓸 수 있도록 자체 캔버스로 분리했다.
    // UIManager.GetOrCreateDynamicCanvas<ConfirmPanelUI>()로 지연 생성되며, 프리팹의 Canvas에
    // Override Sorting + 높은 Sort Order를 줘서 어떤 화면 위에서도 항상 최상단에 뜨게 한다.
    public class ConfirmPanelUI : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_Text _titleText;   // 프리팹의 제목 글자("구매 확인")를 연결
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton;

        private Action _onConfirm;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(HandleConfirm);
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Hide);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
            if (_backdropButton != null)
                _backdropButton.onClick.AddListener(Hide);

            if (_canvas != null)
                _canvas.enabled = false;
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        // 기존 호출(ShopUI)은 그대로 쓰도록 유지 — 제목은 반드시 "구매 확인"으로 되돌린다
        public void Show(string message, string confirmButtonLabel, Action onConfirm)
            => Show("구매 확인", message, confirmButtonLabel, onConfirm);

        // 로그아웃UI 등에서 Title변경
        public void Show(string title, string message, string confirmButtonLabel, Action onConfirm)
        {
            if (_titleText != null) _titleText.text = title;
            gameObject.SetActive(true); // GetOrCreateDynamicCanvas가 생성 직후 비활성화하므로 재활성화 필요
            _messageText.text = message;

            var confirmLabel = _confirmButton.GetComponentInChildren<TMP_Text>(true);
            if (confirmLabel != null)
                confirmLabel.text = confirmButtonLabel;

            _onConfirm = onConfirm;
            _canvas.enabled = true;
        }

        public void Hide()
        {
            if (_canvas != null)
                _canvas.enabled = false;
            _onConfirm = null;
        }

        private void HandleConfirm()
        {
            var callback = _onConfirm;
            Hide();
            callback?.Invoke();
        }
    }
}
