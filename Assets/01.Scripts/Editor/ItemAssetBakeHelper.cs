using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Inventory;
using MySecondWorld.Inventory.Items;
using Cleverous.VaultInventory;
using UnityEngine.AddressableAssets;

namespace MySecondWorld.Editor
{
    public class ItemAssetBakeHelper : EditorWindow
    {
        private const string AssetOutputFolder = "Assets/06.ScriptableObject/Inventory";
        private const string RestrictionSearchFolder = "Assets/06.ScriptableObject/Inventory";

        private static readonly Dictionary<string, string> PackageIconRoots = new()
        {
            // { key, value } 쌍을 나열하면 각각의 쌍이 .Add(key, value) 호출로 자동 변환
            { "Dungeon", "Assets/07.Textures/DungeonWeaponThumbnails" },
            { "FantasyHeroCharacters", "Assets/07.Textures/FantasyHeroCharactersWeaponThumbnails" },
            { "FantasyKingdom", "Assets/07.Textures/FantasyKingdomWeaponThumbnails" },
            { "DungeonItems", "Assets/07.Textures/DungeonItemsThumbnails" },
            { "FantasyKingdomItems", "Assets/07.Textures/FantasyKingdomItemsThumbnails" },
        };

        private static readonly Dictionary<string, string> PackagePrefabRoots = new()
        {
            { "Dungeon", "Assets/Models/PolygonDungeon/Prefabs/Weapons" },
            { "FantasyHeroCharacters", "Assets/Models/PolygonFantasyHeroCharacters/Prefabs/Weapons" },
            { "FantasyKingdom", "Assets/Models/PolygonFantasyKingdom/Prefabs/Weapons/Weapons" },
            { "DungeonItems", "Assets/Models/PolygonDungeon/Prefabs/Items" },
            { "FantasyKingdomItems", "Assets/Models/PolygonFantasyKingdom/Prefabs/Items" },
        };

        private static readonly Dictionary<string, string> PackageModelRoots = new()
        {
            { "DungeonItems", "Assets/Models/PolygonDungeon/Models" },
            { "FantasyKingdomItems", "Assets/Models/PolygonFantasyKingdom/Models" },
        };

        private static readonly Dictionary<string, string> ItemTypeSubfolders = new()
        {
            { "Equipment", "Equipment" },
            { "Consumable", "Consumables" },
            { "Generic", "Generic" },
        };

        private string _csvPath = "";
        private bool _useRemotePrefab = true;
        private bool _overwrite = false;

        [MenuItem("Tools/Inventory/Generate Weapon Item from CSV")]
        private static void Open() => GetWindow<ItemAssetBakeHelper>("Item Baker");


        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = 220f;
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

            _useRemotePrefab = EditorGUILayout.Toggle("원격(RemoteArtPrefab) 사용", _useRemotePrefab);
            EditorGUILayout.HelpBox(
                "ON: RemoteArtPrefab만 채우고 ArtPrefab은 비워둡니다. " +
                "무기처럼 Addressable로 원격 배포할 아이템을 구울 때 켜세요." +
                "\r\nOFF: ArtPrefab만 채우고 RemoteArtPrefab은 비워둡니다. " +
                "포션/재료처럼 로컬 프리팹을 그대로 쓰는 아이템을 구울 때 끄세요.",
                MessageType.Info);

            _overwrite = EditorGUILayout.Toggle("기존 에셋 덮어쓰기", _overwrite);
            EditorGUILayout.HelpBox(
                "OFF(기본): 이미 존재하는 에셋은 건너뜁니다 — 수동으로 고친 값이 보존됩니다." +
                "\r\nON: 이미 존재하는 에셋도 CSV 값으로 필드를 다시 채웁니다(같은 GUID 유지). " +
                "CSV 컬럼이 추가/변경돼 기존 에셋 전체를 일괄 갱신해야 할 때만 켜세요.",
                MessageType.Warning);

            // 경로가 있으면 버튼 생성
            GUI.enabled = !string.IsNullOrEmpty(_csvPath) && File.Exists(_csvPath);
            if (GUILayout.Button("아이템 생성"))
                GenerateFromCsv(_csvPath, _useRemotePrefab, _overwrite);
            if (GUILayout.Button("누락 프리팹 모델에서 생성"))
                GenerateMissingPrefabsFromModels(_csvPath);
            GUI.enabled = true;
        }

        private static void GenerateFromCsv(string csvPath, bool useRemotePrefab, bool overwrite)
        {
            var lines = File.ReadAllLines(csvPath);
            var header = BuildHeaderIndex(lines[0]);
            int columnCount = header.Count;
            var iconCache = new Dictionary<string, Sprite>();
            var prefabCache = new Dictionary<string, GameObject>();
            var prefabRefCache = new Dictionary<string, AssetReferenceGameObject>();
            var restrictionCache = new Dictionary<string, SlotRestriction>();

            int created = 0, updated = 0, skipped = 0, missingIcon = 0, missingArtPrefab = 0, missingRemotePrefab = 0;

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

                    var cols = line.Split(new[] { ',' }, columnCount);
                    // CSV 한 줄이 예상보다 컬럼이 적게 파싱됐을 때 조용히 오류 데이터를 만들지 않고 걸러내는 방어 코드
                    if (cols.Length < columnCount)
                    {
                        // Row {i + 1} 헤더가 1번 줄을 이미 차지하고 있음. 엑셀에서 보이는 실제 행 번호
                        Debug.LogWarning($"[ItemAssetBakeHelper] Row {i + 1}: 컬럼 수 부족 ({cols.Length}) — 건너뜀");
                        skipped++;
                        continue;
                    }

                    string prefabName = cols[header["PrefabName"]];
                    string package = cols[header["Package"]];
                    string koreanName = cols[header["KoreanName"]];
                    string itemType = cols[header["ItemType"]];
                    bool hasPrefab = !header.TryGetValue("HasPrefab", out var hasPrefabIdx)
                        || !string.Equals(cols[hasPrefabIdx].Trim(), "FALSE", StringComparison.OrdinalIgnoreCase);
                    string restrictionTitle = cols[header["RestrictionTitle"]];
                    string tags = cols[header["Tags"]];
                    string description = cols[header["Description"]];

                    if (!ItemTypeSubfolders.TryGetValue(itemType, out var subfolder))
                    {
                        Debug.LogWarning($"[ItemAssetBakeHelper] Row {i + 1}: 알 수 없는 ItemType '{itemType}' — 건너뜀");
                        skipped++;
                        continue;
                    }

                    // Item SO 경로 찾기
                    var folder = $"{AssetOutputFolder}/{subfolder}";
                    var assetPath = $"{folder}/{package}_{prefabName}.asset";
                    bool exists = File.Exists(assetPath);
                    if (exists && !overwrite)
                    {
                        skipped++;
                        continue;
                    }

                    RootItem item;
                    if (exists)
                    {
                        // 기존 에셋을 그대로 로드해서 필드만 갱신 — GUID(참조) 유지
                        item = AssetDatabase.LoadAssetAtPath<RootItem>(assetPath);
                        if (item == null)
                        {
                            Debug.LogWarning($"[ItemAssetBakeHelper] Row {i + 1}: 기존 에셋 로드 실패 — {assetPath}");
                            skipped++;
                            continue;
                        }
                    }
                    else
                    {
                        // 기존 에셋이 없으면 새로 생성
                        item = itemType switch
                        {
                            "Equipment" => ScriptableObject.CreateInstance<EquipmentItem>(),
                            "Consumable" => ScriptableObject.CreateInstance<ConsumableItem>(),
                            "Generic" => ScriptableObject.CreateInstance<GenericItem>(),
                            _ => null,
                        };
                    }

                    if (item == null) 
                    {
                        skipped++;
                        continue; 
                    }
                    
                    // 해당 Item SO에 csv 데이터 대입
                    item.Title = koreanName;
                    item.Description = description;
                    item.Restriction = string.IsNullOrWhiteSpace(restrictionTitle)
                        ? null
                        : FindSlotRestriction(restrictionTitle, restrictionCache);
                    item.Tags = ParseTags(tags);
                    
                    var uiIcon = FindIcon(package, prefabName, iconCache);
                    if (uiIcon != null) 
                        item.UiIcon = uiIcon;
                    else                
                        missingIcon++;

                    if (!hasPrefab)
                    {
                        // Currency/Document 등 원래 3D 모델이 없는 아이템 — 조회 자체를 건너뜀, 카운트 안 함
                        item.ArtPrefab = null;
                        item.RemoteArtPrefab = null;
                    }
                    else if (useRemotePrefab)
                    {
                        var remoteArtPrefab = FindPrefabReference(package, prefabName, prefabRefCache);
                        if (remoteArtPrefab != null)
                            item.RemoteArtPrefab = remoteArtPrefab;
                        else
                            missingRemotePrefab++;

                        item.ArtPrefab = null;
                    }
                    else
                    {
                        var artPrefab = FindPrefab(package, prefabName, prefabCache);
                        if (artPrefab != null)
                            item.ArtPrefab = artPrefab;
                        else
                            missingArtPrefab++;

                        item.RemoteArtPrefab = null;
                    }

                    item.MaxStackSize = itemType == "Equipment" ? 1 : 99;

                    switch (item)
                    {
                        case EquipmentItem equipment:
                            {
                                equipment.Stats = ParseStats(cols, header);
                                break;
                            }
                        case ConsumableItem consumable:
                            {
                                consumable.BuffStats = ParseStats(cols, header);
                                if (float.TryParse(cols[header["HealAmount"]], out var heal))
                                    consumable.HealAmount = heal;
                                if (float.TryParse(cols[header["BuffDuration"]], out var duration))
                                    consumable.BuffDuration = duration;
                                break;
                            }
                    }

                    if (exists) // 덮어 씌우기
                    {
                        EditorUtility.SetDirty(item);
                        updated++;
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(item, assetPath);
                        created++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"생성 {created}개 / 갱신 {updated}개 / 건너뜀 {skipped}개 / 아이콘 없음 {missingIcon}개 / " +
                $"로컬 프리팹 없음 {missingArtPrefab}개 / 원격 프리팹 없음 {missingRemotePrefab}개 → {AssetOutputFolder}");
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

        private static List<StatModifier> ParseStats(string[] cols, Dictionary<string, int> header)
        {
            var stats = new List<StatModifier>();
            AddStatIfPresent(stats, cols[header["Stat1_Type"]], cols[header["Stat1_Mod"]], cols[header["Stat1_Value"]]);
            AddStatIfPresent(stats, cols[header["Stat2_Type"]], cols[header["Stat2_Mod"]], cols[header["Stat2_Value"]]);
            AddStatIfPresent(stats, cols[header["Stat3_Type"]], cols[header["Stat3_Mod"]], cols[header["Stat3_Value"]]);

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

        private static AssetReferenceGameObject FindPrefabReference(string package, string prefabName, Dictionary<string, AssetReferenceGameObject> cache)
        {
            if (!PackagePrefabRoots.TryGetValue(package, out var root)) return null;
            string path = $"{root}/{prefabName}.prefab";

            if (cache.TryGetValue(path, out var cached)) return cached;
            var guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[ItemAssetBakeHelper] 프리팹 없음: {path}");
                cache[path] = null;
                return null;
            }

            var prefab = new AssetReferenceGameObject(guid);
            cache[path] = prefab;
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

        private static void GenerateMissingPrefabsFromModels(string csvPath)
        {
            var lines = File.ReadAllLines(csvPath);
            var header = BuildHeaderIndex(lines[0]);
            int columnCount = header.Count;
            var modelIndexCache = new Dictionary<string, Dictionary<string, string>>(); // modelRoot → {파일명: fbx경로}
            int created = 0, alreadyExists = 0, noModel = 0, skipped = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = line.Split(new[] { ',' }, columnCount);
                    if (cols.Length < columnCount)
                    {
                        Debug.LogWarning($"[ItemAssetBakeHelper] Row {i + 1}: 컬럼 수 부족 ({cols.Length}) — 건너뜀");
                        skipped++;
                        continue;
                    }

                    string prefabName = cols[header["PrefabName"]];
                    string package = cols[header["Package"]];
                    bool hasPrefab = !header.TryGetValue("HasPrefab", out var hasPrefabIdx)
                        || !string.Equals(cols[hasPrefabIdx].Trim(), "FALSE", StringComparison.OrdinalIgnoreCase);

                    if (!hasPrefab) { skipped++; continue; }
                    if (!PackagePrefabRoots.TryGetValue(package, out var prefabRoot)) continue;
                    if (!PackageModelRoots.TryGetValue(package, out var modelRoot)) continue;

                    var prefabPath = $"{prefabRoot}/{prefabName}.prefab";
                    if (File.Exists(prefabPath))
                    {
                        alreadyExists++;
                        continue;
                    }

                    var modelIndex = GetOrBuildModelIndex(modelRoot, modelIndexCache);
                    if (!modelIndex.TryGetValue(prefabName, out var fbxPath))
                    {
                        Debug.LogWarning($"[ItemAssetBakeHelper] 모델 자체 없음: {package}/{prefabName}");
                        noModel++;
                        continue;
                    }

                    var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    DestroyImmediate(instance);
                    created++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[ItemAssetBakeHelper] 프리팹 생성 {created}개 / 이미 있음 {alreadyExists}개 / " +
                $"모델 자체 없음 {noModel}개 / 건너뜀 {skipped}개 (경로: {csvPath})");
        }

        private static Dictionary<string, int> BuildHeaderIndex(string headerLine)
        {
            // 컬럼 이름 → 위치. CSV에 컬럼이 추가/이동돼도 cols[고정숫자] 하드코딩 없이 이름으로 찾도록 함
            var headers = headerLine.Split(',');
            var index = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
                index[headers[i].Trim()] = i;
            return index;   // 키(key) = 헤더의 컬럼 이름 문자열 ("PrefabName", "Package", "HasPrefab" ...)
                            // 값(value) = 그 컬럼이 몇 번째 위치(인덱스)인지(0, 1, ... 21)
                            // header["Package"]가 1이라는 숫자로 바뀌고, 결국 cols[1]을 읽는 것과 완전히 동일한 결과가 나온다.
        }

        private static Dictionary<string, string> GetOrBuildModelIndex(string modelRoot, Dictionary<string, Dictionary<string, string>> cache)
        {
            if (cache.TryGetValue(modelRoot, out var index)) return index;

            index = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { modelRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                index[Path.GetFileNameWithoutExtension(path)] = path;
            }

            cache[modelRoot] = index;
            return index;
        }
    }
}