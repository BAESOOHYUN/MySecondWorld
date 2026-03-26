using UnityEngine;
using UnityEditor;
using System.IO;

// 부모(건물) 오브젝트를 선택하고 실행하면, 건물의 바닥 중앙을 기준으로 모든 피벗을 정렬
public class PivotToBottomCenter : EditorWindow
{
    [MenuItem("Tools/Align Pivot to Bottom Center (Mesh & Collider)")]
    public static void FixHierarchyPivot()
    {
        GameObject root = Selection.activeGameObject; // 건물(부모) 선택
        if (root == null || root.GetComponent<MeshFilter>() == null) return;

        // 1. 기준이 될 건물의 바닥 중앙(Local) 계산
        Mesh rootMesh = root.GetComponent<MeshFilter>().sharedMesh;
        Bounds b = rootMesh.bounds;
        Vector3 targetPivotLocal = new Vector3(b.center.x, b.min.y, b.center.z);

        // 2. 부모와 모든 자식 오브젝트의 메시 수정
        MeshFilter[] allMeshFilters = root.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in allMeshFilters)
        {
            // --- 메시 수정 ---
            Mesh newMesh = Instantiate(mf.sharedMesh);
            Vector3[] vertices = newMesh.vertices;

            // 자식의 경우, 부모와의 상대적 위치 차이를 고려하여 정점 이동
            // 여기서는 단순 구조를 가정하여 부모의 targetPivotLocal만큼 모든 정점을 밀어냄
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= targetPivotLocal;

            newMesh.vertices = vertices;
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();

            // 메시 파일 저장 (Assets/FixedMeshes 폴더 자동 생성)
            if (!AssetDatabase.IsValidFolder("Assets/FixedMeshes"))
                AssetDatabase.CreateFolder("Assets", "FixedMeshes");

            // 메시 에셋 저장 후 디스크에서 다시 로드 (Unity 에셋 시스템이 완전히 처리한 버전 사용)
            string path = $"Assets/FixedMeshes/{mf.name}_Fixed_{mf.GetInstanceID()}.asset";
            AssetDatabase.CreateAsset(newMesh, path);
            AssetDatabase.SaveAssets();
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            mf.sharedMesh = savedMesh;

            // --- 콜라이더 메시 교체 (컴포넌트 유지, sharedMesh만 swap) ---
            var meshCol = mf.GetComponent<MeshCollider>();
            if (meshCol != null)
            {
                meshCol.sharedMesh = null; // 캐시 강제 해제
                meshCol.sharedMesh = savedMesh;
                EditorUtility.SetDirty(mf.gameObject);
            }

            // --- 일반 콜라이더 보정 (Box, Capsule, Sphere 등) ---
            Collider[] colliders = mf.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                if (!(col is MeshCollider))
                {
                    if (col is BoxCollider box)
                        box.center -= targetPivotLocal;
                    else if (col is SphereCollider sphere)
                        sphere.center -= targetPivotLocal;
                    else if (col is CapsuleCollider capsule)
                        capsule.center -= targetPivotLocal;

                    col.enabled = false;
                    col.enabled = true;
                }
            }
        }

        // 3.월드 좌표 보정(시각적 위치 유지)
        Vector3 offsetWorld = root.transform.TransformVector(targetPivotLocal);
        root.transform.position += offsetWorld;

        // 3. 부모 오브젝트의 위치를 월드 좌표에서 보정
        //Vector3 currentPos = root.transform.position;
        //root.transform.position = new Vector3(currentPos.x, 0, currentPos.z);

        // 4. 변경사항 저장
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("메시와 콜라이더 위치 보정 완료!");
    }
}