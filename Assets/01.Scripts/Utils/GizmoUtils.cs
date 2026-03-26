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

        /// <summary>
        /// CharacterController 캡슐 형태의 기즈모를 그린다.
        /// </summary>
        public static void DrawCharacterController(CharacterController cc, Transform t, Color color, float fillAlpha)
        {
            if (cc == null) return;

            var wire = new Color(color.r, color.g, color.b, Mathf.Clamp01(fillAlpha * 3f));
            Gizmos.color = wire;
            Gizmos.matrix = Matrix4x4.identity;

            DrawWireCapsule(
                t.TransformPoint(cc.center),
                t.rotation,
                cc.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z),
                cc.height * t.lossyScale.y
            );
        }

        /// <summary>
        /// 월드 좌표 기준 와이어 캡슐을 그린다.
        /// </summary>
        public static void DrawWireCapsule(Vector3 center, Quaternion rotation, float radius, float height)
        {
            float halfHeight = Mathf.Max(height * 0.5f, radius);
            float cylinderHalf = halfHeight - radius;

            Vector3 up    = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 fwd   = rotation * Vector3.forward;

            Vector3 top    = center + up * cylinderHalf;
            Vector3 bottom = center - up * cylinderHalf;

            // 세로 라인 4개
            Gizmos.DrawLine(top + right * radius,   bottom + right * radius);
            Gizmos.DrawLine(top - right * radius,   bottom - right * radius);
            Gizmos.DrawLine(top + fwd   * radius,   bottom + fwd   * radius);
            Gizmos.DrawLine(top - fwd   * radius,   bottom - fwd   * radius);

            // 위아래 원
            DrawWireCircle(top,    up, radius);
            DrawWireCircle(bottom, up, radius);

            // 위 반구 (앞뒤/좌우 호)
            DrawWireArc(top, right, up,  radius, 180f);
            DrawWireArc(top, fwd,   up,  radius, 180f);

            // 아래 반구 (앞뒤/좌우 호)
            DrawWireArc(bottom, right, -up, radius, 180f);
            DrawWireArc(bottom, fwd,   -up, radius, 180f);
        }

        static void DrawWireCircle(Vector3 center, Vector3 normal, float radius, int segments = 24)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();

            Vector3 prev = center + tangent * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 360f / segments * Mathf.Deg2Rad;
                Vector3 next = center + Quaternion.AngleAxis(i * 360f / segments, normal) * tangent * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        static void DrawWireArc(Vector3 center, Vector3 from, Vector3 axis, float radius, float angleDeg, int segments = 12)
        {
            from.Normalize();
            Vector3 prev = center + from * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = angleDeg * i / segments;
                Vector3 next = center + Quaternion.AngleAxis(t, axis) * from * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
