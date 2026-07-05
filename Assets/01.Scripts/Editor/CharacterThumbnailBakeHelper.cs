using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MySecondWorld.Editor
{
    // Static 파츠 720개로 썸네일 PNG를 굽기 위한 보조 도구.
    // ItemPopulate(서드파티)는 수정하지 않고, 이 도구가
    //   (1) Static 프리팹을 카테고리/성별 그룹으로 묶어 ItemPopulate.itemsToPopulate에 채워주고
    //   (2) bake 후 평면 출력된 PNG를 CharacterParts와 같은 폴더 구조로 정렬한다.
    public class CharacterThumbnailBakeHelper : EditorWindow
    {
        private const string DefaultStaticFolder = "Assets/Models/PolygonFantasyHeroCharacters/Prefabs/Characters_ModularParts_Static";
        private const string SourcePrefKey = "ThumbnailBaker.SourceFolder";
        private const string BakedIconsFolder = "Assets/Saved/Icons";   // ItemPopulate.CreateIcons 고정 출력 경로
        private const string ThumbnailRoot = "Assets/Addressables/CharacterPartThumbnails";
        private const string PresetThumbnailRoot = "Assets/Addressables/CharacterPresetThumbnails";
        // "Chr_Head_No_Elements_Male_02_Static" → cat=Head_No_Elements, gender=Male, num=02
        // "Chr_Hair_34_Static"                  → cat=Hair, gender=(없음), num=34
        // Chr_<카테고리>_<성별?>_<NN>_Static 규칙만 지키면, 소스 폴더만 바꿔주면 끝
        private static readonly Regex PrefabNamePattern =
            new(@"^Chr_(?<cat>.+?)(?:_(?<gender>Male|Female))?_(?<num>\d+)_Static$");
        // bake된 PNG: "Chr_Hair_34_Static_256_RRGGBBAA" (사이즈+배경색 접미사가 붙음)
        private static readonly Regex BakedPngPattern =
            new(@"^Chr_(?<cat>.+?)(?:_(?<gender>Male|Female))?_(?<num>\d+)_Static_\d+_[0-9A-Fa-f]+$");
        private const int PresetBatchSize = 20;
        // "Chr_FantasyHero_Preset_20" → num=20 (카테고리/성별 그룹 없음, 번호만 추출)
        private static readonly Regex PresetPrefabNamePattern =
            new(@"^Chr_FantasyHero_Preset_(?<num>\d+)$");
        private string _staticPrefabFolder;
        private ItemPopulate _populate; //Synty 확장팩 스크립트
        private List<string> _groupKeys = new();
        private Dictionary<string, List<GameObject>> _groups = new();
        private int _selected;
        private List<List<GameObject>> _presetBatches = new();
        private int _presetBatchIndex;

        [MenuItem("Tools/Customization/Bake Character Part Thumbnails")]
        private static void Open() => GetWindow<CharacterThumbnailBakeHelper>("Thumbnail Bake Helper");
        private void OnEnable() => _staticPrefabFolder = EditorPrefs.GetString(SourcePrefKey, DefaultStaticFolder);
        private void OnDisable() => EditorPrefs.SetString(SourcePrefKey, _staticPrefabFolder);

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _staticPrefabFolder = EditorGUILayout.TextField("Source Folder", _staticPrefabFolder);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    var abs = EditorUtility.OpenFolderPanel("소스 폴더 선택", _staticPrefabFolder, "");
                    // 프로젝트 밖 폴더는 무시 (Addressables/AssetDatabase는 Assets/ 상대경로만 인식)
                    if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                        _staticPrefabFolder = "Assets" + abs.Substring(Application.dataPath.Length);
                }
            }

            _populate = (ItemPopulate)EditorGUILayout.ObjectField(
                "ItemPopulate (씬)", _populate, typeof(ItemPopulate), true);

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("프리셋 썸네일 (Characters_Presets)", EditorStyles.boldLabel);

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공통 후처리 (Static + 프리셋 공용)", EditorStyles.boldLabel);

            if (GUILayout.Button("4) 썸네일 전부 Sprite 타입으로 설정"))
                SetThumbnailsAsSprite();

            if (GUILayout.Button("5) 기존 썸네일 이름 접미사 제거"))
                StripThumbnailSuffix();
        }

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

        private void LoadGroupIntoPopulate(string key)
        {
            // 이름순 정렬: bake 순서를 파츠 인덱스 순서(이름 매칭)와 맞추기 위함
            _populate.itemsToPopulate = _groups[key].OrderBy(go => go.name).ToList();
            EditorUtility.SetDirty(_populate);

            if (Application.isPlaying)
                _populate.RegenerateIcons();   // 플레이 중이면 나가지 않고 바로 다시 스폰

            Debug.Log($"'{key}' {_populate.itemsToPopulate.Count}개 채움. ▶Play로 bake하세요.");
        }

        private void LoadPresetBatchIntoPopulate(int batchIndex)
        {
            _populate.itemsToPopulate = _presetBatches[batchIndex];
            EditorUtility.SetDirty(_populate);

            if (Application.isPlaying)
                _populate.RegenerateIcons();   // 플레이 중이면 나가지 않고 바로 다시 스폰

            Debug.Log($"배치 {batchIndex + 1} — {_populate.itemsToPopulate.Count}개 채움. ▶Play로 bake하세요.");
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

        private void SetThumbnailsAsSprite()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ThumbnailRoot, PresetThumbnailRoot });
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