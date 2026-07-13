using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Inventory;
using MySecondWorld.Inventory.Items;
using Cleverous.VaultInventory;

namespace MySecondWorld.Editor
{
    public class ItemAssetBakeHelper : EditorWindow
    {
        private const string AssetOutputFolder = "Assets/06.ScriptableObject/Inventory";
        private const string RestrictionSearchFolder = "Assets/06.ScriptableObject/Inventory";
        private const int ColumnCount = 20; // Note에 콤마가 있어도 마지막 칸으로 몰아넣기 위해 Split 개수 고정

        private static readonly Dictionary<string, string> PackageIconRoots = new()
        {
            // { key, value } 쌍을 나열하면 각각의 쌍이 .Add(key, value) 호출로 자동 변환
            { "Dungeon", "Assets/07.Textures/DungeonWeaponThumbnails" },
            { "FantasyHeroCharacters", "Assets/07.Textures/FantasyHeroCharactersWeaponThumbnails" },
            { "FantasyKingdom", "Assets/07.Textures/FantasyKingdomWeaponThumbnails" },
        };

        private static readonly Dictionary<string, string> PackagePrefabRoots = new()
        {
            { "Dungeon", "Assets/Addressables/DungeonWeapons" },
            { "FantasyHeroCharacters", "Assets/Addressables/FantasyHeroCharactersWeapons" },
            { "FantasyKingdom", "Assets/Addressables/FantasyKingdomWeapons" },
        };

        private static readonly Dictionary<string, string> ItemTypeSubfolders = new()
        {
            { "Equipment", "Equipment" },
            { "Consumable", "Consumables" },
            { "Generic", "Generic" },
        };

        private string _csvPath = "";

        [MenuItem("Tools/Inventory/Generate Weapon Item from CSV")]
        private static void Open() => GetWindow<ItemAssetBakeHelper>("Item Baker");
        private void OnGUI()
        {
            GUILayout.Label("CSV 경로", EditorStyles.boldLabel);

            // 윈도우로 csv 경로 탐색
            EditorGUILayout.BeginHorizontal();
            _csvPath = EditorGUILayout.TextField(_csvPath);
            if (GUILayout.Button("찾기", GUILayout.Width(60)))
            {
                var picked = EditorUtility.OpenFilePanel("아이템 CSV 선택", "Assets/09.Datas", "csv");
                if (!string.IsNullOrEmpty(picked))
                {
                    _csvPath = "Assets" + picked.Substring(Application.dataPath.Length);
                    Debug.Log($"[ItemAssetBakeHelper] _csvPath = {_csvPath}");
                }
            }
            EditorGUILayout.EndHorizontal();

            // 경로가 있으면 버튼 생성
            GUI.enabled = !string.IsNullOrEmpty(_csvPath) && File.Exists(_csvPath);
            if (GUILayout.Button("아이템 생성"))
                GenerateFromCsv(_csvPath);
            GUI.enabled = true;
        }

        private static void GenerateFromCsv(string csvPath)
        {
            var lines = File.ReadAllLines(csvPath);
            var iconCache = new Dictionary<string, Sprite>();
            var prefabCache = new Dictionary<string, GameObject>();
            var restrictionCache = new Dictionary<string, SlotRestriction>();

            int created = 0, skipped = 0, missingIcon = 0, missingPrefab = 0;

            // StartAssetEditing 배치 중에는 방금 만든 폴더가 IsValidFolder에 바로 안 잡히는 문제가 있어서,
            // 필요한 폴더를 배치 시작 전에 전부 미리 만들어둔다.
            foreach (var subfolder in ItemTypeSubfolders.Values)
                CreateFolderRecursive($"{AssetOutputFolder}/{subfolder}");

            AssetDatabase.StartAssetEditing();
            try 
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = line.Split(new[] { ',' }, ColumnCount);
                    // CSV 한 줄이 예상보다 컬럼이 적게 파싱됐을 때 조용히 오류 데이터를 만들지 않고 걸러내는 방어 코드
                    if (cols.Length < ColumnCount)
                    {
                        // Row {i + 1} 헤더가 1번 줄을 이미 차지하고 있음. 엑셀에서 보이는 실제 행 번호
                        Debug.LogWarning($"[ItemAssetBakeHelper] Row {i + 1}: 컬럼 수 부족 ({cols.Length}) — 건너뜀");
                        skipped++;
                        continue;
                    }

                    string prefabName = cols[0];
                    string package = cols[1];
                    string koreanName = cols[2];
                    string itemType = cols[3];
                    string restrictionTitle = cols[6];
                    string tags = cols[7];

                    if (!ItemTypeSubfolders.TryGetValue(itemType, out var subfolder))
                    {
                        Debug.LogWarning($"[ItemAssetBakeHelper] Row {i + 1}: 알 수 없는 ItemType '{itemType}' — 건너뜀");
                        skipped++;
                        continue;
                    }

                    // Item SO 경로 찾기
                    var folder = $"{AssetOutputFolder}/{subfolder}";
                    var assetPath = $"{folder}/{package}_{prefabName}.asset";
                    if (File.Exists(assetPath))
                    {
                        skipped++;
                        continue;
                    }

                    RootItem item = itemType switch
                    {
                        "Equipment" => ScriptableObject.CreateInstance<EquipmentItem>(),
                        "Consumable" => ScriptableObject.CreateInstance<ConsumableItem>(),
                        "Generic" => ScriptableObject.CreateInstance<GenericItem>(),
                        _ => null,
                    };

                    if (item == null) 
                    {
                        skipped++;
                        continue; 
                    }
                    
                    // 해당 Item SO에 csv 데이터 대입
                    item.Title = koreanName;
                    item.Restriction = string.IsNullOrWhiteSpace(restrictionTitle)
                        ? null
                        : FindSlotRestriction(restrictionTitle, restrictionCache);
                    item.Tags = ParseTags(tags);
                    
                    var uiIcon = FindIcon(package, prefabName, iconCache);
                    if (uiIcon != null) 
                        item.UiIcon = uiIcon;
                    else                
                        missingIcon++;

                    var artPrefab = FindPrefab(package, prefabName, prefabCache);
                    if (artPrefab != null)
                        item.ArtPrefab = artPrefab;
                    else
                        missingPrefab++;
                    item.MaxStackSize = itemType == "Equipment" ? 1 : 99;

                    switch (item)
                    {
                        case EquipmentItem equipment:
                            {
                                equipment.Stats = ParseStats(cols);
                                break;
                            }
                        case ConsumableItem consumable:
                            {
                                consumable.BuffStats = ParseStats(cols);
                                if (float.TryParse(cols[17], out var heal))
                                    consumable.HealAmount = heal;
                                if (float.TryParse(cols[18], out var duration))
                                    consumable.BuffDuration = duration;
                                break;
                            }
                    }

                    AssetDatabase.CreateAsset(item, assetPath);
                    created++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"생성 {created}개 / 건너뜀(기존 존재) {skipped}개 / 아이콘 없음 {missingIcon}개 / 프리팹 없음 {missingPrefab}개 → {AssetOutputFolder}");
        }

        private static ItemCategory ParseTags(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return ItemCategory.None;
            var result = ItemCategory.None;

            foreach (var token in raw.Split('|'))
            {
                if (Enum.TryParse<ItemCategory>(token.Trim(), out var flag))
                    result |= flag; // 여러 플래그를 하나의 변수에 누적해서 켜 넣는 전형적인 Flags enum 조합 패턴
                //result = None        // 0000000
                //token "Equipment" → flag = 0000001
                //result |= flag  →  result = 0000000 | 0000001 = 0000001
                //token "Weapon" → flag = 0000010
                //result |= flag  →  result = 0000001 | 0000010 = 0000011   // Equipment + Weapon 둘 다 켜짐
            }

            return result;
        }

        private static List<StatModifier> ParseStats(string[] cols)
        {
            var stats = new List<StatModifier>();
            AddStatIfPresent(stats, cols[8], cols[9], cols[10]);    // Stat1_Type	Stat1_Mod	Stat1_Value
                                                                    // Attack	    Flat	    15
            AddStatIfPresent(stats, cols[11], cols[12], cols[13]);  // Stat2_Type	Stat2_Mod	Stat2_Value
                                                                    // HpRegen	    Flat    	3
            AddStatIfPresent(stats, cols[14], cols[15], cols[16]);  // Stat3_Type	Stat3_Mod	Stat3_Value
                                                                    // MagicAttack	Flat	    5

            return stats;
        }

        private static void AddStatIfPresent(List<StatModifier> stats, string typeRaw, string modRaw, string valueRaw)
        {
            // 이 패턴이 if/else if/else 중첩보다 선호되는 이유는 조건이 여러 개 겹칠 때 들여쓰기가 계속 깊어지는 걸 피할 수 있어서다.
            if (string.IsNullOrWhiteSpace(typeRaw)) return; // 그 Stat_Type 슬롯 안 쓰면 종료
            if (!Enum.TryParse<StatType>(typeRaw.Trim(), out var statType)) return; // Stat_Type칸에 값은 있는데, 그 값이 StatType enum에 정의된 이름(Attack, MagicAttack 등)과 일치하지 않으면(오타 등) 종료.
            if (!Enum.TryParse<ModifierType>(modRaw.Trim(), out var modType)) return; //  Stat_Mod 칸이 ModifierType(Flat, Percent 등)과 안 맞으면 종료.
            if (!float.TryParse(valueRaw, out var value)) return; // Stat_Value 칸이 숫자로 안 읽히면(빈칸, 오타 등) 종료

            stats.Add(new StatModifier { Type = statType, ModifierType = modType, Value = value });
        }

        private static Sprite FindIcon(string package, string prefabName, Dictionary<string, Sprite> cache)
        {
            if (!PackageIconRoots.TryGetValue(package, out var root)) return null;
            string path = $"{root}/{prefabName}.png";

            // 다음에 같은 경로가 또 요청되면 재조회 안 하고 바로 꺼내 쓰기 위해 저장
            // 같은 아이콘 경로를 여러 행이 참조할 때 매번 AssetDatabase.LoadAssetAtPath를 반복 호출하지 않기 위한 메모이제이션(memoization)
            if (cache.TryGetValue(path, out var cached)) return cached; //여기서 캐시를 읽음

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            cache[path] = sprite; //여기서 캐시를 저장

            if (sprite == null)
                Debug.LogWarning($"[ItemAssetBakeHelper] 아이콘 없음: {path}");
            return sprite;
        }

        private static GameObject FindPrefab(string package, string prefabName, Dictionary<string, GameObject> cache)
        {
            if (!PackagePrefabRoots.TryGetValue(package, out var root)) return null;
            string path = $"{root}/{prefabName}.prefab";

            if (cache.TryGetValue(path, out var cached)) return cached;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            cache[path] = prefab;

            if (prefab == null)
                Debug.LogWarning($"[ItemAssetBakeHelper] 프리팹 없음: {path}");
            return prefab;
        }

        private static SlotRestriction FindSlotRestriction(string title, Dictionary<string, SlotRestriction> cache)
        {
            if (cache.TryGetValue(title, out var cached)) return cached;

            var guids = AssetDatabase.FindAssets("t:SlotRestriction", new[] { RestrictionSearchFolder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);       // GUID → 파일 경로
                var restriction = AssetDatabase.LoadAssetAtPath<SlotRestriction>(path); // 파일 경로 → 실제 오브젝트 로드후 <SlotRestriction> 타입으로 캐스팅
                if (restriction != null && restriction.Title == title)
                {
                    cache[title] = restriction;
                    return restriction;         // Title 문자열로 최종 매칭
                }
            }
            Debug.LogWarning($"[ItemAssetBakeHelper] SlotRestriction '{title}' 못 찾음");
            cache[title] = null;
            return null;
        }

        private static void CreateFolderRecursive(string fullPath)
        {
            if (AssetDatabase.IsValidFolder(fullPath)) return;

            var parts = fullPath.Split('/'); // parts = ["Assets", "06.ScriptableObject", "Inventory"]
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]); // "Equipment"폴더 생성
                current = next;
            }
        }
    };

}