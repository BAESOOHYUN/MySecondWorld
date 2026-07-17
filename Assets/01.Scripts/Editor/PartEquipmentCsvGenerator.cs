using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using MySecondWorld.Customization;

namespace MySecondWorld.Editor
{
    public class PartEquipmentCsvGenerator : EditorWindow
    {
        private const string CsvOutputPath = "Assets/09.Datas/PartEquipmentItems.csv";
        private AvatarPartKeyTableSO _target;

        [MenuItem("Tools/Inventory/Generate Part Equipment CSV")]
        private static void ShowWindow()
        {
            GetWindow<PartEquipmentCsvGenerator>("Part Equipment CSV");
        }

        private void OnGUI()
        {
            _target = (AvatarPartKeyTableSO)EditorGUILayout.ObjectField(
                "TargetSO", _target, typeof(AvatarPartKeyTableSO), false);

            // PartType/Gender/Index/SourcePrefabName/SubType/ThumbnailName
            // 전부 이미 AvatarPartKeyTableSO.Entries에 존재하는 데이터이므로
            // 이미 있는 SO 데이터를 코드가 읽어서 CSV로 뽑아내는 게 효율적
            if (GUILayout.Button("CSV 생성") && _target != null)
            {
                GenerateCsv(_target);
            }
        }

        private static void GenerateCsv(AvatarPartKeyTableSO table)
        {
            var rows = new List<string>
            {
                "PartType,Gender,Index,SourcePrefabName,SubType,ThumbnailName,GeneratedTitle,RestrictionTitle," +
                "Description,Stat1_Type,Stat1_Mod,Stat1_Value,Stat2_Type,Stat2_Mod,Stat2_Value,Stat3_Type,Stat3_Mod,Stat3_Value"
            };
            var titleSet = new HashSet<string>();
            int duplicateCount = 0;

            foreach (var partType in PartEquipmentCatalog.GroupBPartTypes)
            {
                foreach (var gender in new[] { Gender.Male, Gender.Female })
                {
                    AppendRows(table, partType, gender, isGroupC: false, rows, titleSet, ref duplicateCount);
                }
            }

            foreach (var partType in PartEquipmentCatalog.GroupCPartTypes)
            {
                // AllGenderParts만 쓰는 그룹이라 Male/Female 어느 쪽으로 조회해도 결과가 같음 — 중복 방지 위해 한 번만 호출
                AppendRows(table, partType, Gender.Male, isGroupC: true, rows, titleSet, ref duplicateCount);
            }

            File.WriteAllLines(CsvOutputPath, rows, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();

            if (duplicateCount > 0)
                Debug.LogWarning($"[PartEquipmentCsvGenerator] 중복 GeneratedTitle {duplicateCount}건 발견 — CSV 확인 필요");

            Debug.Log($"[PartEquipmentCsvGenerator] {rows.Count - 1}개 행 생성 완료 → {CsvOutputPath}");
        }

        private static void AppendRows(AvatarPartKeyTableSO table, PartType partType, Gender gender, bool isGroupC,
            List<string> rows, HashSet<string> titleSet, ref int duplicateCount)
        {
            var candidates = table.GetCandidates(partType, gender);
            var thumbnails = table.GetThumbnailNames(partType, gender);
            string genderColumn = isGroupC ? "All" : gender.ToString();

            // 인덱스 0은 그룹 B(맨몸)/그룹 C(장착 안함) 둘 다 무료 슬롯이라 CSV 대상에서 제외
            for (int index = 1; index < candidates.Count; index++)
            {
                string subType = table.GetSubType(partType, gender, index);
                string thumbnailName = index < thumbnails.Count ? thumbnails[index] : string.Empty;
                string sourcePrefabName = GetPrefabName(candidates[index]);
                string title = PartEquipmentCatalog.BuildTitle(partType, genderColumn, subType, index, isGroupC);

                // 중복이 나오면 안 되는 구조라(그룹B는 성별 마커, 그룹C는 PartType으로 항상 갈림)
                // — 걸리면 자동수정 대신 BuildTitle/번역사전 버그를 의심하라는 신호로만 카운트
                if (!titleSet.Add(title))
                    duplicateCount++;

                rows.Add(
                    ToCsvRow(partType.ToString(), genderColumn, index.ToString(),
                    sourcePrefabName, subType, thumbnailName, title, partType.ToString(),
                    "", "", "", "", "", "", "", "", "", ""));
            }
        }

        private static string GetPrefabName(AssetReferenceGameObject reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
                return string.Empty;

            string path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
            return Path.GetFileNameWithoutExtension(path);
        }

        // ───────── 헬퍼 클래스 ──────────────

        private static string ToCsvRow(params string[] fields)
        {
            return string.Join(",", Array.ConvertAll(fields, EscapeCsvField));
        }

        private static string EscapeCsvField(string field)
        {
            field ??= string.Empty;
            bool needsQuoting = field.Contains(',') || field.Contains('"') || field.Contains('\n');
            if (!needsQuoting)
                return field;
            // CSV 표준: 콤마/따옴표/개행 있으면 통째로 따옴표로 감싸고, 내부 따옴표는 두 번 써서 이스케이프
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
    }
}