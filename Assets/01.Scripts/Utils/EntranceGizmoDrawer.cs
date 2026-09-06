using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MySecondWorld.Utils
{
    /// <summary>
    /// 플레이 중이 아닐 때(에디터 모드)에도 Scene 뷰에서 Entrance 오브젝트를 빨간색 기즈모로 표시해주는 헬퍼 컴포넌트.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("MySecondWorld/Utils/Entrance Gizmo Drawer")]
    public class EntranceGizmoDrawer : MonoBehaviour
    {
        public enum DrawMode
        {
            CombinedRendererBounds, // 자식 렌더러 전체 영역 바운딩 박스
            EntranceDoorFocus,      // 실제 입구 오브젝트(SM_Env_Entrance_Crypt_02 등) 중심 포커스
            CustomBox,              // 커스텀 중심/크기 박스
            CustomSphere            // 커스텀 구체
        }

        [Header("Gizmo Settings")]
        [Tooltip("기즈모 색상 (기본: 빨강)")]
        [SerializeField] private Color _gizmoColor = Color.red;

        [Tooltip("반투명 면 채우기 여부")]
        [SerializeField] private bool _drawFill = true;

        [Tooltip("채우기 투명도 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _fillAlpha = 0.18f;

        [Tooltip("와이어프레임 표시 여부")]
        [SerializeField] private bool _drawWire = true;

        [Header("Draw Mode")]
        [SerializeField] private DrawMode _mode = DrawMode.CombinedRendererBounds;

        [Header("Door Focus Target (EntranceDoorFocus 모드용)")]
        [Tooltip("비워두면 'SM_Env_Entrance_Crypt_02'를 자동으로 찾아 기준점으로 사용합니다.")]
        [SerializeField] private Transform _doorTarget;
        [SerializeField] private Vector3 _doorBoxSize = new Vector3(8f, 6f, 8f);

        [Header("Custom Bounds (CustomBox / CustomSphere 모드용)")]
        [SerializeField] private Vector3 _customCenter = Vector3.zero;
        [SerializeField] private Vector3 _customSize = new Vector3(10f, 5f, 10f);
        [SerializeField] private float _sphereRadius = 5f;

        [Header("Label Settings")]
        [SerializeField] private bool _showLabel = true;
        [SerializeField] private string _labelText = "Entrance";

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            bool isSelected = Selection.activeGameObject == gameObject ||
                              (Selection.gameObjects != null && System.Array.IndexOf(Selection.gameObjects, gameObject) >= 0);
            DrawGizmo(isSelected);
#else
            DrawGizmo(false);
#endif
        }

        private void DrawGizmo(bool isSelected)
        {
            if (!enabled) return;

            Bounds bounds = CalculateBounds();
            if (bounds.size == Vector3.zero && _mode == DrawMode.CombinedRendererBounds)
            {
                bounds = new Bounds(transform.position, Vector3.one * 2f);
            }

            float currentFillAlpha = isSelected ? Mathf.Min(1f, _fillAlpha * 1.6f) : _fillAlpha;
            Color fillColor = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, currentFillAlpha);
            Color wireColor = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, isSelected ? 1f : 0.85f);

            Matrix4x4 oldMatrix = Gizmos.matrix;

            if (_mode == DrawMode.CustomSphere)
            {
                Vector3 center = transform.TransformPoint(_customCenter);
                if (_drawFill)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawSphere(center, _sphereRadius);
                }
                if (_drawWire)
                {
                    Gizmos.color = wireColor;
                    Gizmos.DrawWireSphere(center, _sphereRadius);
                }
            }
            else
            {
                if (_drawFill)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawCube(bounds.center, bounds.size);
                }
                if (_drawWire)
                {
                    Gizmos.color = wireColor;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }

            Gizmos.matrix = oldMatrix;

#if UNITY_EDITOR
            if (_showLabel)
            {
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = isSelected ? Color.yellow : wireColor;
                labelStyle.fontSize = 12;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.MiddleCenter;

                Vector3 labelPos = bounds.center + Vector3.up * (bounds.extents.y + 0.5f);
                Handles.Label(labelPos, $"[{_labelText}]", labelStyle);
            }
#endif
        }

        private Bounds CalculateBounds()
        {
            if (_mode == DrawMode.EntranceDoorFocus)
            {
                Vector3 center = GetDoorCenter();
                return new Bounds(center, _doorBoxSize);
            }

            if (_mode == DrawMode.CustomBox)
            {
                return new Bounds(transform.TransformPoint(_customCenter), _customSize);
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }
                return combined;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            if (colliders != null && colliders.Length > 0)
            {
                Bounds combined = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    combined.Encapsulate(colliders[i].bounds);
                }
                return combined;
            }

            return new Bounds(transform.position, Vector3.one * 2f);
        }

        private Vector3 GetDoorCenter()
        {
            if (_doorTarget != null)
            {
                return _doorTarget.position;
            }

            Transform crypt = transform.Find("SM_Env_Entrance_Crypt_02");
            if (crypt != null)
            {
                return crypt.position;
            }

            Transform dirt = transform.Find("SM_Env_Entrance_Dirt_01");
            if (dirt != null)
            {
                return dirt.position;
            }

            return transform.position;
        }
    }
}
