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
        private const string SourceRoot = "Assets/Addressables/CharacterParts";
        private const string ThumbnailRoot = "Assets/07.Textures/CharacterPartThumbnails";

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
                    if (!TryResolvePartType(folderName, out var partType))
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