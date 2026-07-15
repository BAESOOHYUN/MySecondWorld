using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MySecondWorld.Editor
{
    // 썸네일 PNG 베이킹 보조 도구.
    // ItemPopulate(서드파티)는 수정하지 않고, 이 도구가
    //   (1) Static 파츠(720개)/프리셋(Characters_Presets)/임의 프리팹 폴더(무기 등)를
    //       ItemPopulate.itemsToPopulate에 채워주고
    //   (2) bake 후 평면 출력된 PNG를 각각의 폴더 구조로 정렬한다.
    public class ThumbnailBakeHelper : EditorWindow
    {
        // ── 공통 상수 ──────────────────────────────────
        private const string DefaultStaticFolder = "Assets/Models/PolygonFantasyHeroCharacters/Prefabs/Characters_ModularParts_Static";
        private const string SourcePrefKey = "ThumbnailBaker.SourceFolder";
        private const string ThumbnailCategoryPrefKey = "ThumbnailBaker.ThumbnailCategoryName";
        private const string BakedIconsFolder = "Assets/Saved/Icons";   // ItemPopulate.CreateIcons 고정 출력 경로
        private const string ThumbnailRoot = "Assets/07.Textures/CharacterPartThumbnails";
        private const string PresetThumbnailRoot = "Assets/07.Textures/CharacterPresetThumbnails";
        private const string ItemThumbnailRootPrefKey = "ThumbnailBaker.ItemThumbnailRoot";
        private const string DefaultItemThumbnailRoot = "Assets/07.Textures";
        private const string ExcludeSubfolderPrefKey = "ThumbnailBaker.ExcludeSubfolder";
        private const int PresetBatchSize = 20;

        // "Chr_Head_No_Elements_Male_02_Static" → cat=Head_No_Elements, gender=Male, num=02
        // "Chr_Hair_34_Static"                  → cat=Hair, gender=(없음), num=34
        private static readonly Regex PrefabNamePattern =
            new(@"^Chr_(?<cat>.+?)(?:_(?<gender>Male|Female))?_(?<num>\d+)_Static$");
        // bake된 PNG: "Chr_Hair_34_Static_256_RRGGBBAA" (사이즈+배경색 접미사가 붙음)
        private static readonly Regex BakedPngPattern =
            new(@"^Chr_(?<cat>.+?)(?:_(?<gender>Male|Female))?_(?<num>\d+)_Static_\d+_[0-9A-Fa-f]+$");
        // "Chr_FantasyHero_Preset_20" → num=20 (카테고리/성별 그룹 없음, 번호만 추출)
        private static readonly Regex PresetPrefabNamePattern =
            new(@"^Chr_FantasyHero_Preset_(?<num>\d+)$");

        // ── 필드 ──────────────────────────────────
        private string _staticPrefabFolder;
        private string _itemThumbnailRoot;
        private string _excludeSubfolder;
        private string _thumbnailCategoryName;
        private ItemPopulate _populate; // Synty 확장팩 스크립트

        private List<string> _groupKeys = new();
        private Dictionary<string, List<GameObject>> _groups = new();
        private int _selected;

        private List<List<GameObject>> _presetBatches = new();
        private int _presetBatchIndex;

        // ── Unity 라이프사이클 ──────────────────────────────────
        [MenuItem("Tools/Customization/Thumbnail Bake Helper")]
        private static void Open() => GetWindow<ThumbnailBakeHelper>("Thumbnail Bake Helper");

        private void OnEnable()
        {
            _staticPrefabFolder = EditorPrefs.GetString(SourcePrefKey, DefaultStaticFolder);
            _itemThumbnailRoot = EditorPrefs.GetString(ItemThumbnailRootPrefKey, DefaultItemThumbnailRoot);
            _excludeSubfolder = EditorPrefs.GetString(ExcludeSubfolderPrefKey, "");
            _thumbnailCategoryName = EditorPrefs.GetString(ThumbnailCategoryPrefKey, "");
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(SourcePrefKey, _staticPrefabFolder);
            EditorPrefs.SetString(ItemThumbnailRootPrefKey, _itemThumbnailRoot);
            EditorPrefs.SetString(ExcludeSubfolderPrefKey, _excludeSubfolder);
            EditorPrefs.SetString(ThumbnailCategoryPrefKey, _thumbnailCategoryName);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "사용 순서\n" +
                "[Static 파츠] 1) 스캔 → 그룹 선택 → 2) 채우기 → ▶Play bake (그룹마다 반복) → 3) 정렬\n" +
                "[프리셋]      P1) 스캔 → 배치 선택 → P2) 채우기 → ▶Play bake (배치마다 반복) → P3) 정렬\n" +
                "[아이템]      Source Folder 지정 → W1) 채우기 → ▶Play bake → I) 정렬\n" +
                "공통 후처리   4) Sprite 설정 → 5) 이름 접미사 제거 (모든 트랙 완료 후 한 번)",
                MessageType.None);
            EditorGUILayout.Space();

            _staticPrefabFolder = DrawFolderField("Source Folder", _staticPrefabFolder, "소스 폴더 선택");
            _excludeSubfolder = EditorGUILayout.TextField("  ㄴ 제외 하위 폴더명 (선택, 아이템 워크플로 전용)", _excludeSubfolder);

            _populate = (ItemPopulate)EditorGUILayout.ObjectField(
                "ItemPopulate (씬)", _populate, typeof(ItemPopulate), true);

            DrawStaticPartsSection();
            DrawPresetSection();
            DrawItemThumbnailSection();
            DrawCommonPostProcessingSection();
        }

        // ── GUI 섹션 ──────────────────────────────────
        private void DrawStaticPartsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("캐릭터 파츠 썸네일 (Characters_Parts)", EditorStyles.boldLabel);

            if (GUILayout.Button("1) Static 프리팹 스캔 → 그룹화"))
                ScanGroups();

            if (_groupKeys.Count > 0)
            {
                _selected = EditorGUILayout.Popup("그룹", _selected, _groupKeys.ToArray());
                EditorGUILayout.HelpBox(
                    $"{_groupKeys[_selected]} — {_groups[_groupKeys[_selected]].Count}개", MessageType.Info);

                using (new EditorGUI.DisabledScope(_populate == null))
                {
                    if (GUILayout.Button("2) 선택 그룹을 ItemPopulate에 채우기"))
                        LoadGroupIntoPopulate(_groupKeys[_selected]);
                }
                EditorGUILayout.HelpBox("채운 뒤 ▶Play로 bake → 그룹마다 반복.", MessageType.None);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("3) bake된 PNG를 폴더 구조로 정렬"))
                SortBakedIcons();
        }

        private void DrawPresetSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("캐릭터 프리셋 썸네일 (Characters_Presets)", EditorStyles.boldLabel);

            if (GUILayout.Button("P1) 프리셋 스캔 → 배치 분할"))
                ScanPresetBatches();

            if (_presetBatches.Count > 0)
            {
                var batchLabels = Enumerable.Range(0, _presetBatches.Count)
                    .Select(i => $"배치 {i + 1} ({i * PresetBatchSize + 1}~{i * PresetBatchSize + _presetBatches[i].Count})")
                    .ToArray();
                _presetBatchIndex = EditorGUILayout.Popup("배치", _presetBatchIndex, batchLabels);

                using (new EditorGUI.DisabledScope(_populate == null))
                {
                    if (GUILayout.Button("P2) 선택 배치를 ItemPopulate에 채우기"))
                        LoadPresetBatchIntoPopulate(_presetBatchIndex);
                }
                EditorGUILayout.HelpBox("채운 뒤 ▶Play로 bake → 배치마다 반복.", MessageType.None);
            }

            if (GUILayout.Button("P3) bake된 프리셋 PNG를 폴더로 정렬"))
                SortPresetIcons();
        }

        private void DrawItemThumbnailSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("아이템 썸네일 (Source Folder 기준, 임의 카테고리)", EditorStyles.boldLabel);

            _itemThumbnailRoot = DrawFolderField("저장 루트", _itemThumbnailRoot, "저장 루트 폴더 선택");
            _thumbnailCategoryName = EditorGUILayout.TextField("  ㄴ 썸네일 폴더명 (예: DungeonItems)", _thumbnailCategoryName);

            using (new EditorGUI.DisabledScope(_populate == null))
            {
                if (GUILayout.Button("I1) Source Folder 프리팹 전체를 ItemPopulate에 채우기"))
                    LoadFolderIntoPopulate(_staticPrefabFolder);
            }

            EditorGUILayout.HelpBox(
                "Source Folder(맨 위)에 원하는 프리팹 폴더를 넣고 bake한 뒤 아래 버튼을 누르면, " +
                "그 폴더명 그대로 저장 루트 밑에 하위 폴더가 생성됩니다. 예: .../Prefabs/Weapons → <저장 루트>/Weapons/\n" +
                "제외 하위 폴더명을 입력하면 그 이름의 하위 폴더(및 그 안의 프리팹)는 스캔에서 제외됩니다. 예: Modular",
                MessageType.None);
            if (GUILayout.Button("I2) bake된 PNG를 Source Folder명으로 정렬"))
                SortItemIcons();
        }

        private void DrawCommonPostProcessingSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공통 후처리 (Static + 프리셋 + 아이템 공용)", EditorStyles.boldLabel);

            if (GUILayout.Button("4) 썸네일 전부 Sprite 타입으로 설정"))
                SetThumbnailsAsSprite();

            if (GUILayout.Button("5) 기존 썸네일 이름 접미사 제거"))
                StripThumbnailSuffix();
        }

        // 텍스트필드 + "..." 폴더 브라우즈 버튼 한 쌍. Source Folder/저장 루트가 동일 패턴을 써서 공용화.
        private static string DrawFolderField(string label, string value, string dialogTitle)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    var abs = EditorUtility.OpenFolderPanel(dialogTitle, value, "");
                    // 프로젝트 밖 폴더는 무시 (Addressables/AssetDatabase는 Assets/ 상대경로만 인식)
                    if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                        value = "Assets" + abs.Substring(Application.dataPath.Length);
                }
            }
            return value;
        }

        // ── Static 파츠 ──────────────────────────────────
        private void ScanGroups()
        {
            _groups = new Dictionary<string, List<GameObject>>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { _staticPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                var m = PrefabNamePattern.Match(name);
                if (!m.Success) continue;

                var key = GroupKey(m.Groups["gender"].Value, m.Groups["cat"].Value);
                if (!_groups.TryGetValue(key, out var list))
                    _groups[key] = list = new List<GameObject>();
                list.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            _groupKeys = _groups.Keys.OrderBy(k => k).ToList();
            _selected = 0;
            Debug.Log($"썸네일 그룹 {_groupKeys.Count}개 / 프리팹 {_groups.Values.Sum(l => l.Count)}개 스캔.");
        }

        private void LoadGroupIntoPopulate(string key)
        {
            // 이름순 정렬: bake 순서를 파츠 인덱스 순서(이름 매칭)와 맞추기 위함
            _populate.itemsToPopulate = _groups[key].OrderBy(go => go.name).ToList();
            EditorUtility.SetDirty(_populate);

            if (Application.isPlaying)
                _populate.RegenerateIcons();   // 플레이 중이면 나가지 않고 바로 다시 스폰

            Debug.Log($"'{key}' {_populate.itemsToPopulate.Count}개 채움. ▶Play로 bake하세요.");
        }

        private void SortBakedIcons()
        {
            AssetDatabase.Refresh(); // bake는 File.WriteAllBytes라 먼저 import 시켜야 FindAssets에 잡힘
            if (!AssetDatabase.IsValidFolder(BakedIconsFolder))
            {
                Debug.LogWarning($"bake 폴더 없음: {BakedIconsFolder}");
                return;
            }

            var moved = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BakedIconsFolder }))
            {
                var src = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(src);
                var m = BakedPngPattern.Match(fileName);
                if (!m.Success) continue;

                var folder = EnsureFolder(GroupKey(m.Groups["gender"].Value, m.Groups["cat"].Value));
                var err = AssetDatabase.MoveAsset(src, $"{folder}/{fileName}.png");
                if (string.IsNullOrEmpty(err)) moved++;
                else Debug.LogWarning($"이동 실패 {src}: {err}");
            }
            AssetDatabase.Refresh();
            Debug.Log($"썸네일 {moved}개 정렬 완료 → {ThumbnailRoot}");
        }

        // ── 프리셋 ──────────────────────────────────
        private void ScanPresetBatches()
        {
            var sorted = new List<(int num, GameObject go)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { _staticPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                var m = PresetPrefabNamePattern.Match(name);
                // 패턴 일치 시 번호순 정렬 키, 불일치(다른 네이밍 폴더 등) 시 이름순으로 밀려나도록 MaxValue
                var num = m.Success ? int.Parse(m.Groups["num"].Value) : int.MaxValue;
                sorted.Add((num, AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            }
            sorted.Sort((a, b) => a.num != b.num
                ? a.num.CompareTo(b.num)
                : string.CompareOrdinal(a.go.name, b.go.name));

            _presetBatches = new List<List<GameObject>>();
            for (int i = 0; i < sorted.Count; i += PresetBatchSize)
                _presetBatches.Add(sorted.Skip(i).Take(PresetBatchSize).Select(x => x.go).ToList());

            _presetBatchIndex = 0;
            Debug.Log($"프리셋 {sorted.Count}개 스캔 → {_presetBatches.Count}개 배치({PresetBatchSize}개씩)로 분할.");
        }

        private void LoadPresetBatchIntoPopulate(int batchIndex)
        {
            _populate.itemsToPopulate = _presetBatches[batchIndex];
            EditorUtility.SetDirty(_populate);

            if (Application.isPlaying)
                _populate.RegenerateIcons();   // 플레이 중이면 나가지 않고 바로 다시 스폰

            Debug.Log($"배치 {batchIndex + 1} — {_populate.itemsToPopulate.Count}개 채움. ▶Play로 bake하세요.");
        }

        private void SortPresetIcons()
        {
            AssetDatabase.Refresh(); // bake는 File.WriteAllBytes라 먼저 import 시켜야 FindAssets에 잡힘
            if (!AssetDatabase.IsValidFolder(BakedIconsFolder))
            {
                Debug.LogWarning($"bake 폴더 없음: {BakedIconsFolder}");
                return;
            }
            if (!AssetDatabase.IsValidFolder(PresetThumbnailRoot))
                CreateFolderRecursive(PresetThumbnailRoot);

            var moved = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BakedIconsFolder }))
            {
                var src = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(src);
                if (BakedPngPattern.IsMatch(fileName)) continue; // Static 아이템 파츠는 기존 3번 버튼이 처리

                var err = AssetDatabase.MoveAsset(src, $"{PresetThumbnailRoot}/{fileName}.png");
                if (string.IsNullOrEmpty(err)) moved++;
                else Debug.LogWarning($"이동 실패 {src}: {err}");
            }
            AssetDatabase.Refresh();
            Debug.Log($"프리셋 썸네일 {moved}개 정렬 완료 → {PresetThumbnailRoot}");
        }

        // ── 아이템 (임의 프리팹 폴더) ──────────────────────────────────
        // Static/Preset과 달리 이름 패턴 매칭이 필요 없음 — 폴더 안 프리팹을 전부 그대로 채운다.
        private void LoadFolderIntoPopulate(string folder)
        {
            // Source Folder 바로 밑의 하위 폴더일 때만 제외 (더 깊은 곳의 동명 폴더는 영향 없음)
            var excludePath = string.IsNullOrEmpty(_excludeSubfolder)
                ? null
                : $"{folder.TrimEnd('/')}/{_excludeSubfolder}/";

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => excludePath == null || !path.StartsWith(excludePath))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .OrderBy(go => go.name)
                .ToList();

            _populate.itemsToPopulate = prefabs;
            EditorUtility.SetDirty(_populate);

            if (Application.isPlaying)
                _populate.RegenerateIcons();

            var excludeMsg = string.IsNullOrEmpty(_excludeSubfolder) ? "" : $" ('{_excludeSubfolder}' 하위 폴더 제외)";
            Debug.Log($"'{folder}' {prefabs.Count}개 프리팹을 ItemPopulate에 채움{excludeMsg}. ▶Play로 bake하세요.");
        }

        // Source Folder 경로의 마지막 폴더명을 그대로 목적지 하위 폴더로 사용 — 카테고리 추가 시 코드 수정 불필요.
        // 단, 같은 Play 세션에 캐릭터 파츠와 아이템을 섞어서 bake하면 이 버튼과 P3 버튼이
        // 서로의 결과물을 가로챌 수 있으니 카테고리별로 세션을 분리해서 bake할 것.
        private void SortItemIcons()
        {
            AssetDatabase.Refresh(); // bake는 File.WriteAllBytes라 먼저 import 시켜야 FindAssets에 잡힘
            if (!AssetDatabase.IsValidFolder(BakedIconsFolder))
            {
                Debug.LogWarning($"bake 폴더 없음: {BakedIconsFolder}");
                return;
            }

            if (string.IsNullOrEmpty(_thumbnailCategoryName))
            {
                Debug.LogWarning("[ThumbnailBakeHelper] 썸네일 폴더명을 입력하세요 (예: DungeonItems). 경로 자동 추론은 폴더명이 겹칠 수 있어 제거됨.");
                return;
            }
            var destFolder = CreateFolderRecursive($"{_itemThumbnailRoot}/{_thumbnailCategoryName}Thumbnails");

            var moved = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BakedIconsFolder }))
            {
                var src = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(src);
                if (BakedPngPattern.IsMatch(fileName)) continue; // 캐릭터 파츠는 기존 3번 버튼이 처리

                var err = AssetDatabase.MoveAsset(src, $"{destFolder}/{fileName}.png");
                if (string.IsNullOrEmpty(err)) moved++;
                else Debug.LogWarning($"이동 실패 {src}: {err}");
            }
            AssetDatabase.Refresh();
            Debug.Log($"아이템 썸네일 {moved}개 정렬 완료 → {destFolder} (Source Folder: {_staticPrefabFolder})");
        }

        // ── 폴더 경로 공용 헬퍼 ──────────────────────────────────
        // gender 있으면 "{Gender}_Parts/{cat}", 없으면 "All_Gender_Parts/{cat}"
        private static string GroupKey(string gender, string cat)
        {
            var root = string.IsNullOrEmpty(gender) ? "All_Gender_Parts" : $"{gender}_Parts";
            // Head는 소스 프리팹 파일명에 "All_Elements" 표기가 없어 접미사 없는 기본값으로 잡힌다.
            // CharacterParts 쪽(Female_Head_All_Elements)과 이름을 맞추기 위해 명시적으로 보정.
            if (cat == "Head")
                cat = "Head_All_Elements";
            return $"{root}/{cat}";
        }

        // ThumbnailRoot/GenderRoot/Category 폴더를 (없으면) 한 단계씩 생성하고 경로 반환
        private static string EnsureFolder(string groupKey)
        {
            return CreateFolderRecursive($"{ThumbnailRoot}/{groupKey}");
        }

        // 임의의 전체 경로를 한 단계씩 생성 (이미 있으면 스킵)하고 그대로 반환
        private static string CreateFolderRecursive(string fullPath)
        {
            if (AssetDatabase.IsValidFolder(fullPath)) return fullPath;

            var parts = fullPath.Split('/');
            var cur = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
            return fullPath;
        }

        // ── 공통 후처리 ──────────────────────────────────
        private void SetThumbnailsAsSprite()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ThumbnailRoot, PresetThumbnailRoot, _itemThumbnailRoot });
            Debug.Log($"대상 텍스처 {guids.Length}개 검사 시작.");   // ← 0이면 ThumbnailRoot 경로 문제
            var changed = 0;
            AssetDatabase.StartAssetEditing();   // 720개 reimport를 한 묶음으로 (개별 갱신보다 빠름)
            try
            {
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    // Sprite + Single 둘 다 맞아야 스킵 (지금은 Multiple이라 다시 처리돼야 함)
                    if (importer.textureType == TextureImporterType.Sprite &&
                        importer.spriteImportMode == SpriteImportMode.Single)
                        continue;

                    importer.textureType = TextureImporterType.Sprite;   // 기본 Sprite Mode=Single (썸네일 1장씩이라 적합)
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();   // 배치 종료 — 여기서 실제로 한번에 재임포트 처리됨
            }

            AssetDatabase.Refresh();
            Debug.Log($"{changed}개 텍스처를 Sprite로 설정 완료.");
        }

        private void StripThumbnailSuffix()
        {
            var renamed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ThumbnailRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var m = BakedPngPattern.Match(fileName);     // 아직 BakedPngPattern 있을 때 실행
                    if (!m.Success) continue;

                    var g = m.Groups["gender"].Value;
                    var clean = string.IsNullOrEmpty(g)
                        ? $"Chr_{m.Groups["cat"].Value}_{m.Groups["num"].Value}_Static"
                        : $"Chr_{m.Groups["cat"].Value}_{g}_{m.Groups["num"].Value}_Static";
                    if (clean == fileName) continue;

                    AssetDatabase.RenameAsset(path, clean);      // 확장자 없이 새 이름
                    renamed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }
            Debug.Log($"{renamed}개 이름 정리 완료.");
        }
    }
}