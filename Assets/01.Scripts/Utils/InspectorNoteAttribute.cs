using UnityEngine;

namespace MySecondWorld.Foundation
{
    // Inspector에서 필드 위에 안내 상자를 띄우는 표시.  예) [InspectorNote("프리팹에서 꺼짐이 정상")]
    // 이 클래스는 메시지만 들고 있고, 실제로 그리는 건 Editor/InspectorNoteDrawer.cs다.
    // 에디터 전용 코드(UnityEditor)를 빌드에 넣지 않으려고 둘로 나눴다.
    public class InspectorNoteAttribute : PropertyAttribute
    {
        public readonly string Message;
        public InspectorNoteAttribute(string message) => Message = message;
    }
}