using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OnUIChecker
{
    private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();


    // TODO : 원 아이콘 바깥 영역에 포인터가 있어도 네모진 UI 영역 안에 있으면 true로 체크되는 문제
    public static bool IsPointerOverUIObject(Vector2 touchPos)
    {
        // EventSystem이 없는 씬이나 초기화 타이밍 보호
        if (EventSystem.current == null)
            return false;

        // 좌표가 유효하지 않으면 UI로 간주하지 않음
        // (InputAction에서 Contact 첫 프레임에 (0,0)이 넘어오는 케이스 방어)
        if (touchPos == Vector2.zero)
            return false;

        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current)
        {
            position = touchPos
        };

        _raycastResults.Clear(); // 매번 Clear만 하면 됨, 재할당 없음

        // IsPointerOverGameObject 대신 이걸 쓰는 이유 : 모바일 터치, 마우스 클릭 구분하지 않고 Vector2 값으로만 판단하기 위해서.
        EventSystem.current.RaycastAll(eventDataCurrentPosition, _raycastResults);

        return _raycastResults.Count > 0;
    }
}
