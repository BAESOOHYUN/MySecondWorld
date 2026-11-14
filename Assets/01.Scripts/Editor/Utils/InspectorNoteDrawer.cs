using UnityEditor;
using UnityEngine;
using MySecondWorld.Foundation;
using UnityEngine.UIElements;

// DecoratorDrawer: 필드 자체는 그대로 두고, 그 위에 추가 UI만 얹는 드로어
[CustomPropertyDrawer(typeof(InspectorNoteAttribute))]
public class InspectorNoteDrawer : DecoratorDrawer
{
    private const float Margin = 4f;   // 상자 아래 여백

    // 상자의 높이 = 메시지가 지금 Inspector 폭에서 차지하는 높이 (줄바꿈 반영)
    public override float GetHeight()
    {
        var note = (InspectorNoteAttribute)attribute;
        float width = EditorGUIUtility.currentViewWidth - 40f;   // 좌우 여백과 정보 아이콘 자리
        return EditorStyles.helpBox.CalcHeight(new GUIContent(note.Message), width) + Margin;
    }

    public override void OnGUI(Rect position)
    {
        var note = (InspectorNoteAttribute)attribute;
        position.height -= Margin;
        EditorGUI.HelpBox(position, note.Message, MessageType.Info);
    }
    public override VisualElement CreatePropertyGUI()
    {
        var note = (InspectorNoteAttribute)attribute;
        return new HelpBox(note.Message, HelpBoxMessageType.Info);   // 줄바꿈과 높이는 UI Toolkit이 알아서 처리
    }
}