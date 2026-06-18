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
        // "Chr_Head_No_Elements_Male_02_Static" → cat=Head_No_Elements, gender=Male, num=02
        // "Chr_Hair_34_Static"                  → cat=Hair, gender=(없음), num=34
        // Chr_<카테고리>_<성별?>_<NN>_Static 규칙만 지키면, 소스 폴더만 바꿔주면 끝
        private static readonly Regex PrefabNamePattern =
            new(@"^Chr_(?<cat>.+?)(?:_(?<gender>Male|Female))?_(?<num>\d+)_Static$");
        // bake된 PNG: "Chr_Hair_34_Static_256_RRGGBBAA" (사이즈+배경색 접미사가 붙음)
        private static readonly Regex BakedPngPattern =
            new(@"^Chr_(?<cat>.+?)(?:_(?<gender>Male|Female))?_(?<num>\d+)_Static_\d+_[0-9A-Fa-f]+$");
        private string _staticPrefabFolder;
        private ItemPopulate _populate; //Synty 확장팩 스크립트
        private List<string> _groupKeys = new();
        private Dictionary<string, List<GameObject>> _groups = new();
        private int _selected;

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

            if (GUILayout.Button("4) 썸네일 전부 Sprite 타입으로 설정"))
                SetThumbnailsAsSprite();
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

        // gender 있으면 "{Gender}_Parts/{cat}", 없으면 "All_Gender_Parts/{cat}"
        private static string GroupKey(string gender, string cat)
        {
            var root = string.IsNullOrEmpty(gender) ? "All_Gender_Parts" : $"{gender}_Parts";
            return $"{root}/{cat}";
        }

        // ThumbnailRoot/GenderRoot/Category 폴더를 (없으면) 한 단계씩 생성하고 경로 반환
        private static string EnsureFolder(string groupKey)
        {
            var full = $"{ThumbnailRoot}/{groupKey}";
            if (AssetDatabase.IsValidFolder(full)) return full;

            var parts = full.Split('/');
            var cur = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
            return full;
        }

        private void SetThumbnailsAsSprite()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ThumbnailRoot });
            var changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();   // 720개 reimport를 한 묶음으로 (개별 갱신보다 빠름)
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                    if (importer.textureType == TextureImporterType.Sprite) continue;

                    importer.textureType = TextureImporterType.Sprite;   // 기본 Sprite Mode=Single (썸네일 1장씩이라 적합)
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            Debug.Log($"{changed}개 텍스처를 Sprite로 설정 완료.");
        }
    }
}