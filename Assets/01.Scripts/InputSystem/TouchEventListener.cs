using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using TMPro;
using R3;

public enum DeviceType
{
    PC,
    Mobile
}

public class TouchEventListener : MonoBehaviour, IInputAxisOwner
{
    public event Action<float, float> OnPriContactStarted;
    public event Action<float, float> OnSecContactStarted;
    public event Action<float, float> OnPriStartDrag;
    public event Action<float, float> OnSecStartDrag;
    public event Action<float, float> OnPriDrag;
    public event Action<float, float> OnSecDrag;
    public event Action OnPriEndDrag;
    public event Action OnSecEndDrag;
    public event Action<float> OnZoom;

    // Awake에서 실행 플랫폼으로 자동 결정한다. 인스펙터에서 바꿔도 실행 시 덮어써진다.
    public DeviceType deviceType;

    // 터치 시작 좌표가 UI 위였는지 여부 (터치 시작 프레임에 1회만 판정)
    public bool isPriStartPositionOnUI;
    public bool isSecStartPositionOnUI;

    // 현재 좌표가 UI 위인지 여부 (매 프레임 갱신)
    public bool isPriPositionOnUI;
    public bool isSecPositionOnUI;

    private Vector2 _priStartPosition;
    private Vector2 _secStartPosition;
    private float _prevPriDeltaX;
    private float _prevPriDeltaY;
    private float _prevSecDeltaX;
    private float _prevSecDeltaY;
    private float _prevPriPositionX;
    private float _prevPriPositionY;
    private float _prevSecPositionX;
    private float _prevSecPositionY;
    private float _pinchMinDist;
    private bool _isPriDragging;
    private bool _isSecDragging; 
    private float DRAG_THRESHOLD  => deviceType == DeviceType.PC? 5f : 15f;

    private readonly Subject<bool> _priContact = new();
    private readonly Subject<bool> _secContact = new();
    private readonly Subject<Vector2> _onTap = new();
    public Observable<Vector2> OnTap => _onTap;
    // .DistinctUntilChanged() 값이 바뀔 때만 통과 (true→true는 막고, false→true만 통과). 기존 코드의 priStarted = priDown && !_wasPriDown 역할과 완전히 동일
    // .Where(v => v) true인 것만 통과 (false는 버림) .Where(v => !v) false 것만 통과 (true는 버림)
    // .AsUnitObservable() bool값 필요없으니 Unit(빈 신호)으로 변환. bool 스트림 → 판단 용도. Unit 스트림 → 통보 용도
    public Observable<Unit> OnPriStarted => _priContact.DistinctUntilChanged().Where(v => v).AsUnitObservable();
    public Observable<Unit> OnPriEnded => _priContact.DistinctUntilChanged().Where(v => !v).AsUnitObservable();
    public Observable<Unit> OnSecStarted => _priContact.DistinctUntilChanged().Where(v => v).AsUnitObservable();
    public Observable<Unit> OnSecEnded => _priContact.DistinctUntilChanged().Where(v => !v).AsUnitObservable();

    #region InputAxis

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitDefaults()
    {
        DefaultPan = new InputAxis
        {
            Value = 0,
            Range = new Vector2(-180, 180),
            Wrap = true,
            Center = 0,
            Restrictions = InputAxis.RestrictionFlags.NoRecentering
        };

        DefaultTilt = new InputAxis
        {
            Value = 0,
            Range = new Vector2(-45, 45),
            Wrap = false,
            Center = 0,
            Restrictions = InputAxis.RestrictionFlags.NoRecentering
        };

        DefaultPositionX = new InputAxis
        {
            Value = 0,
            Range = new Vector2(0, Screen.width),
            Wrap = false,
            Center = 0,
            Restrictions = InputAxis.RestrictionFlags.Momentary
        };

        DefaultPositionY = new InputAxis
        {
            Value = 0,
            Range = new Vector2(0, Screen.height),
            Wrap = false,
            Center = 0,
            Restrictions = InputAxis.RestrictionFlags.Momentary
        };
    }
    public static InputAxis DefaultPan;
    public static InputAxis DefaultTilt;
    public static InputAxis DefaultPositionX;
    public static InputAxis DefaultPositionY;

    public InputAxis PriStartPositionX = DefaultPositionX;
    public InputAxis PriStartPositionY = DefaultPositionY;
    public InputAxis PriContact = InputAxis.DefaultMomentary;
    public InputAxis PriDeltaX = DefaultPan;
    public InputAxis PriDeltaY = DefaultTilt;
    public InputAxis PriPositionX = DefaultPositionX;
    public InputAxis PriPositionY = DefaultPositionY;

    public InputAxis SecStartPositionX = DefaultPositionX;
    public InputAxis SecStartPositionY = DefaultPositionY;
    public InputAxis SecContact = InputAxis.DefaultMomentary;
    public InputAxis SecDeltaX = DefaultPan;
    public InputAxis SecDeltaY = DefaultTilt;
    public InputAxis SecPositionX = DefaultPositionX;
    public InputAxis SecPositionY = DefaultPositionY;

    public InputAxis Zoom = InputAxis.DefaultMomentary;
    #endregion

    public void GetInputAxes(List<IInputAxisOwner.AxisDescriptor> axes)
    {
        axes.Add(new() { DrivenAxis = () => ref PriStartPositionX, Name = nameof(PriStartPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriStartPositionY, Name = nameof(PriStartPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PriContact, Name = nameof(PriContact) });
        axes.Add(new() { DrivenAxis = () => ref PriDeltaX, Name = nameof(PriDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriDeltaY, Name = nameof(PriDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref PriPositionX, Name = nameof(PriPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref PriPositionY, Name = nameof(PriPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });

        axes.Add(new() { DrivenAxis = () => ref SecStartPositionX, Name = nameof(SecStartPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecStartPositionY, Name = nameof(SecStartPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecContact, Name = nameof(SecContact) });
        axes.Add(new() { DrivenAxis = () => ref SecDeltaX, Name = nameof(SecDeltaX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecDeltaY, Name = nameof(SecDeltaY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
        axes.Add(new() { DrivenAxis = () => ref SecPositionX, Name = nameof(SecPositionX), Hint = IInputAxisOwner.AxisDescriptor.Hints.X });
        axes.Add(new() { DrivenAxis = () => ref SecPositionY, Name = nameof(SecPositionY), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });

        axes.Add(new() { DrivenAxis = () => ref Zoom, Name = nameof(Zoom), Hint = IInputAxisOwner.AxisDescriptor.Hints.Y });
    }

    private void Awake()
    {
        // 인스펙터 수동 설정에 의존하면 빌드마다 바꿔줘야 하고, 깜빡하면 폰에서 PC 프로파일로 돌아간다
        // (회전/줌 속도, 드래그 임계값이 전부 어긋남).
        deviceType = Application.isMobilePlatform ? DeviceType.Mobile : DeviceType.PC;
    }

    private void Start()
    {
        #region 터치 시작과 종료
        OnPriStarted.Subscribe(_ =>
        {
            _priStartPosition = new Vector2(PriStartPositionX.Value, PriStartPositionY.Value);
            isPriStartPositionOnUI = OnUIChecker.IsPointerOverUIObject(_priStartPosition);
            OnPriContactStarted?.Invoke(_priStartPosition.x, _priStartPosition.y);
        }).AddTo(this);

        OnPriEnded.Subscribe(_ =>
        {
            // 탭 감지
            bool wasTap = !_isPriDragging && !isPriStartPositionOnUI;
            Vector2 tapPos = new Vector2(PriPositionX.Value, PriPositionY.Value);

            _isPriDragging = false;
            OnPriEndDrag?.Invoke();
            ResetPri();

            // NPC 클릭시
            if (wasTap)
                _onTap.OnNext(tapPos);
        }).AddTo(this);

        OnSecStarted.Subscribe(_ =>
        {
            _secStartPosition = new Vector2(SecStartPositionX.Value, SecStartPositionY.Value);
            isSecStartPositionOnUI = OnUIChecker.IsPointerOverUIObject(_secStartPosition);
            OnSecContactStarted?.Invoke(_secStartPosition.x, _secStartPosition.y);
            // 핀치 이전 위치 초기화
            _prevPriPositionX = PriPositionX.Value;
            _prevPriPositionY = PriPositionY.Value;
            _prevSecPositionX = SecPositionX.Value;
            _prevSecPositionY = SecPositionY.Value;
        }).AddTo(this);

        OnSecEnded.Subscribe(_ =>
        {
            _isSecDragging = false;
            OnSecEndDrag?.Invoke();
            ResetSec();
            _prevPriPositionX = 0;
            _prevPriPositionY = 0;
            _prevSecPositionX = 0;
            _prevSecPositionY = 0;
        }).AddTo(this);
        #endregion
    }

    private void Update()
    {
        // 현재 위치 UI 판정 (매 프레임)
        isPriPositionOnUI = OnUIChecker.IsPointerOverUIObject(new Vector2(PriPositionX.Value, PriPositionY.Value));
        isSecPositionOnUI = OnUIChecker.IsPointerOverUIObject(new Vector2(SecPositionX.Value, SecPositionY.Value));
        UICheckDebug();

        if (deviceType == DeviceType.PC && !isPriPositionOnUI)
            OnZoom?.Invoke(Zoom.Value);

        _priContact.OnNext(PriContact.Value > 0);
        _secContact.OnNext(SecContact.Value > 0);
        bool priDown = PriContact.Value > 0;
        bool secDown = SecContact.Value > 0;

        #region 터치 무시
        // 아무 클릭도 안함
        if (!priDown && !secDown)
            return;

        //둘 다 UI 위에서 시작
        if (priDown && isPriStartPositionOnUI && secDown && isSecStartPositionOnUI)
            return;

        #endregion
        #region 단독 드래그
        bool priCanRotate = priDown && !isPriStartPositionOnUI;
        bool secCanRotate = secDown && !isSecStartPositionOnUI;

        // [0]번째 단독 드래그
        if (priCanRotate && !secCanRotate)
        {
            Vector2 currentPos = new Vector2(PriPositionX.Value, PriPositionY.Value);
            float dragDist = Vector2.Distance(currentPos, _priStartPosition);
            if (dragDist < DRAG_THRESHOLD)
                return;  // 임계값 미만이면 드래그 아님

            float dx = Mathf.DeltaAngle(_prevPriDeltaX, PriDeltaX.Value);
            float dy = Mathf.DeltaAngle(_prevPriDeltaY, PriDeltaY.Value);
            _prevPriDeltaX = PriDeltaX.Value;
            _prevPriDeltaY = PriDeltaY.Value;

            if (!_isPriDragging)
            {
                _isPriDragging = true;
                OnPriStartDrag?.Invoke(dx, dy);
            }
            else
                OnPriDrag?.Invoke(dx, dy);
        }

        // [1]번째 단독 드래그
        if (secCanRotate && !priCanRotate)
        {
            Vector2 currentPos = new Vector2(SecPositionX.Value, SecPositionY.Value);
            float dragDist = Vector2.Distance(currentPos, _secStartPosition);
            if (dragDist < DRAG_THRESHOLD)
                return;  // 임계값 미만이면 드래그 아님

            float dx = Mathf.DeltaAngle(_prevSecDeltaX, SecDeltaX.Value);
            float dy = Mathf.DeltaAngle(_prevSecDeltaY, SecDeltaY.Value);
            _prevSecDeltaX = SecDeltaX.Value;
            _prevSecDeltaY = SecDeltaY.Value;

            if (!_isSecDragging)
            {
                _isSecDragging = true;
                OnSecStartDrag?.Invoke(dx, dy);
            }
            else
                OnSecDrag?.Invoke(dx, dy);
        }
        #endregion
        #region 핀치
        // 핀치 불가 조건
        if (!priDown || !secDown
            || isPriStartPositionOnUI || isSecStartPositionOnUI
            || isPriPositionOnUI || isSecPositionOnUI)
            return;

        // 핀치 거리 계산
        float prevDX = _prevPriPositionX - _prevSecPositionX;
        float prevDY = _prevPriPositionY - _prevSecPositionY;
        float currDX = PriPositionX.Value - SecPositionX.Value;
        float currDY = PriPositionY.Value - SecPositionY.Value;

        float prevDist = Mathf.Sqrt(prevDX * prevDX + prevDY * prevDY);
        float currDist = Mathf.Sqrt(currDX * currDX + currDY * currDY);

        _prevPriPositionX = PriPositionX.Value;
        _prevPriPositionY = PriPositionY.Value;
        _prevSecPositionX = SecPositionX.Value;
        _prevSecPositionY = SecPositionY.Value;

        if (currDist < _pinchMinDist)
            return;

        OnZoom?.Invoke(prevDist - currDist);
        #endregion
    }

    public void ClearPri()
    {
        PriDeltaX.Value = 0;
        PriDeltaY.Value = 0;
        PriPositionX.Value = 0;
        PriPositionY.Value = 0;
        PriStartPositionX.Value = 0;
        PriStartPositionY.Value = 0;
        isPriPositionOnUI = false;
        isPriStartPositionOnUI = false;
    }

    public void ClearSec()
    {
        SecDeltaX.Value = 0;
        SecDeltaY.Value = 0;
        SecPositionX.Value = 0;
        SecPositionY.Value = 0;
        SecStartPositionX.Value = 0;
        SecStartPositionY.Value = 0;
        isSecPositionOnUI = false;
        isSecStartPositionOnUI = false;
    }

    public void ResetPri()
    {
        PriContact = InputAxis.DefaultMomentary;
        PriDeltaX = DefaultPan;
        PriDeltaY = DefaultTilt;
        PriStartPositionX = DefaultPositionX;
        PriStartPositionY = DefaultPositionY;
        PriPositionX = DefaultPositionX;
        PriPositionY = DefaultPositionY;
        isPriPositionOnUI = false;
        isPriStartPositionOnUI = false;
    }

    public void ResetSec()
    {
        SecContact = InputAxis.DefaultMomentary;
        SecDeltaX = DefaultPan;
        SecDeltaY = DefaultTilt;
        SecStartPositionX = DefaultPositionX;
        SecStartPositionY = DefaultPositionY;
        SecPositionX = DefaultPositionX;
        SecPositionY = DefaultPositionY;
        isSecPositionOnUI = false;
        isSecStartPositionOnUI = false;
    }

    private void UICheckDebug()
    {
        if (isPriPositionOnUI == true)
        {
            foreach (var hit in OnUIChecker.GetLastResults())
            {
                Debug.Log($"{hit.gameObject.name}");

                var img = hit.gameObject.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    Debug.Log($"{img.name} threshold = {img.alphaHitTestMinimumThreshold}");
                var tmp = hit.gameObject.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    Debug.Log($"TMP {tmp.name} raycastTarget = {tmp.raycastTarget}");
            }
        }
        if (isSecPositionOnUI)
        {
            foreach (var hit in OnUIChecker.GetLastResults())
            {
                var img = hit.gameObject.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    Debug.Log($"{img.name} threshold = {img.alphaHitTestMinimumThreshold}"); var tmp = hit.gameObject.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    Debug.Log($"TMP {tmp.name} raycastTarget = {tmp.raycastTarget}");
            }
        }
    }
}
