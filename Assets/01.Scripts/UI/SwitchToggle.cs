using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class SwitchToggle : MonoBehaviour
{
    // 좌우로 미는 토글
    [SerializeField] private RectTransform _handle;
    [SerializeField] private Image _background;
    [SerializeField] private Color _onColor = new Color(0.3f, 0.8f, 0.4f);
    [SerializeField] private Color _offColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private float _speed = 10f;

    private Toggle _toggle;
    private float _t;   // 0 = 꺼짐(왼쪽), 1 = 켜짐(오른쪽)

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        _t = _toggle.isOn ? 0.9f : 0.1f;   // 켜질 때 현재 상태로 바로 맞춤(밀리는 연출 없이)
        Apply();
    }

    private void Update()
    {
        float target = _toggle.isOn ? 0.9f : 0.1f;
        if (Mathf.Approximately(_t, target)) return;
        _t = Mathf.MoveTowards(_t, target, _speed * Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply()
    {
        float range = _background.rectTransform.rect.width - _handle.rect.width;
        _handle.anchoredPosition = new Vector2(range * _t, 0f);
        _background.color = Color.Lerp(_offColor, _onColor, _t);
    }
}