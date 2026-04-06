using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > UI Click Settings Applier
/// Canvas/프리팹을 드래그 앤 드롭하면 하위 모든 Image/TMP에 클릭 설정을 일괄 적용.
/// Image: alphaHitTestMinimumThreshold 적용
/// TMP: raycastTarget = false
/// </summary>
public class UIAlphaThresholdSetter : EditorWindow
{
    private List<GameObject> targets = new List<GameObject>();
    private float alphaThreshold = 0.5f;
    private Vector2 scrollPos;

    [MenuItem("Tools/UI AlphaThreshold Setter")]
    static void Open()
    {
        GetWindow<UIAlphaThresholdSetter>("UI AlphaThreshold Settings");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Canvas / 프리팹을 드래그해서 추가", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 1f);
        EditorGUILayout.Space();

        // 드래그 앤 드롭 영역
        var dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "여기에 GameObject / 프리팹을 드롭");
        HandleDragAndDrop(dropArea);

        EditorGUILayout.Space();

        // 추가된 목록 표시
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            targets[i] = (GameObject)EditorGUILayout.ObjectField(targets[i], typeof(GameObject), true);
            if (GUILayout.Button("X", GUILayout.Width(20)))
                targets.RemoveAt(i);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("적용", GUILayout.Height(30)))
            Apply();
        if (GUILayout.Button("목록 비우기", GUILayout.Height(30)))
            targets.Clear();
        EditorGUILayout.EndHorizontal();
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        var evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition))
            return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
                break;
            case EventType.DragPerform:
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && !targets.Contains(go))
                        targets.Add(go);
                }
                evt.Use();
                break;
        }
    }

    private void Apply()
    {
        int imageCount = 0;
        int tmpCount = 0;

        foreach (var target in targets)
        {
            if (target == null) continue;

            foreach (var image in target.GetComponentsInChildren<Image>(true))
            {
                Undo.RecordObject(image, "Apply Alpha Threshold");
                image.alphaHitTestMinimumThreshold = alphaThreshold;
                EditorUtility.SetDirty(image);
                imageCount++;
            }

            foreach (var tmp in target.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Undo.RecordObject(tmp, "Disable TMP RaycastTarget");
                tmp.raycastTarget = false;
                EditorUtility.SetDirty(tmp);
                tmpCount++;
            }
        }

        Debug.Log($"[UIClickSettings] Image {imageCount}개 threshold 적용, TMP {tmpCount}개 raycast 해제 완료");
    }
}
