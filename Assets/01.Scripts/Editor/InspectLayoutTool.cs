using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace MySecondWorld.EditorTools
{
    public static class InspectLayoutTool
    {
        [MenuItem("Tools/Inspect UI Layout")]
        public static void Inspect()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== UI LAYOUT & COMPONENT INSPECTION ===");
            InspectPrefab("Assets/03.Prefab/UI/Canvas/Inventory Canvas.prefab", sb);
            InspectPrefab("Assets/03.Prefab/UI/Canvas/ShopCanvas.prefab", sb);

            string outPath = "c:/UnityProject/MySecondWorld/.gemini/memory/ui_layout_inspection.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[InspectLayoutTool] Detailed dump written to {outPath}");
        }

        private static void InspectPrefab(string path, StringBuilder sb)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                sb.AppendLine($"\n==========================================");
                sb.AppendLine($"PREFAB: {path}");
                sb.AppendLine($"==========================================");
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    sb.AppendLine($"CanvasScaler: mode={scaler.uiScaleMode}, refRes={scaler.referenceResolution}, matchMode={scaler.screenMatchMode}, match={scaler.matchWidthOrHeight}");
                }

                DumpHierarchyAndComponents(root.transform, 0, sb);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DumpHierarchyAndComponents(Transform t, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);
            RectTransform rt = t as RectTransform;
            if (rt != null)
            {
                sb.AppendLine($"{indent}- [{t.name}] (active={t.gameObject.activeSelf})");
                sb.AppendLine($"{indent}    Rect: anchorMin=({rt.anchorMin.x:F2}, {rt.anchorMin.y:F2}), anchorMax=({rt.anchorMax.x:F2}, {rt.anchorMax.y:F2}), pivot=({rt.pivot.x:F2}, {rt.pivot.y:F2}), pos=({rt.anchoredPosition.x:F1}, {rt.anchoredPosition.y:F1}), size=({rt.sizeDelta.x:F1}, {rt.sizeDelta.y:F1})");

                // Check key UI layout components
                var glg = t.GetComponent<GridLayoutGroup>();
                if (glg != null)
                {
                    sb.AppendLine($"{indent}    [GridLayoutGroup] cellSize=({glg.cellSize.x}, {glg.cellSize.y}), spacing=({glg.spacing.x}, {glg.spacing.y}), constraint={glg.constraint}, constraintCount={glg.constraintCount}, padding=({glg.padding.left},{glg.padding.right},{glg.padding.top},{glg.padding.bottom})");
                }
                var hlg = t.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    sb.AppendLine($"{indent}    [HorizontalLayoutGroup] spacing={hlg.spacing}, forceExpand=({hlg.childForceExpandWidth},{hlg.childForceExpandHeight}), controlSize=({hlg.childControlWidth},{hlg.childControlHeight}), padding=({hlg.padding.left},{hlg.padding.right},{hlg.padding.top},{hlg.padding.bottom})");
                }
                var vlg = t.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    sb.AppendLine($"{indent}    [VerticalLayoutGroup] spacing={vlg.spacing}, forceExpand=({vlg.childForceExpandWidth},{vlg.childForceExpandHeight}), controlSize=({vlg.childControlWidth},{vlg.childControlHeight}), padding=({vlg.padding.left},{vlg.padding.right},{vlg.padding.top},{vlg.padding.bottom})");
                }
                var csf = t.GetComponent<ContentSizeFitter>();
                if (csf != null)
                {
                    sb.AppendLine($"{indent}    [ContentSizeFitter] horiz={csf.horizontalFit}, vert={csf.verticalFit}");
                }
                var le = t.GetComponent<LayoutElement>();
                if (le != null)
                {
                    sb.AppendLine($"{indent}    [LayoutElement] min=({le.minWidth}, {le.minHeight}), pref=({le.preferredWidth}, {le.preferredHeight}), flex=({le.flexibleWidth}, {le.flexibleHeight})");
                }
                var sr = t.GetComponent<ScrollRect>();
                if (sr != null)
                {
                    sb.AppendLine($"{indent}    [ScrollRect] horiz={sr.horizontal}, vert={sr.vertical}, mvt={sr.movementType}");
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (t.name.Contains("Content") && i > 2)
                {
                    if (i == 3) sb.AppendLine($"{indent}  ... ({t.childCount - 3} more slot items omitted)");
                    continue;
                }
                if ((t.name == "Armor_Slots" || t.name == "Accessory_Slots") && i > 1)
                {
                    if (i == 2) sb.AppendLine($"{indent}  ... ({t.childCount - 2} more paperdoll slots omitted)");
                    continue;
                }
                DumpHierarchyAndComponents(child, depth + 1, sb);
            }
        }
    }
}
