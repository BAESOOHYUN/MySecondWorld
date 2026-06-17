using MySecondWorld.Customization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MySecondWorld.Editor
{
    // AvatarPartKeyTable을 (자동으로) 채워주는 도구
    public class AvatarPartKeyTablePopulator : EditorWindow
    {
        private const string SourceRoot = "Assets/Addressables/CharacterParts";
        private static readonly string[] GenderRootFolders =
        {
            "Male_Parts",
            "Female_Parts",
            "All_Gender_Parts"
        };
        
        // "Female_00_Head" / "Male_04_Arm_Upper_Right" / "00_Ear" 등에서
        // 정규식으로 앞의 (성별_)번호_ 부분을 떼고 카테고리 이름만 뽑아냄
        private static readonly Regex CategoryPattern = new(@"^(?:Female_|Male_|All_)?\d+_(.+)$");

        // "Female_00_Head" / "Male_04_Arm_Upper_Right" / "00_Ear" 등에서
        // 앞의 (성별_)번호_ 부분을 떼고 카테고리 이름만 뽑아냄
        private static readonly Dictionary<string, PartType> CategoryAliases = new()
        {
            { "Head_Attachment", PartType.HelmetAttachment },
            { "Shoulder_Attachment_Right", PartType.ShoulderAttachRight },
            { "Shoulder_Attachment_Left", PartType.ShoulderAttachLeft },
            { "Elbow_Attachment_Right", PartType.ElbowAttachRight },
            { "Elbow_Attachment_Left", PartType.ElbowAttachLeft },
            { "Knee_Attachement_Right", PartType.KneeAttachRight },   // 폴더명 오타(Attachement) 그대로 키로 사용
            { "Knee_Attachement_Left", PartType.KneeAttachLeft },
            { "Extra", PartType.Ear },                                 // All_12_Extra 안엔 Elf_Ear만 존재
        };

        private AvatarPartKeyTableSO _target;

        [MenuItem("Tools/Customization/Populate Avatar Part Key Table")]
        private static void Open()
        {
            GetWindow<AvatarPartKeyTablePopulator>("Avatar Part Key Table Populator");
        }

        public void OnGUI()
        {
            // Object 타입 값을 AvatarPartKeyTableSO 타입 변수에 그냥 대입하면 컴파일 에러남. (AvatarPartKeyTableSO)로 명시적 다운캐스트를 해줘야 대입이 가능
            // (typeof(AvatarPartKeyTableSO))는 Unity가 Inspector UI를 그릴 때 "이 칸에는 AvatarPartKeyTableSO 타입만 드래그 가능하게 필터링해라"라고 알려주는 용도
            _target = (AvatarPartKeyTableSO)EditorGUILayout.ObjectField("Target SO", _target, typeof(AvatarPartKeyTableSO), false);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"스캔 경로 : {SourceRoot}", MessageType.Info);

            if (_target == null) return;
            if (GUILayout.Button("Populate (덮어쓰기)"))
            {
                Populate();
            }
        }

        private void Populate()
        {
            var unmatchedFolders = new List<string>();
            var addedCount = 0;

            foreach (var genderRoot in GenderRootFolders)
            {
                // rootPath : Male_Parts/Female_Parts/All_Gender_Parts 
                var rootPath = $"{SourceRoot}/{genderRoot}";

                if(!AssetDatabase.IsValidFolder(rootPath))
                {
                    // Male_Parts/Female_Parts/All_Gender_Parts 루트 폴더 자체가 존재하는지 체크
                    Debug.LogWarning($"폴더 없음, 스킵: {rootPath}");
                    continue;
                }

                // GetSubFolders 직계 자식 폴더들의 경로 배열을 반환 (하위 폴더의 하위 폴더는 포함 안 됨, 1단계만)
                // → Male_Parts의 "직계 자식"들: Male_00_Head, Male_01_Eyebrows, Male_03_Torso...
                var categoryFolders = AssetDatabase.GetSubFolders(rootPath);
                foreach (var categoryFolder in categoryFolders)
                {
                    var folderName = Path.GetFileName(categoryFolder);
                    // categoryFolders 폴더 안의 개별 카테고리 폴더 하나하나가 매칭되는지 체크
                    if (!TryResolvePartType(folderName, out var partType))
                    {
                        unmatchedFolders.Add(categoryFolder);
                        continue;
                    }

                    // new[] { categoryFolder }  "검색 범위를 이 폴더(들) 안으로 한정하라"
                    // 그 폴더 밑에 몇 단계가 더 있든 전부 재귀로 찾아냄.
                    var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { categoryFolder })
                                                   .OrderBy(guid => AssetDatabase.GUIDToAssetPath(guid))
                                                   .ToArray();
                    var refs = prefabGuids.Select(guid => new AssetReferenceGameObject(guid))
                                          .ToList();

                    AssignToEntry(partType, genderRoot, refs);
                    addedCount += refs.Count;
                }
            }

            EditorUtility.SetDirty(_target);
            AssetDatabase.SaveAssets();
            Debug.Log($"AvatarPartKeyTable 채우기 완료: {addedCount}개 추가.");

            if (unmatchedFolders.Count > 0)
            {
                Debug.LogWarning(
                    "매칭 실패 폴더 (CategoryAliases에 추가 필요):\n" +
                    string.Join("\n", unmatchedFolders));
            }
        }

        private static bool TryResolvePartType(string folderName, out PartType partType)
        {
            var match = CategoryPattern.Match(folderName);
            var category = match.Success ? match.Groups[1].Value : folderName;
            var normalized = category.Replace("_", "");

            // CategoryPattern 정규식으로 접두사·번호를 먼저 떼어낸 카테고리 문자열을 비교 대상으로 바꾼 뒤에야 제대로 동작
            if (Enum.TryParse(normalized, true, out partType)) 
                return true;
            // 1차 시도(Enum.TryParse)가 실패했을 때
            // 2차로 CategoryAliases 사전에 등록된 별칭으로 한 번 더 시도

            // Dictionary의 TryGetValue는 표준 패턴
            // 폴더 이름이 PartType enum 이름과 글자 그대로 똑같지 않을 때를 위한 "이름 변환 사전"
            // Synty가 만든 실제 폴더명이 우리 enum과 정확히 안 맞을 가능성에 대한 대비용 빈 사전
            // 키가 사전에 있으면 true + 값 채움, 없으면 false + partType은 기본값
            return CategoryAliases.TryGetValue(category, out partType);
        }

        private void AssignToEntry(PartType partType, string genderRoot, List<AssetReferenceGameObject> refs)
        {
            var entry = _target.Entries.Find(e => e.PartType == partType);
            if (entry == null)
            {
                entry = new AvatarPartKeyTableSO.PartEntry { PartType = partType };
                _target.Entries.Add(entry);
            }

            // C# 8.0부터 생긴 문법. 값을 직접 반환하는 식(expression)
            var list = genderRoot switch
            {
                "Male_Parts" => entry.MaleParts,
                "Female_Parts" => entry.FemaleParts,
                "All_Gender_Parts" => entry.AllGenderParts,
                _ => null // _는 default
            };

            // list는 entry.MaleParts 등 기존 리스트에 대한 참조(레퍼런스). entry.MaleParts 자체를 비우는 것
            // Populator를 한 번 실행한 뒤 나중에 프리팹을 추가하고 다시 실행하면 기존 리스트에 새 항목을 또 AddRange로 추가하면 중복이 생김
            // 매번 실행할 때 "일단 비우고 → 지금 스캔한 결과로 새로 채운다"는 방식으로, 여러 번 실행해도 항상 같은 결과가 나오게(idempotent)
            list?.Clear();
            list?.AddRange(refs);
        }
    }
}