using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Customization;

// Extractor가 이미 추출 시점에 BoneNames+RootBoneName을 모두 채우므로,
// 이 Baker는 "원본 프리팹이 바뀌었을 때 기존 파츠를 일괄 재동기화"하는 유지보수 도구다.
public class PartBoneBakerWindow : EditorWindow
{
    private GameObject _sourcePrefab;

    [MenuItem("Tools/Customization/Bake Part Bone Data")]
    static void Open() => GetWindow<PartBoneBakerWindow>("Part Bone Baker");

    void OnGUI()
    {
        _sourcePrefab = (GameObject)EditorGUILayout.ObjectField(
            "원본 Synty 프리팹", _sourcePrefab, typeof(GameObject), false);

        EditorGUI.BeginDisabledGroup(_sourcePrefab == null);
        if (GUILayout.Button("Bake"))
            Bake(_sourcePrefab);
        EditorGUI.EndDisabledGroup();
    }

    static void Bake(GameObject source)
    {
        // Pass 1: 원본에서 Mesh → (rootBone 이름, bones 이름 배열) 수집
        var meshToRootBone = new Dictionary<Mesh, string>();
        var meshToBoneNames = new Dictionary<Mesh, string[]>();
        foreach (var smr in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;
            if (smr.rootBone != null)
                meshToRootBone[smr.sharedMesh] = smr.rootBone.name;
            if (smr.bones != null && smr.bones.Length > 0)
                meshToBoneNames[smr.sharedMesh] = smr.bones.Select(b => b?.name ?? string.Empty).ToArray();
        }
        Debug.Log($"[BakeBoneData] 원본에서 {meshToRootBone.Count}개 메시 수집");

        // Pass 2: PartBoneNames 있는 파츠 프리팹 업데이트
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var pbn = prefab.GetComponentInChildren<PartBoneNames>();
            if (pbn == null) continue;

            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            bool changed = false;

            if (meshToRootBone.TryGetValue(smr.sharedMesh, out string rootName)
                && pbn.RootBoneName != rootName)
            {
                pbn.RootBoneName = rootName;
                changed = true;
            }

            if (meshToBoneNames.TryGetValue(smr.sharedMesh, out string[] boneNames)
                && !BoneNamesEqual(pbn.BoneNames, boneNames))
            {
                pbn.BoneNames = boneNames;
                changed = true;
            }

            if (!changed) continue;

            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BakeBoneData] {count}개 파츠 완료");
    }

    private static bool BoneNamesEqual(string[] a, string[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
