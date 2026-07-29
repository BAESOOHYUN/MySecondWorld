using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Combat.Monster;

namespace MySecondWorld.Editor
{
    public static class MonsterStatsBakeHelper
    {
        private const string CsvPath = "Assets/09.Datas/monster-draft.csv";
        private const string OutputFolder = "Assets/06.ScriptableObject/Monster";
        private enum BakeResult { Created, Updated, Skipped }

        [MenuItem("Tools/Combat/Bake Monster Stats From CSV")]
        private static void BakeFromCsv()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"[MonsterStatsBakeHelper] CSV 없음: {CsvPath}");
                return;
            }

            var lines = File.ReadAllLines(CsvPath);
            var header = ItemAssetBakeHelper.BuildHeaderIndex(lines[0]);
            int columnCount = header.Count;
            int created = 0, updated = 0, skipped = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = ItemAssetBakeHelper.ParseCsvLine(line);
                    if (cols.Length < columnCount)
                    {
                        Debug.LogWarning($"[MonsterStatsBakeHelper] Row {i + 1}: 컬럼 수 부족 ({cols.Length}) — 건너뜀");
                        skipped++;
                        continue;
                    }

                    string monsterId = cols[header["MonsterId"]];
                    string monsterType = cols[header["MonsterType"]];

                    var result = ApplyRowToAsset(monsterId, monsterType, cols, header, i + 1);
                    switch (result)
                    {
                        case BakeResult.Created: created++; break;
                        case BakeResult.Updated: updated++; break;
                        default: skipped++; break;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[MonsterStatsBakeHelper] 생성 {created}개 / 갱신 {updated}개 / 건너뜀 {skipped}개 → {OutputFolder}");
        }

        private static BakeResult ApplyRowToAsset(
            string monsterId, string monsterType, string[] cols,
            Dictionary<string, int> header, int rowNumber)
        {
            if (monsterType != "Melee" && monsterType != "Ranged")
            {
                Debug.LogWarning($"[MonsterStatsBakeHelper] Row {rowNumber}: 알 수 없는 MonsterType '{monsterType}' — 건너뜀");
                return BakeResult.Skipped;
            }
            string assetPath = $"{OutputFolder}/{monsterId}.asset";
            bool exists = File.Exists(assetPath);
            MonsterStatsSO stats;

            if (exists)
            {
                stats = AssetDatabase.LoadAssetAtPath<MonsterStatsSO>(assetPath);
                if (stats == null)
                {
                    Debug.LogWarning($"[MonsterStatsBakeHelper] Row {rowNumber}: 기존 에셋 로드 실패 — {assetPath}");
                    return BakeResult.Skipped;
                }

                // CSV의 MonsterType이 기존 에셋의 실제 C# 타입과 어긋나면(Melee↔Ranged 전환 시도)
                // SerializedObject로 없는 필드를 쓰려다 조용히 무시되는 사고를 막기 위해 먼저 걸러낸다.
                bool isRangedAsset = stats is RangedMonsterStatsSO;
                bool isRangedCsv = monsterType == "Ranged";
                if (isRangedAsset != isRangedCsv)
                {
                    Debug.LogWarning(
                        $"[MonsterStatsBakeHelper] Row {rowNumber}: '{monsterId}' 타입 불일치 " +
                        $"(기존 에셋={(isRangedAsset ? "Ranged" : "Melee")}, CSV={monsterType}) — 건너뜀");
                    return BakeResult.Skipped;
                }
            }
            else
            {
                stats = monsterType == "Ranged"
                    ? ScriptableObject.CreateInstance<RangedMonsterStatsSO>()
                    : ScriptableObject.CreateInstance<MonsterStatsSO>();
            }

            var so = new SerializedObject(stats);
            WriteFloatField(so, "_maxHP", cols[header["MaxHP"]]);
            WriteFloatField(so, "_attack", cols[header["Attack"]]);
            WriteFloatField(so, "_attackRange", cols[header["AttackRange"]]);
            WriteFloatField(so, "_attackCooldown", cols[header["AttackCooldown"]]);
            WriteFloatField(so, "_detectRange", cols[header["DetectRange"]]);
            WriteFloatField(so, "_moveSpeed", cols[header["MoveSpeed"]]);

            if (monsterType == "Ranged")
            {
                WriteFloatField(so, "_retreatDistance", cols[header["RetreatDistance"]]);
                WriteFloatField(so, "_projectileSpeed", cols[header["ProjectileSpeed"]]);
            }

            // WriteFloatField() 호출로 _maxHP, _attack, _attackRange ... 를 전부 버퍼에만 쌓아뒀다가
            // 버퍼에 쌓인 변경사항들을 실제 오브젝트(stats)에 한꺼번에 반영(commit)
            so.ApplyModifiedProperties();

            if (exists)
            {
                EditorUtility.SetDirty(stats);
                return BakeResult.Updated;
            }

            AssetDatabase.CreateAsset(stats, assetPath);
            return BakeResult.Created;
        }

        private static void WriteFloatField(SerializedObject so, string fieldName, string rawValue)
        {
            if (!float.TryParse(rawValue, out var value))
            {
                Debug.LogWarning($"[MonsterStatsBakeHelper] 필드 '{fieldName}' 값 파싱 실패: '{rawValue}'");
                return;
            }

            var property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[MonsterStatsBakeHelper] 필드 '{fieldName}' 못 찾음 — SO 필드명 확인 필요");
                return;
            }

            property.floatValue = value;
        }
    }
}