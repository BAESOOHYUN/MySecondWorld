using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MySecondWorld.Network;

/// <summary>
/// ZoneDefinition SO를 Inspector에서 클릭하면 자동으로 이 에디터가 표시된다
/// </summary>
[CustomEditor(typeof(ZoneDefinition))]
public class ZoneBoundsCalculatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // SO의 최신 데이터를 Inspector에 가져옴
        serializedObject.Update();

        // 기존 필드 표시
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Calculate Bounds from Scene"))
        {
            CalculateBounds();
        }

        // Inspector에서 수정한 값을 SO에 저장
        serializedObject.ApplyModifiedProperties();
    }

    // --- 기즈모: SO 선택 시 씬 뷰에 bounds + preloadMargin 시각화 ---
    // OnSceneGUI는 씬 오브젝트의 CustomEditor에서만 호출됨.
    // ScriptableObject는 씬에 없으므로 SceneView.duringSceneGui 이벤트를 사용.

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneViewGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneViewGUI;
    }

    private void OnSceneViewGUI(SceneView sceneView)
    {
        ZoneDefinition zoneDef = target as ZoneDefinition;
        if (zoneDef == null || zoneDef.bounds.size == Vector3.zero) return;

        // bounds (플레이어 소속 판정 영역) — 초록색
        Handles.color = Color.green;
        Handles.DrawWireCube(zoneDef.bounds.center, zoneDef.bounds.size);

        // bounds + preloadMargin (미리 로드 영역) — 노란색 반투명
        Handles.color = new Color(1f, 1f, 0f, 0.5f);
        Vector3 expandedSize = zoneDef.bounds.size + Vector3.one * zoneDef.preloadMargin * 2;
        Handles.DrawWireCube(zoneDef.bounds.center, expandedSize);

        // 라벨
        Handles.color = Color.white;
        Handles.Label(
            zoneDef.bounds.center + Vector3.up * (zoneDef.bounds.size.y / 2 + 2f),
            $"{zoneDef.name}\nbounds: {zoneDef.bounds.size}\npreload: {expandedSize}");
    }

    private void CalculateBounds()
    {
        ZoneDefinition zoneDef = (ZoneDefinition)target;
        //  sceneName 비어있으면 경고
        if (string.IsNullOrEmpty(zoneDef.sceneName))
        {
            EditorUtility.DisplayDialog(
                "ZoneBoundsCalculator",
                "sceneName이 비어있습니다. Inspector에서 씬을 먼저 지정해주세요.",
                "확인"
            );
            return;
        }

        // 씬이 이미 로드되어 있는지 확인
        Scene existingScene = EditorSceneManager.GetSceneByPath(zoneDef.sceneName);
        bool wasAlreadyLoaded = existingScene.IsValid() && existingScene.isLoaded;

        Scene targetScene;
        if (wasAlreadyLoaded)
        {
            targetScene = existingScene;
        }
        else
        {
            targetScene = EditorSceneManager.OpenScene(zoneDef.sceneName, OpenSceneMode.Additive);
        }

        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "ZoneBoundsCalculator",
                $"씬을 열 수 없습니다: {zoneDef.sceneName}",
                "확인"
            );
            return;
        }

        // 씬 내 모든 Renderer의 bounds 합산
        bool initialized = false;
        Bounds combinedBounds = new Bounds();

        foreach (GameObject rootObj in targetScene.GetRootGameObjects())
        {
            // MeshRenderer, SkinnedMeshRenderer 같은 렌더러
            // Renderer를 기준으로 bounds를 계산한다.
            Renderer[] renderers = rootObj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (!initialized)
                {
                    combinedBounds = renderer.bounds;
                    initialized = true;
                }
                else
                {   // 여러 개의 bounds를 하나의 큰 bounds로 합치는 메서드
                    // 씬의 모든 오브젝트를 감싸는 하나의 큰 박스가 된다.
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        // 원래 로드되지 않은 씬이었다면 다시 언로드
        if (!wasAlreadyLoaded)
        {
            EditorSceneManager.CloseScene(targetScene, true);
        }

        if (!initialized)
        {
            EditorUtility.DisplayDialog(
                "ZoneBoundsCalculator",
                $"씬에서 Renderer를 하나도 찾지 못했습니다: {zoneDef.sceneName}\n" +
                "Bounds를 계산할 수 없습니다.",
                "확인"
            );
            return;
        }

        // 결과를 SO에 반영. 둘 다 해야 bounds 계산 결과가 SO 파일에 확실히 저장된다.
        zoneDef.bounds = combinedBounds;
        EditorUtility.SetDirty(zoneDef); // SO가 수정됐음을 Unity에게 알려주는 메서드. 이걸 안 하면 Unity가 변경된 걸 모름.
        AssetDatabase.SaveAssets();      // dirty 표시된 에셋들을 디스크에 실제로 저장. 이걸 안 하면 Unity 끄면 날아감.
 
        Debug.Log(
            $"[ZoneBoundsCalculator] '{zoneDef.sceneName}' bounds 계산 완료\n" +
            $"  Center: {combinedBounds.center}\n" +
            $"  Size:   {combinedBounds.size}"
        );
    }
}
