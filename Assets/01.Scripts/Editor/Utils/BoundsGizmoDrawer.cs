using UnityEditor;
using UnityEngine;

namespace MySecondWorld.EditorTools
{
    /// <summary>
    /// ScriptableObject의 Bounds 필드를 Scene 뷰에 와이어박스로 그리는 공용 헬퍼.
    /// ScriptableObject는 씬에 없어 OnSceneGUI가 호출되지 않으므로,
    /// 호출부(CustomEditor)가 SceneView.duringSceneGui에 직접 구독해서 이 메서드를 불러야 한다.
    /// </summary>
    public static class BoundsGizmoDrawer
    {
        public static void Draw(Bounds bounds, string label, Color color)
        {
            if (bounds.size == Vector3.zero) return;
            Handles.color = color;
            Handles.DrawWireCube(bounds.center, bounds.size);
            Handles.color = Color.white;
            Handles.Label(bounds.center + Vector3.up * (bounds.size.y / 2f + 1f), label);
        }

        /// <summary>
        /// 바깥으로 margin만큼 확장한 두 번째 박스를 겹쳐 그린다 (preloadMargin 등 시각화용).
        /// </summary>
        public static void DrawExpanded(Bounds bounds, float margin, Color color)
        {
            if (bounds.size == Vector3.zero || margin <= 0f) return;

            Handles.color = color;
            Vector3 expandedSize = bounds.size + Vector3.one * margin * 2f;
            Handles.DrawWireCube(bounds.center, expandedSize);
        }
    }
}
