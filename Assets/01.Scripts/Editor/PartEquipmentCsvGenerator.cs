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
        public const string CsvOutputPath = "Assets/09.Datas/PartEquipmentItems.csv";
        private AvatarPartKeyTableSO _target;

        [MenuItem("Tools/Inventory/Generate Part Equipment CSV")]
        private static void ShowWindow()
        {
            GetWindow<PartEquipmentCsvGenerator>("Part Equipment CSV");
        }

        private void OnGUI()
        {
            _target = EditorAssetDefaults.DrawWithDefault("TargetSO", _target, EditorAssetDefaults.AvatarPartKeyTablePath);

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
                "PartType,Gender,Index,SourcePrefabName,SubType,ThumbnailName," +
                "GeneratedTitle,FantasyName,Material," +
                "RestrictionTitle,Description," +
                "Stat1_Type,Stat1_Mod,Stat1_Value," +
                "Stat2_Type,Stat2_Mod,Stat2_Value," +
                "Stat3_Type,Stat3_Mod,Stat3_Value"
            };
            var titleSet = new HashSet<string>();
            int duplicateCount = 0;

            // Torso, ArmUpperLeft, ...
            foreach (var partType in PartEquipmentCatalog.GroupBPartTypes)
            {
                foreach (var gender in new[] { Gender.Male, Gender.Female })
                {
                    AppendRows(table, partType, gender, isGroupC: false, rows, titleSet, ref duplicateCount);
                }
            }

            // HeadCoverings, ShoulderAttachLeft, ...
            foreach (var partType in PartEquipmentCatalog.GroupCPartTypes)
            {
                // AllGenderParts만 쓰는 그룹이라 Male/Female 어느 쪽으로 조회해도 결과가 같음 — 중복 방지 위해 한 번만 호출
                AppendRows(table, partType, Gender.Male, isGroupC: true, rows, titleSet, ref duplicateCount);
            }

            // Head_No_Elements
            foreach (var gender in new[] { Gender.Male, Gender.Female })
            {
                AppendHeadLockRows(table, gender, rows, titleSet, ref duplicateCount);
            }

            File.WriteAllLines(CsvOutputPath, rows, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();

            if (duplicateCount > 0)
                Debug.LogWarning($"[PartEquipmentCsvGenerator] 중복 GeneratedTitle {duplicateCount}건 발견 — CSV 확인 필요");

            Debug.Log($"[PartEquipmentCsvGenerator] {rows.Count - 1}개 행 생성 완료 → {CsvOutputPath}");
        }

        // PartType(+Gender) 하나를 통째로 순회하면서,
        // 그 안의 모든 파츠 변형(0번, 1번, 2번...)을 CSV 행으로 만들어 rows 리스트에 쌓는 함수
        private static void AppendRows(
            AvatarPartKeyTableSO table, PartType partType, Gender gender, bool isGroupC,
            List<string> rows, HashSet<string> titleSet, ref int duplicateCount)
        {
            // 미러 쌍은 Left 1행만 출력 — Right는 베이킹 때 Left 값을 그대로 재사용
            // 이 partType이 미러 쌍의 오른쪽 7개(ArmUpperRight, HandRight, LegRight...) 중 하나인지 bool로 판정
            if (MirroredPartPairs.IsRight(partType)) return;

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
                    sourcePrefabName, subType, thumbnailName, title,
                    "", "", partType.ToString(),
                    "", "", "", "", "", "", "", "", "", ""));
            }
        }

        // Head는 그룹 A 예외 — No_Elements 서브타입만 헬멧 장착 조건으로 잠금 대상 (ItemAssetBakeHelper와 동일 필터)
        private static void AppendHeadLockRows(
            AvatarPartKeyTableSO table, Gender gender,
            List<string> rows, HashSet<string> titleSet, ref int duplicateCount)
        {
            var candidates = table.GetCandidates(PartEquipmentCatalog.HeadLockPartType, gender);
            var thumbnails = table.GetThumbnailNames(PartEquipmentCatalog.HeadLockPartType, gender);
            string genderColumn = gender.ToString();

            for (int index = 0; index < candidates.Count; index++)
            {
                // 성별 별 Head 뽑은 것중에, "No_Elements" 값을 가졌는지 판단
                string subType = table.GetSubType(PartEquipmentCatalog.HeadLockPartType, gender, index);
                if (!string.Equals(subType, PartEquipmentCatalog.HeadLockSubType, StringComparison.OrdinalIgnoreCase))
                    continue;

                string thumbnailName = index < thumbnails.Count ? thumbnails[index] : string.Empty;
                string sourcePrefabName = GetPrefabName(candidates[index]);
                string title = PartEquipmentCatalog.BuildTitle(
                    PartEquipmentCatalog.HeadLockPartType, genderColumn, subType, index, isGourpC: false);
                
                if (!titleSet.Add(title))
                    duplicateCount++;

                rows.Add(
                    ToCsvRow(PartEquipmentCatalog.HeadLockPartType.ToString(), genderColumn, index.ToString(),
                    sourcePrefabName, subType, thumbnailName, title,
                    "", "", PartEquipmentCatalog.HeadLockPartType.ToString(),
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

        public static string ToCsvRow(params string[] fields)
        {
            return string.Join(",", Array.ConvertAll(fields, EscapeCsvField));
        }

        // CSV 텍스트 조립할 때 값 자체에 콤마/따옴표/줄바꿈이 들어있으면 깨지는 문제를 막는 이스케이프 함수
        // ex)...가죽, 강철 혼합,다음컬럼...
        //  ─ 컴마 기준으로 자르면 가죽과 강철 혼합이 서로 다른 컬럼인 것처럼 잘못 쪼개져 버림
        private static string EscapeCsvField(string field)
        {
            field ??= string.Empty;
            // needsQuoting이 true면 →  "가죽, 강철 혼합"   (양쪽에 큰따옴표 추가)
            bool needsQuoting = field.Contains(',') || field.Contains('"') || field.Contains('\n');
            if (!needsQuoting)
                return field;
            // CSV 표준: 콤마/따옴표/개행 있으면 통째로 따옴표로 감싸고, 내부 따옴표는 두 번 써서 이스케이프
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
    }
}