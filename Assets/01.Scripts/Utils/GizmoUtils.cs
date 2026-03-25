using UnityEngine;

namespace MySecondWorld.Utils
{
    public static class GizmoUtils
    {
        /// <summary>
        /// Collider 형태에 맞게 반투명 기즈모를 그린다. 에디터 모드 전용.
        /// </summary>
        /// <param name="col">대상 Collider</param>
        /// <param name="t">Collider가 붙은 Transform (localToWorldMatrix 기준)</param>
        /// <param name="color">기즈모 색상 (alpha는 fill 기준)</param>
        /// <param name="fillAlpha">채우기 투명도 (0~1)</param>
        public static void DrawCollider(Collider col, Transform t, Color color, float fillAlpha)
        {
            if (Application.isPlaying) return;
            if (col == null) return;

            var fill = new Color(color.r, color.g, color.b, fillAlpha);
            var wire = new Color(color.r, color.g, color.b, Mathf.Clamp01(fillAlpha * 3f));

            Gizmos.matrix = t.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.color = fill; Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = wire; Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.color = fill; Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = wire; Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
}
