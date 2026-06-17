using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Customization;

namespace MySecondWorld.Editor
{
    public static class ModularCharacterPartExtractor
    {
        // 통합 모델 프리팹 (Modular_Characters 하위 파츠 + Root 스켈레톤)
        private const string SourcePrefabPath = "Assets/03.Prefab/Avatar/ModularCharacters.prefab";
        private const string OutputFolder = "Assets/Addressables/CharacterParts";

        [MenuItem("Tools/Customization/Extract Modular Character Parts")]
        public static void Extract()
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[Extractor] 프리팹을 찾을 수 없음: {SourcePrefabPath}");
                return;
            }

            EnsureFolder(OutputFolder);

            // PrefabUtility.LoadPrefabContents = 프리팹을 임시 씬에 풀어서 편집 가능한 상태로 로드
            // (Instantiate와 달리 원본 에셋을 건드리지 않고, 끝나면 반드시 UnloadPrefabContents 호출 필요)
            var root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            try
            {
                var modularCharacters = root.transform.Find("Modular_Characters");
                if (modularCharacters == null)
                {
                    Debug.LogError("[Extractor] 'Modular_Characters' 자식을 찾을 수 없음");
                    return;
                }

                var renderers = modularCharacters.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Debug.Log($"[Extractor] {renderers.Length}개 파츠 발견, 추출 시작");
                int count = 0;

                foreach (var renderer in renderers)
                {
                    if (ExtractPart(modularCharacters, renderer))
                        count++;
                }
                Debug.Log($"[Extractor] 완료: {count}/{renderers.Length}개 파츠 프리팹 생성");
            }
            finally
            {  
                // 임시 씬 오브젝트 정리 — 누락 시 메모리에 계속 남음
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool ExtractPart(Transform modularCharactersRoot, SkinnedMeshRenderer source)
        {
            //  sharedMesh는 복사 없이 원본 에셋(ModularCharacters.fbx)참조 그대로 가져옴. 불필요한 메모리 복제를 피함.
            if (source.sharedMesh == null)
                return false;

            var folderPath = BuildGroupFolderPath(modularCharactersRoot, source.transform.parent);
            EnsureFolder(folderPath);
            var partName = source.gameObject.name;
            var assetPath = $"{folderPath}/{partName}.prefab";
            var tempGo = new GameObject(partName);
            try
            {
                var partRenderer = tempGo.AddComponent<SkinnedMeshRenderer>();
                partRenderer.sharedMesh = source.sharedMesh;
                // SkinnedMeshRenderer 하나가 머테리얼 슬롯을 여러 개 가질 수 있음
                // But 이 프로젝트에서 쓰는 SyntyStudio는 FantasyHero 머테리얼 하나에 의존하므로 사실상 배열[0]만 씀.
                // 혹시 모를 예외 + 확장을 위한 방어코드.
                partRenderer.sharedMaterials = source.sharedMaterials;
                // ↑ 여기서 끝. partRenderer.bones = ... 나 partRenderer.rootBone = ... 가 없음
                // bones/rootBone은 의도적으로 비움 — boneNames에 본 이름만 저장후 런타임에 RemapBones()가 실제 Root 스켈레톤으로 재할당
                var boneNames = tempGo.AddComponent<PartBoneNames>();
                // 이 파츠를 변형시키는 본은 여러 개(Inspector의 Root Bone 하나가 아님) → 전체 이름을 배열로 저장
                // ex) Torso(몸통) 메시 하나만 해도, 애니메이션 중에 자연스럽게 휘어지려면 척추(Spine), 가슴(Chest), 어깨(Shoulder L/R), 목(Neck) 등 여러 본의 영향을 동시에 받아야 함.
                boneNames.BoneNames = source.bones.Select(b => b.name).ToArray();

                // PrefabUtility.SaveAsPrefabAsset는 씬에 떠 있는 GameObject 인스턴스를 통째로 찍어서(.prefab 파일로) 디스크에 저장하는 함수
                PrefabUtility.SaveAsPrefabAsset(tempGo, assetPath);
                return true;
            }
            finally
            {
                // tempGo는 그 저장을 위한 "임시 모델"
                // Object.DestroyImmediate는 프리팹으로 저장한 직후 즉시 지우는 용도
                Object.DestroyImmediate(tempGo);
            }
        }

        // 재귀함수로 폴더경로 검색 후 없으면 생성
        private static void EnsureFolder(string path)
        {
            // System.IO.Directory.CreateDirectory()로 직접 만들면 메타파일이 없는 "고스트 폴더"가 생겨서 문제가 됨.
            // Unity는 .meta 파일과 GUID로 에셋을 추적하기 때문에 AssetDatabase를 통해 만들어야 GUID가 정상 생성
            if (AssetDatabase.IsValidFolder(path))
                return;

            // GetDirectoryName이 윈도우에서는 백슬래시(\)를 쓰는 경로를 반환할 수 있어서 .Replace("\\", "/")로 Unity가 쓰는 슬래시 형식으로 통일
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            // GetFileName "경로의 마지막 세그먼트"를 반환하는 함수
            var folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }

        // 파츠가 매달린 가장 아래(partParent)에서 시작해 parent를 타고 위로 올라가며 Modular_Characters에 도달할 때까지 반복
        // 예: Modular_Characters/Male_Parts/Male_00_Head/Male_Head_All_Elements
        private static string BuildGroupFolderPath(Transform modularCharactersRoot, Transform partParent)
        {
            var segments = new List<string>();
            var current = partParent;

            while (current != null && current != modularCharactersRoot)
            {
                // Insert(0, ...)를 쓰는 이유: 아래에서 위로 올라가면서 모으기 때문에,
                // 그냥 Add()로 쌓으면 순서가 거꾸로(Male_Head_All_Elements, Male_00_Head, Male_Parts)가 됨
                // 매번 리스트의 맨 앞(index 0)에 끼워 넣으면 자연스럽게 "위에서 아래로" 순서가 복원됨
                segments.Insert(0, current.name);
                current = current.parent;
            }

            var path = OutputFolder;
            foreach (var segment in segments)
                path += $"/{segment}";

            return path;
        }
    }
}