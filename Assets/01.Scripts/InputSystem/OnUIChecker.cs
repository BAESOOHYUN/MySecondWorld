using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OnUIChecker
{
    private static List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private static PointerEventData _pointerEventData;
    public static bool IsConversationActive { get; private set; }   // NPC와 대화·메뉴·상점이 열려 있는가
    public static bool IsMovementBlockedByUI 
        => IsConversationActive || IsInputFieldFocused(); //  NPC와 대화 중이거나 입력창에 글을 쓰는 중이면 캐릭터를 움직이지 않는다

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitStatics()
    {
        _raycastResults = new List<RaycastResult>();
        _pointerEventData = null;
        IsConversationActive = false;
    }

    public static List<RaycastResult> GetLastResults()
    {
        return _raycastResults;
    }

    public static bool IsPointerOverUIObject(Vector2 touchPos)
    {
        // EventSystem이 없는 씬이나 초기화 타이밍 보호
        // || InputAction에서 Contact 첫 프레임에(0, 0)이 넘어오는 케이스 방어
        if (EventSystem.current == null || touchPos == Vector2.zero)
            return false;

        // PointerEventData 캐싱 (매 프레임 new 할당 방지)
        if (_pointerEventData == null)
            _pointerEventData = new PointerEventData(EventSystem.current);
        _pointerEventData.position = touchPos;

        _raycastResults.Clear();

        // IsPointerOverGameObject 대신 이걸 쓰는 이유 : 모바일 터치, 마우스 클릭 구분하지 않고 Vector2 값으로만 판단하기 위해서.
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);

        return _raycastResults.Count > 0;
    }

    public static bool IsInputFieldFocused()
    {
        if (EventSystem.current == null) return false;
            var currObj = EventSystem.current.currentSelectedGameObject;
        
        return currObj != null 
            && currObj.TryGetComponent(out TMPro.TMP_InputField _); // 밑줄(_)은 "결과값을 쓰지 않겠다"는 의미, true/false만 판단
    }

    public static void SetConversationActive(bool active) => IsConversationActive = active;
}
