using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using MySecondWorld.Customization;

namespace MySecondWorld.Editor
{
    // AvatarPartKeyTable을 (자동으로) 채워주는 도구
    public class AvatarPartKeyTablePopulator : EditorWindow
    {
        private AvatarPartKeyTableSO _target;
        private const string SourceRoot = "Assets/03.Prefab/CharacterParts";
        private const string ThumbnailRoot = "Assets/07.Textures/CharacterPartThumbnails";

        private static readonly string[] GenderRootFolders =
        {
            "Male_Parts",
            "Female_Parts",
            "All_Gender_Parts"
        };

        // 그룹 C(부착형 악세서리) — 원본 폴더가 _01부터 시작해 "미착용" 후보가 없음.
        // 이 PartType들은 스캔 결과 리스트 맨 앞에 빈 슬롯(인덱스 0)을 코드로 삽입해준다.
        private static readonly HashSet<PartType> GroupCAccessoryParts = new()
        {
            PartType.HeadCoverings, PartType.BackAttachment,
            PartType.ShoulderAttachLeft, PartType.ShoulderAttachRight,
            PartType.ElbowAttachLeft, PartType.ElbowAttachRight,
            PartType.KneeAttachLeft, PartType.KneeAttachRight,
            PartType.HelmetAttachment, PartType.HipsAttachment
        };

        // 썸네일 이름→참조 사전 만드는 메서드
        // 썸네일 이름에서 "_Static"을 뗀 게 곧 파츠 프리팹 이름.
        // 예: 썸네일 "Chr_BackAttachment_01_Static" → 키 "Chr_BackAttachment_01"
        private static Dictionary<string, string> BuildThumbnailLookup()
        {
            var dict = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { ThumbnailRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                var partName = fileName.EndsWith("_Static") ?
                    fileName.Substring(0, fileName.Length - "_Static".Length) : fileName;

                dict[partName] = fileName; // 아틀라스 안 스프라이트 이름 (Chr_XXX_Static)
            }
            return dict;
        }

        [MenuItem("Tools/Customization/Populate Avatar Part Key Table")]
        private static void Open()
        {
            GetWindow<AvatarPartKeyTablePopulator>("Avatar Part Key Table Populator");
        }

        public void OnGUI()
        {
            _target = EditorAssetDefaults.DrawWithDefault("Target SO", _target, EditorAssetDefaults.AvatarPartKeyTablePath);

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
            var missingThumbs = new List<string>();
            var addedCount = 0;
            var thumbByName = BuildThumbnailLookup();

            // 0단계 : rootPath : Male_Parts/Female_Parts/All_Gender_Parts 성별 루트
            foreach (var genderRoot in GenderRootFolders)
            {
                var rootPath = $"{SourceRoot}/{genderRoot}";

                if(!AssetDatabase.IsValidFolder(rootPath))
                {
                    // Male_Parts/Female_Parts/All_Gender_Parts 루트 폴더 자체가 존재하는지 체크
                    Debug.LogWarning($"폴더 없음, 스킵: {rootPath}");
                    continue;
                }

                // 1단계 : categoryFolders : 직계 자식 폴더(GetSubFolders)들의 경로 배열을 반환 (하위 폴더의 하위 폴더는 포함 안 됨, 1단계만)
                // → Male_Parts의 "직계 자식"들: Male_00_Head, Male_01_Eyebrows, Male_03_Torso...
                var categoryFolders = AssetDatabase.GetSubFolders(rootPath);
                foreach (var categoryFolder in categoryFolders)
                {
                    var folderName = Path.GetFileName(categoryFolder);
                    // categoryFolders 폴더 안의 개별 카테고리 폴더 하나하나가 매칭되는지 체크
                    // 1단계 폴더명 문자열 → PartType enum 값으로 변환 (폴더 자체는 건드리지 않음)
                    if (!PartTypeResolver.TryResolvePartType(folderName, out var partType))
                    {
                        unmatchedFolders.Add(categoryFolder);
                        continue;
                    }

                    // 1단계 카테고리 폴더 기준, 그 밑에 몇 단계가 더 있든(2단계 하위 폴더 포함) 전부 재귀로 검색
                    // new[] { categoryFolder }  "검색 범위를 이 폴더(들) 안으로 한정하라"
                    // .OrderBy(...).ToArray() 검색된 프리팹 GUID를 경로 알파벳 순 정렬 
                    var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { categoryFolder })
                                                   .OrderBy(guid => AssetDatabase.GUIDToAssetPath(guid))
                                                   .ToArray();
                    // AssetReferenceGameObject 리스트로 변환
                    var refs = prefabGuids.Select(guid => new AssetReferenceGameObject(guid))
                                          .ToList();

                    // 파츠와 "같은 순서"로 썸네일 참조를 만든다 (인덱스 정합 보장)
                    var thumbs = prefabGuids.Select(guid =>
                    {
                        var partName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                        if (thumbByName.TryGetValue(partName, out var t)) 
                            return t;
                        missingThumbs.Add(partName);
                        return string.Empty;   // 짝 없으면 null (UI에서 placeholder 처리)
                    }).ToList();

                    // 파츠와 동일 순서로 서브타입 문자열 추출
                    // 직접 categoryFolder에 있으면 "" / 서브폴더에 있으면 서브폴더 이름에서 partType 접두사 제거
                    var subTypes = prefabGuids.Select(guid =>
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
                        var parentDir = Path.GetDirectoryName(path).Replace("\\", "/");
                        if (parentDir == categoryFolder.Replace("\\", "/"))
                            return string.Empty;
                        var subFolderName = Path.GetFileName(parentDir);     // ex) "Head_All_Elements"

                        // 성별 접두사 제거 (Male_Head_All_Elements → Head_All_Elements)
                        foreach (var genderPrefix in new[] { "Male_", "Female_", "All_" })
                        {
                            if (subFolderName.StartsWith(genderPrefix))
                            {
                                subFolderName = subFolderName.Substring(genderPrefix.Length);
                                break;
                            }
                        }

                        // 파츠 타입 접두사 제거 (Head_All_Elements → All_Elements)
                        var partTypePrefix = partType.ToString() + "_";
                        return subFolderName.StartsWith(partTypePrefix)
                            ? subFolderName.Substring(partTypePrefix.Length)
                            : subFolderName;
                    }).ToList();

                    // 그룹 C 한정: 인덱스 0에 "미착용" 빈 슬롯 삽입 (실제 프리팹 에셋 없이 코드로만 처리)
                    if (GroupCAccessoryParts.Contains(partType))
                    {
                        refs.Insert(0, new AssetReferenceGameObject(string.Empty));
                        thumbs.Insert(0, string.Empty);
                        subTypes.Insert(0, string.Empty);
                    }

                    // SO의 Entries에서 해당 PartType 항목을 찾거나 새로 생성하고,
                    // genderRoot에 맞는 리스트(MaleParts/FemaleParts/AllGenderParts)를 비우고 다시 채움
                    AssignToEntry(partType, genderRoot, refs, thumbs, subTypes);
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

            if (missingThumbs.Count > 0)
            {
                Debug.LogWarning(
                    $"썸네일 매칭 실패 {missingThumbs.Count}개:\n" +
                    string.Join("\n", missingThumbs));
            }
        }

        private void AssignToEntry(PartType partType, 
                                    string genderRoot, 
                                    List<AssetReferenceGameObject> refs,
                                    List<string> thumbs,
                                    List<string> subTypes)
        {
            var entry = _target.Entries.Find(e => e.PartType == partType);
            if (entry == null)
            {
                entry = new AvatarPartKeyTableSO.PartEntry { PartType = partType };
                _target.Entries.Add(entry);
            }

            // C# 8.0부터 생긴 문법. 값을 직접 반환하는 식(expression)
            // 파츠 리스트 + 썸네일 리스트를 같은 genderRoot 기준으로 함께 선택
            var (list, thumbList, subTypeList) = genderRoot switch
            {
                "Male_Parts" => (entry.MaleParts, entry.MaleThumbnailNames, entry.MalePartSubTypes),
                "Female_Parts" => (entry.FemaleParts, entry.FemaleThumbnailNames, entry.FemalePartSubTypes),
                "All_Gender_Parts" => (entry.AllGenderParts, entry.AllGenderThumbnailNames, entry.AllGenderPartSubTypes),
                _ => ((List<AssetReferenceGameObject>)null, (List<string>)null, (List<string>)null)
            };

            // list는 entry.MaleParts 등 기존 리스트에 대한 참조(레퍼런스). entry.MaleParts 자체를 비우는 것
            // Populator를 한 번 실행한 뒤 나중에 프리팹을 추가하고 다시 실행하면 기존 리스트에 새 항목을 또 AddRange로 추가하면 중복이 생김
            // 매번 실행할 때 "일단 비우고 → 지금 스캔한 결과로 새로 채운다"는 방식으로, 여러 번 실행해도 항상 같은 결과가 나오게(idempotent)
            list?.Clear();
            list?.AddRange(refs);
            thumbList?.Clear(); 
            thumbList?.AddRange(thumbs);
            subTypeList?.Clear(); 
            subTypeList?.AddRange(subTypes);
        }
    }
}