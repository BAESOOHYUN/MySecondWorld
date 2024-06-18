using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OnUIChecker
{
    public static bool IsPointerOverUIObject(Vector2 touchPos)
    {
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current)
        {
            position = touchPos
        };

        List<RaycastResult> results = new List<RaycastResult>();

        // IsPointerOverGameObject 대신 이걸 쓰는 이유 : 터치, 클릭 구분하지 않고 Vector2 값으로만 판단하기 위해서.
        // result는 out인듯? DLL확인
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        return results.Count > 0;
    }
}
