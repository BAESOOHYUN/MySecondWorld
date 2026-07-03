using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Customization;

namespace MySecondWorld.Editor
{
    // ── 목적 ──────────────────────────────────────────────────────────────────
    // Assets/Models/.../Characters_Presets 하위의 Synty 사전 세팅 캐릭터 프리팹은
    // Modular_Characters 밑에 "부위별 후보 여러 개 + 그 중 하나만 activeSelf=true" 구조로
    // 파츠가 이미 조합되어 있다. 이 도구는 그 활성 조합을 읽어서
    // NPC가 그대로 사용할 수 있는 CharacterAppearanceSO(런타임에 ObservableCharacterCustomizer가
    // 소비하는 그 SO)로 변환해준다 — 즉 "프리셋 프리팹 → NPC 외형 데이터" 원클릭 변환기.
    // 프리팹 1개씩 처리하는 단일 임포트와, 폴더 안의 프리팹을 전부 처리하는 일괄 임포트 두 가지를 제공한다.
    //
    // ── 왜 파츠 이름으로 매칭하는가 ──
    // AvatarPartKeyTableSO.Entries에 들어있는 AssetReferenceGameObject들은
    // ModularCharacterPartExtractor가 "원본 메시 GameObject 이름 그대로" 잘라 만든 프리팹들이다.
    // 즉 프리셋 프리팹 안에서 활성화된 리프 GameObject 이름(예: "Chr_Torso_Female_04")과
    // KeyTable에 등록된 프리팹 이름이 항상 1:1로 같다 — 폴더 구조나 인덱스 순서가 바뀌어도
    // "이름이 같은 것을 찾는다"는 원칙만 지키면 깨지지 않는다.
    //
    // ── 파츠 슬롯 이름 → PartType 매칭 규칙 ──
    // AvatarPartKeyTablePopulator.TryResolvePartType과 완전히 동일한 규칙(정규식 + CategoryAliases)을
    // 그대로 복제해서 쓴다. 두 도구가 서로 다른 매칭 규칙을 쓰면 KeyTable에 등록된 인덱스와
    // 프리셋에서 읽은 인덱스가 어긋날 수 있으므로, 이 두 파일 중 하나를 고치면 반드시 다른 하나도 같이 고칠 것.
    public class CharacterPresetImporterWindow : EditorWindow
    {
        // 프로젝트에 AvatarPartKeyTableSO는 이 경로 하나만 존재하고 앞으로도 여러 개 둘 계획이 없으므로
        // (unity-cli exec로 FindAssets 확인 결과 1개뿐이었음), 매번 드래그하지 않도록 OnEnable에서 기본값을
        // 자동으로 채워 넣는다. 그래도 필드 자체는 계속 노출해서, 혹시 다른 테이블로 바꿔 테스트하고 싶을 때는
        // 직접 드래그해서 덮어쓸 수 있게 열어둔다.
        private const string DefaultPartKeyTablePath = "Assets/06.ScriptableObject/AvatarPartKeyTableSO.asset";

        // 배치 임포트 기본 대상 폴더. Characters_Presets 전체를 한 번에 돌리는 게 제일 흔한 시나리오라 기본값으로 지정.
        private const string DefaultBatchFolder =
            "Assets/Models/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets";

        private AvatarPartKeyTableSO _partKeyTable;

        // ── 단일 임포트 ──
        private GameObject _sourcePreset;

        // NPC 이름표. SO 파일 이름 뒤에 그대로 붙는다 (예: "NPCAppearanceSO_Aria.asset").
        // 비워두면 소스 프리셋 프리팹 이름(예: "Chr_FantasyHero_Preset_1")을 대신 사용한다.
        private string _characterName = "";

        // 명시적으로 지정하면 그 에셋을 그대로 재사용(이름만 Character Name 기준으로 바꿔침).
        // 비워두면 _targetFolder 밑에 "NPCAppearanceSO_{이름}.asset" 경로로 자동 생성/재사용한다.
        private CharacterAppearanceSO _targetSO;

        // ── 공통 ──
        // 기존 PlayerAppearanceSO.asset과 같은 폴더(Assets/06.ScriptableObject/CharacterAppearance)를 쓰되,
        // NPC용은 섞이지 않도록 그 아래 NPC 서브폴더를 따로 둔다. 단일/배치 임포트 둘 다 이 폴더를 공유한다.
        private string _targetFolder = "Assets/06.ScriptableObject/CharacterAppearance/NPC";

        // ── 배치 임포트 ──
        // 폴더는 DefaultAsset(Project 창에서 폴더를 드래그하면 이 타입으로 잡힘)으로 받아서
        // 드래그&드롭 UX를 살린다. 실제 스캔은 AssetDatabase.GetAssetPath로 얻은 경로 문자열로 수행.
        private DefaultAsset _batchFolder;

        // 폴더를 스캔해서 찾은 프리팹 하나하나에 대응하는 행(row). characterName은 기본으로 프리팹 파일명을
        // 채워두되, Import 버튼을 누르기 전까지 사용자가 목록에서 직접 고쳐 쓸 수 있다.
        // [System.Serializable]이라 EditorWindow(=ScriptableObject 파생)에 필드로 두면 도메인 리로드에도 웬만하면 유지된다.
        [System.Serializable]
        private class BatchEntry
        {
            public string prefabPath;
            public string prefabName;
            public string characterName;
        }

        // readonly 안 붙이는 이유: Unity 직렬화는 readonly 필드를 아예 지원하지 않는다(공식 문서에 명시된 제약).
        private List<BatchEntry> _batchEntries = new();
        private Vector2 _batchScroll;

        private static readonly Regex CategoryPattern = new(@"^(?:Female_|Male_|All_)?\d+_(.+)$");

        private static readonly Dictionary<string, PartType> CategoryAliases = new()
        {
            { "Head_Attachment", PartType.HelmetAttachment },
            { "Shoulder_Attachment_Right", PartType.ShoulderAttachRight },
            { "Shoulder_Attachment_Left", PartType.ShoulderAttachLeft },
            { "Elbow_Attachment_Right", PartType.ElbowAttachRight },
            { "Elbow_Attachment_Left", PartType.ElbowAttachLeft },
            { "Knee_Attachement_Right", PartType.KneeAttachRight }, // 폴더명 오타(Attachement) 그대로 키로 사용
            { "Knee_Attachement_Left", PartType.KneeAttachLeft },
            { "Extra", PartType.Ear }, // All_12_Extra 안엔 Elf_Ear만 존재
        };

        // 런타임 흐름(CharacterCustomizerBase.cs:127 SetColor)은 "데이터 → 머티리얼" 방향으로 색을 적용한다:
        // CharacterAppearanceData의 색 값을 읽어서 material.SetColor(prop, color)로 셰이더에 써넣는다.
        // 이 도구는 그 반대 방향, 즉 "머티리얼 → 데이터"로 동작한다:
        // 프리셋 프리팹이 이미 가진 머티리얼에서 material.GetColor(prop)로 값을 읽어 CharacterAppearanceData에 채운다.
        // 그래서 아래 프로퍼티 이름 문자열은 CharacterCustomizerBase.SetColor(150~163번 줄)의 switch문에 있는
        // 문자열과 반드시 1:1로 똑같아야 한다. 둘 중 한쪽만 이름을 바꾸면:
        //   - 여기(Importer)만 옛 이름이면 → material.HasProperty가 false가 되어 그 색은 조용히 기본값(검정)으로 남는다.
        //   - CharacterCustomizerBase만 옛 이름이면 → 런타임에 색이 아예 셰이더에 안 먹는다.
        // 즉 셰이더 프로퍼티 이름을 바꿀 일이 생기면 이 배열과 CharacterCustomizerBase.SetColor 양쪽을 같이 고쳐야 한다.
        private static readonly (string shaderProp, ColorSlot slot)[] ColorPropertyMap =
        {
            ("_Color_Primary", ColorSlot.Primary),
            ("_Color_Secondary", ColorSlot.Secondary),
            ("_Color_Leather_Primary", ColorSlot.LeatherPrimary),
            ("_Color_Leather_Secondary", ColorSlot.LeatherSecondary),
            ("_Color_Metal_Primary", ColorSlot.MetalPrimary),
            ("_Color_Metal_Secondary", ColorSlot.MetalSecondary),
            ("_Color_Metal_Dark", ColorSlot.MetalDark),
            ("_Color_Hair", ColorSlot.Hair),
            ("_Color_Skin", ColorSlot.Skin),
            ("_Color_Stubble", ColorSlot.Stubble),
            ("_Color_Scar", ColorSlot.Scar),
            ("_Color_Eyes", ColorSlot.Eyes),
            ("_Color_BodyArt", ColorSlot.BodyArt),
        };

        // CharacterAppearanceData의 색상 필드 13개를 스위치문 없이 인덱스로 다루기 위한 내부 전용 열거값.
        // (CharacterAppearanceData는 PartType 인덱서는 있지만 색상 인덱서는 없어서 별도로 매핑)
        private enum ColorSlot
        {
            Primary, Secondary, LeatherPrimary, LeatherSecondary,
            MetalPrimary, MetalSecondary, MetalDark,
            Hair, Skin, Stubble, Scar, Eyes, BodyArt
        }

        // 프리팹 하나를 읽어서 뽑아낸 결과. 단일/배치 임포트가 이 구조체를 공유해서 로직 중복을 없앤다.
        private readonly struct ExtractResult
        {
            public readonly CharacterAppearanceData Data;
            public readonly int PartCount;
            public readonly int UnmatchedCount;
            public readonly List<string> Unmatched;
            public readonly bool ColorsFound;

            public ExtractResult(CharacterAppearanceData data, int partCount, List<string> unmatched, bool colorsFound)
            {
                Data = data;
                PartCount = partCount;
                Unmatched = unmatched;
                UnmatchedCount = unmatched.Count;
                ColorsFound = colorsFound;
            }
        }

        [MenuItem("Tools/Customization/Import Character Preset")]
        private static void Open()
        {
            GetWindow<CharacterPresetImporterWindow>("Character Preset Importer");
        }

        // 창을 새로 열 때(도메인 리로드 후 포함) 고정 자산 경로에서 기본값을 채워 넣는다.
        // 이미 값이 들어있으면(레이아웃 복원 등으로) 덮어쓰지 않는다.
        private void OnEnable()
        {
            if (_partKeyTable == null)
                _partKeyTable = AssetDatabase.LoadAssetAtPath<AvatarPartKeyTableSO>(DefaultPartKeyTablePath);
            if (_batchFolder == null)
                _batchFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultBatchFolder);
            if (_batchEntries.Count == 0)
                ScanBatchFolder();
        }

        public void OnGUI()
        {
            _partKeyTable = (AvatarPartKeyTableSO)EditorGUILayout.ObjectField(
                new GUIContent("Avatar Part Key Table", "프로젝트에 하나뿐인 고정 자산이라 기본값이 자동으로 채워진다. 다른 테이블로 테스트하고 싶을 때만 바꿀 것."),
                _partKeyTable, typeof(AvatarPartKeyTableSO), false);

            _targetFolder = EditorGUILayout.TextField(
                new GUIContent("New Asset Folder", "새로 만드는 CharacterAppearanceSO가 저장될 폴더. 단일/배치 임포트가 공유한다."),
                _targetFolder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("단일 임포트", EditorStyles.boldLabel);
            _sourcePreset = (GameObject)EditorGUILayout.ObjectField(
                "Source Preset Prefab", _sourcePreset, typeof(GameObject), false);
            _characterName = EditorGUILayout.TextField(
                new GUIContent("Character Name", "비우면 소스 프리팹 이름을 사용. SO 파일 이름 뒤에 붙는다 (예: NPCAppearanceSO_Aria)."),
                _characterName);
            _targetSO = (CharacterAppearanceSO)EditorGUILayout.ObjectField(
                new GUIContent("Target SO", "비우면 New Asset Folder 밑에 Character Name 기준으로 새로 생성하거나 기존 것을 재사용한다."),
                _targetSO, typeof(CharacterAppearanceSO), false);

            using (new EditorGUI.DisabledScope(_sourcePreset == null || _partKeyTable == null))
            {
                if (GUILayout.Button("Import (단일)"))
                    ImportSingle();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("일괄 임포트 (폴더)", EditorStyles.boldLabel);
            var newBatchFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("Batch Source Folder", "이 폴더 밑의 프리팹을 전부 스캔한다. Modular_Characters가 없는 프리팹은 Import 시 자동으로 건너뛴다."),
                _batchFolder, typeof(DefaultAsset), false);
            if (newBatchFolder != _batchFolder)
            {
                _batchFolder = newBatchFolder;
                ScanBatchFolder(); // 폴더를 바꾸면 목록도 바로 새로 스캔 (이전 폴더의 이름 목록이 남아있으면 헷갈리므로)
            }

            using (new EditorGUI.DisabledScope(_batchFolder == null))
            {
                if (GUILayout.Button("Scan Folder (목록 새로고침)"))
                    ScanBatchFolder();
            }

            if (_batchEntries.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"{_batchEntries.Count}개 프리팹 — Character Name을 프리팹별로 직접 수정 가능 (비워두면 프리팹 이름 사용)",
                    EditorStyles.miniLabel);

                _batchScroll = EditorGUILayout.BeginScrollView(_batchScroll, GUILayout.Height(250));
                foreach (var entry in _batchEntries)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.prefabName, GUILayout.Width(220));
                    entry.characterName = EditorGUILayout.TextField(entry.characterName);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("이름 전부 프리팹 이름으로 리셋"))
                    foreach (var entry in _batchEntries)
                        entry.characterName = entry.prefabName;
            }

            using (new EditorGUI.DisabledScope(_batchEntries.Count == 0 || _partKeyTable == null))
            {
                if (GUILayout.Button($"Import All ({_batchEntries.Count}개)"))
                    ImportBatch();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "색상은 프리셋 프리팹이 실제로 참조하는 공유 머티리얼(예: FantasyHero.mat)에서 그대로 읽어온다.\n" +
                "같은 파일명의 SO가 New Asset Folder에 이미 있으면 새로 만들지 않고 그 안의 내용만 덮어써서(idempotent),\n" +
                "같은 이름으로 재실행해도 중복 에셋이 안 쌓인다. 단, Character Name을 바꾼 뒤 다시 Import하면 새 이름의\n" +
                "에셋이 새로 생기고 예전 이름의 에셋은 그대로 남으니, 필요하면 옛것은 직접 삭제할 것.",
                MessageType.Info);
        }

        // Batch Source Folder 밑의 프리팹을 전부 찾아서 _batchEntries를 다시 만든다.
        // Character Name 기본값은 프리팹 파일명이며, 이미 목록에 같은 경로가 있어도 이름 수정 내역은 보존하지 않고
        // 완전히 새로 스캔한다 — "새로고침"이라는 버튼 의미에 맞춰 항상 최신 폴더 상태를 반영하기 위함.
        // (Modular_Characters 유무는 여기서 안 걸러낸다: 프리팹마다 LoadPrefabContents를 또 해야 해서 스캔이
        //  두 배로 느려지고, 실제 유효성 검사는 Import 시 TryExtractAppearance가 어차피 한 번 더 하기 때문)
        private void ScanBatchFolder()
        {
            _batchEntries.Clear();
            if (_batchFolder == null) return;

            var folderPath = AssetDatabase.GetAssetPath(_batchFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                _batchEntries.Add(new BatchEntry { prefabPath = path, prefabName = name, characterName = name });
            }
        }

        private void ImportSingle()
        {
            var sourcePath = AssetDatabase.GetAssetPath(_sourcePreset);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[PresetImporter] 프리팹 경로를 찾을 수 없음");
                return;
            }

            if (!TryExtractAppearance(sourcePath, out var result))
                return; // 실패 사유는 TryExtractAppearance 내부에서 이미 로그로 남김

            // 단일 임포트는 Target SO를 명시적으로 지정할 수 있으므로, 지정 여부에 따라 다른 경로로 SO를 확보한다.
            var displayName = string.IsNullOrEmpty(_characterName) ? _sourcePreset.name : _characterName;
            var targetSO = _targetSO != null
                ? RenameToMatch(_targetSO, displayName)
                : ResolveOrCreateSO(_targetFolder, $"NPCAppearanceSO_{displayName}");

            ApplyAndSave(targetSO, result);
            LogResult(_sourcePreset.name, targetSO, result);
            Selection.activeObject = targetSO;
        }

        private void ImportBatch()
        {
            if (_batchEntries.Count == 0)
            {
                Debug.LogWarning("[PresetImporter] 스캔된 프리팹이 없음 — 먼저 Scan Folder를 실행할 것");
                return;
            }

            int created = 0, updated = 0, skipped = 0;
            var allUnmatched = new List<string>();
            var skippedNames = new List<string>();

            try
            {
                for (int i = 0; i < _batchEntries.Count; i++)
                {
                    var entry = _batchEntries[i];

                    EditorUtility.DisplayProgressBar(
                        "Character Preset Importer",
                        $"{entry.prefabName} ({i + 1}/{_batchEntries.Count})",
                        (float)(i + 1) / _batchEntries.Count);

                    if (!TryExtractAppearance(entry.prefabPath, out var result, logErrors: false))
                    {
                        skipped++;
                        skippedNames.Add(entry.prefabName);
                        continue;
                    }

                    // 목록에서 비워둔 경우(사용자가 지웠거나 처음부터 빈 값)에 대비한 방어적 기본값.
                    // 평소엔 ScanBatchFolder가 항상 prefabName으로 채워두므로 거의 안 탈 경로.
                    var displayName = string.IsNullOrEmpty(entry.characterName) ? entry.prefabName : entry.characterName;
                    var desiredName = $"NPCAppearanceSO_{displayName}";
                    var existedBefore = AssetDatabase.LoadAssetAtPath<CharacterAppearanceSO>(
                        $"{_targetFolder}/{desiredName}.asset") != null;

                    var targetSO = ResolveOrCreateSO(_targetFolder, desiredName);
                    ApplyAndSave(targetSO, result);

                    if (existedBefore) updated++; else created++;
                    if (result.UnmatchedCount > 0)
                        allUnmatched.AddRange(result.Unmatched.ConvertAll(u => $"{entry.prefabName}:{u}"));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[PresetImporter] 일괄 임포트 완료 — 신규 {created}개, 갱신 {updated}개, 스킵(Modular_Characters 없음) {skipped}개.");
            if (skippedNames.Count > 0)
                Debug.LogWarning("[PresetImporter] 스킵된 프리팹(Modular_Characters 없음):\n" + string.Join("\n", skippedNames));
            if (allUnmatched.Count > 0)
                Debug.LogWarning($"[PresetImporter] 파츠 매칭 실패 {allUnmatched.Count}건 (KeyTable에 파츠 없음):\n" + string.Join("\n", allUnmatched));
        }

        // 프리팹 1개를 열어서 (파츠 인덱스 + 색상)까지 전부 채운 CharacterAppearanceData를 만든다.
        // 단일/배치 임포트가 모두 이 메서드 하나만 호출한다 — 추출 로직은 여기 한 곳에만 존재.
        private bool TryExtractAppearance(string prefabPath, out ExtractResult result, bool logErrors = true)
        {
            result = default;

            List<(PartType part, string leafName)> leaves;
            Gender gender;
            bool genderAmbiguous;
            Color[] extractedColors; // ColorSlot 선언 순서와 1:1 대응. 프로퍼티를 못 찾은 슬롯은 foundAny=false로만 표시되고
                                      // 배열 값 자체는 default(Color)(0,0,0,0)로 남는다 — nullable을 쓰지 않는다.
            bool colorsFound;

            // LoadPrefabContents = 프리팹을 임시 씬에 풀어서 activeSelf 등 실제 계층 상태를 읽을 수 있게 해준다.
            // Instantiate와 달리 씬에 아무 흔적도 남기지 않고, 원본 .prefab 에셋도 전혀 건드리지 않는다.
            // 대신 반드시 UnloadPrefabContents로 정리해야 하므로 try/finally로 감싼다.
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var modularCharacters = root.transform.Find("Modular_Characters");
                if (modularCharacters == null)
                {
                    if (logErrors)
                        Debug.LogError($"[PresetImporter] 'Modular_Characters' 자식을 찾을 수 없음: {prefabPath}");
                    return false;
                }

                gender = ResolveGender(modularCharacters, out genderAmbiguous);
                leaves = CollectActiveLeaves(modularCharacters);
                colorsFound = TryExtractColors(modularCharacters, out extractedColors);
            }
            finally
            {
                // 임시 씬 오브젝트 정리 — 누락 시 에디터 메모리에 계속 남아 배치 임포트처럼 여러 번 돌릴 때 특히 누적된다.
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 몸 파츠(팔다리·몸통·머리 등)는 기본 0(첫 후보 = 항상 착용),
            // 액세서리류(수염·헤드커버·부착물)는 기본 -1(미착용) — CharacterAppearanceSO.SetDefaultIndices와 동일 관례.
            // 프리셋에서 해당 슬롯에 활성 리프가 없으면(=미착용 상태) 이 기본값이 그대로 유지된다.
            var data = new CharacterAppearanceData
            {
                Gender = gender,
                FacialHair = -1,
                HeadCoverings = -1,
                Ear = -1,
                HelmetAttachment = -1,
                BackAttachment = -1,
                ShoulderAttachRight = -1,
                ShoulderAttachLeft = -1,
                ElbowAttachRight = -1,
                ElbowAttachLeft = -1,
                KneeAttachRight = -1,
                KneeAttachLeft = -1,
                HipsAttachment = -1,
            };

            // 파츠 인덱스 매칭: 프리셋에서 찾은 (부위, 메시이름)을 KeyTable 후보 리스트에서 검색해 인덱스로 치환.
            // 못 찾은 항목(KeyTable populate가 덜 됐거나 이름이 어긋난 경우)은 unmatched로 모아서 경고만 하고 계속 진행.
            var unmatched = new List<string>();
            if (genderAmbiguous)
                unmatched.Add("Gender:AMBIGUOUS — 신체 파츠 중 일부가 Female/Male 양쪽에서 활성으로 잡힘, 수동 확인 권장");
            foreach (var (part, leafName) in leaves)
            {
                if (TryResolveIndex(part, gender, leafName, out var index))
                    data[part] = index;
                else
                    unmatched.Add($"{part}:{leafName}");
            }

            // 색상 적용: 프리셋 머티리얼에서 읽은 값이 있으면 그대로, 없으면(예외적으로 머티리얼이 없거나
            // 프로퍼티가 하나도 없는 경우) CharacterAppearanceData 기본값(전부 검정/투명)이 그대로 남는다.
            if (colorsFound)
                ApplyExtractedColors(ref data, extractedColors);

            result = new ExtractResult(data, leaves.Count, unmatched, colorsFound);
            return true;
        }

        private static void ApplyAndSave(CharacterAppearanceSO targetSO, ExtractResult result)
        {
            targetSO.DefaultAppearance = result.Data;
            EditorUtility.SetDirty(targetSO);
            AssetDatabase.SaveAssets();
        }

        private static void LogResult(string sourceName, CharacterAppearanceSO targetSO, ExtractResult result)
        {
            Debug.Log($"[PresetImporter] {sourceName} → {targetSO.name} " +
                      $"({result.Data.Gender}) 완료. 파츠 {result.PartCount - result.UnmatchedCount}/{result.PartCount}개 매칭. " +
                      $"색상 {(result.ColorsFound ? "추출됨" : "추출 실패 → 기본값(검정) 유지")}.");
            if (result.UnmatchedCount > 0)
                Debug.LogWarning("[PresetImporter] 매칭 실패 (KeyTable에 파츠 없음):\n" + string.Join("\n", result.Unmatched));
        }

        // folder/{fileNameNoExt}.asset 경로에 이미 CharacterAppearanceSO가 있으면 그걸 그대로 재사용(덮어쓰기 대상)하고,
        // 없으면 새로 만든다. 이 방식(경로 고정 + 존재하면 재사용) 덕분에 같은 이름으로 여러 번 실행해도
        // "_1", "_2" 같은 중복 에셋이 쌓이지 않고 항상 같은 결과가 된다 — AvatarPartKeyTablePopulator의
        // "Populate(덮어쓰기)" 관례와 동일한 사고방식.
        private static CharacterAppearanceSO ResolveOrCreateSO(string folder, string fileNameNoExt)
        {
            EnsureFolder(folder);
            var path = $"{folder}/{fileNameNoExt}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterAppearanceSO>(path);
            if (existing != null)
                return existing;

            var so = CreateInstance<CharacterAppearanceSO>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        // 단일 임포트에서 Target SO를 명시적으로 드래그해 지정한 경우 전용. 그 SO를 그대로 쓰되,
        // 파일 이름이 Character Name과 다르면 이름만 바꿔친다(내용은 이후 ApplyAndSave가 덮어씀).
        private static CharacterAppearanceSO RenameToMatch(CharacterAppearanceSO so, string displayName)
        {
            var desiredFileName = $"NPCAppearanceSO_{displayName}";
            var currentPath = AssetDatabase.GetAssetPath(so);
            if (Path.GetFileNameWithoutExtension(currentPath) == desiredFileName)
                return so;

            // RenameAsset은 확장자 없이 "새 파일명"만 받는다. 실패해도(중복 등) 예외 없이 에러 문자열만 반환하므로
            // 로그로 알려주고 기존 이름 그대로 진행한다.
            var error = AssetDatabase.RenameAsset(currentPath, desiredFileName);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"[PresetImporter] SO 이름 변경 실패({desiredFileName}): {error}");
            return so;
        }

        // 성별을 안정적으로 구분할 수 있는 "신체" 파츠들만 골랐다(얼굴 관련은 전부 제외).
        // 버그 수정 이력: 원래 Female_Parts 전체(Head 포함)에서 활성 리프가 하나라도 있으면 Female로 판정했는데,
        // 실제로 Male 프리셋(Chr_FantasyHero_Preset_2)인데도 Female_00_Head 하위에 원인 불명의 활성 잔여물이
        // 하나 껴 있어서(Torso/Arms/Hips/Legs는 전부 Male_Parts만 활성인데 Head만 예외) 성별이 Female로
        // 잘못 판정되는 경우를 실측으로 발견했다. Head 하나만 예외였다고 해서 Torso 하나만 믿는 것도
        // 똑같이 위험한 가정이다(120개 프리셋 전부를 검증한 게 아니므로) — 그래서 슬롯 하나만 보지 않고
        // 아래 10개를 전부 검사해 다수결로 판정하고, 의견이 갈리면(ambiguous) 배치 로그에 경고를 남겨
        // 사람이 직접 확인할 수 있게 한다.
        private static readonly PartType[] GenderCheckParts =
        {
            PartType.Torso, PartType.ArmUpperRight, PartType.ArmUpperLeft,
            PartType.ArmLowerRight, PartType.ArmLowerLeft, PartType.HandLeft, PartType.HandRight,
            PartType.Hips, PartType.LegLeft, PartType.LegRight,
        };

        private static Gender ResolveGender(Transform modularCharacters, out bool ambiguous)
        {
            int femaleVotes = CountActiveGenderCheckSlots(modularCharacters, "Female_Parts");
            int maleVotes = CountActiveGenderCheckSlots(modularCharacters, "Male_Parts");
            ambiguous = femaleVotes > 0 && maleVotes > 0; // 신체 파츠인데도 양쪽 다 활성 리프가 잡히면 뭔가 이상한 상태
            return femaleVotes >= maleVotes ? Gender.Female : Gender.Male;
        }

        // genderRootName("Female_Parts" 또는 "Male_Parts") 바로 아래 슬롯 중 GenderCheckParts에 해당하는 것만
        // 골라서, 그중 몇 개가 실제로 활성 리프를 갖고 있는지 센다.
        private static int CountActiveGenderCheckSlots(Transform modularCharacters, string genderRootName)
        {
            var genderRoot = modularCharacters.Find(genderRootName);
            if (genderRoot == null) return 0;

            int count = 0;
            foreach (Transform slot in genderRoot)
            {
                if (!TryResolvePartType(slot.name, out var partType)) continue;
                if (System.Array.IndexOf(GenderCheckParts, partType) < 0) continue;
                if (HasActiveLeaf(slot)) count++;
            }
            return count;
        }

        // node 아래에 activeSelf=true인 리프(자식이 없는 노드)가 하나라도 있는지 재귀 탐색.
        private static bool HasActiveLeaf(Transform node)
        {
            foreach (Transform child in node)
            {
                if (!child.gameObject.activeSelf) continue; // 비활성 분기는 그 아래도 볼 필요 없음(전부 대안 파츠)
                if (child.childCount == 0) return true;      // 자식 없음 = 실제 메시 리프
                if (HasActiveLeaf(child)) return true;
            }
            return false;
        }

        // Modular_Characters 바로 아래 3개 컨테이너(All_Gender_Parts/Female_Parts/Male_Parts)를 각각 순회하며,
        // 그 밑 1단계 자식(파츠 슬롯. 예: "Female_03_Torso")마다 PartType을 알아내고,
        // 그 슬롯 안에서 활성 상태를 따라 내려가 최종 리프(실제 착용 메시) 이름을 하나씩 수집한다.
        // 슬롯 하나당 활성 리프는 최대 1개(=Synty 규칙: 후보 중 정확히 하나만 켜져 있거나 전부 꺼져있음(미착용)).
        private static List<(PartType, string)> CollectActiveLeaves(Transform modularCharacters)
        {
            var result = new List<(PartType, string)>();
            foreach (Transform genderRoot in modularCharacters)
            {
                foreach (Transform slot in genderRoot)
                {
                    if (!TryResolvePartType(slot.name, out var partType))
                        continue; // 이름 매칭 실패 = 아직 다루지 않는 슬롯(예: Chest_Attachment) → 조용히 스킵

                    var leaf = FindActiveLeafName(slot);
                    if (leaf != null)
                        result.Add((partType, leaf));
                }
            }
            return result;
        }

        // slot(예: Female_03_Torso) 아래에서 활성 자식을 하나 골라, 그 자식이 리프일 때까지 계속 한 단계씩 내려간다.
        // 중간에 여러 단계 서브그룹(예: Female_00_Head → Female_Head_All_Elements → 실제 메시)이 있을 수 있어서 재귀.
        // 버그 수정 이력: "활성 자식은 항상 최대 1개"라고 가정하고 첫 번째 활성 자식만 보고 break했었는데,
        // 실측 결과(unity-cli LoadPrefabContents로 Chr_FantasyHero_Preset_1 직접 확인) HeadCoverings_Base_Hair/
        // No_FacialHair/No_Hair, Female_Head_All_Elements/No_Elements 같은 서브타입 컨테이너들은 전부
        // activeSelf=true인 채로 동시에 존재했다 — 진짜 "착용 여부"는 그보다 한 단계 더 아래에서 갈린다.
        // 그래서 첫 번째 활성 자식 안에 진짜 리프가 없으면(전부 비활성) 그 컨테이너 이름 자체를 리프인 것처럼
        // 잘못 반환해버렸고(예: "HeadCoverings_Base_Hair"), 그 이름은 실제 프리팹 파일명이 아니라서
        // KeyTable 매칭에 실패 → 조용히 -1(미착용)로 떨어졌었다(Head는 우연히 첫 분기에 진짜 리프가 있어서 안 걸림).
        // HasActiveLeaf()가 이미 하던 대로 "활성 자식을 전부 순서대로 시도하고, 안에 진짜 리프가 없으면 다음
        // 활성 형제로 넘어간다"는 규칙으로 통일해서 고쳤다.
        private static string FindActiveLeafName(Transform node)
        {
            foreach (Transform child in node)
            {
                if (!child.gameObject.activeSelf) continue; // 비활성 자식이면 건너뛰고 다음 자식으로
                if (child.childCount == 0) return child.name; // 자식 없음 = 실제 메시 리프, 바로 확정

                var deeper = FindActiveLeafName(child);
                if (deeper != null) return deeper; // 이 분기 안에 진짜 리프가 있었다 → 확정

                // 이 분기는 활성 컨테이너였지만 그 안엔 진짜 착용된 게 없었음(전부 비활성) → 다음 활성 형제로 계속 시도
            }
            return null; // 활성 자식이 아예 없거나, 활성 자식들 안 어디에도 진짜 리프가 없음 (미착용 상태)
        }

        // "Female_03_Torso" → "Torso", "All_04_Back_Attachment" → "BackAttachment" 처럼
        // (성별_)번호_ 접두사를 떼고 언더스코어를 지운 뒤 PartType enum 이름과 대소문자 무시 비교.
        // 1차 시도가 실패하면(Synty 원본 폴더명이 enum과 다르거나 오타가 있는 경우) CategoryAliases로 2차 시도.
        // AvatarPartKeyTablePopulator.TryResolvePartType과 반드시 동일 규칙을 유지해야 함(주석 상단 참고).
        private static bool TryResolvePartType(string folderName, out PartType partType)
        {
            var match = CategoryPattern.Match(folderName);
            var category = match.Success ? match.Groups[1].Value : folderName;
            var normalized = category.Replace("_", "");

            if (System.Enum.TryParse(normalized, true, out partType))
                return true;
            return CategoryAliases.TryGetValue(category, out partType);
        }

        // 목표: 프리셋에서 찾은 leafName(예: "Chr_Torso_Female_04")을 받아서
        // AvatarPartKeyTableSO.GetCandidates(part, gender) 리스트(전용 파츠 + AllGender 파츠를 이어붙인 순서) 안에서 찾아
        // 몇 번째(index)에 해당하는지 알아내는 것.
        // 이 인덱스가 곧 CharacterAppearanceData[part]에 들어갈 값이다
        // (런타임에 ObservableCharacterCustomizer가 같은 GetCandidates 순서로 Addressables를 로드하므로 순서가 어긋나면 안 됨).
        private bool TryResolveIndex(PartType part, Gender gender, string leafName, out int index)
        {
            var candidates = _partKeyTable.GetCandidates(part, gender);
            for (int i = 0; i < candidates.Count; i++)
            {
                // AssetReferenceGameObject(Addressables 타입)는 실제 이름이 아니라 **GUID만 들고 있다**.
                // candidates[i]가 null일 수도 있고(인스펙터에서 슬롯을 비워둔 경우),
                // GUID가 비어있을 수도 있으므로 방어적으로 스킵.
                var guid = candidates[i]?.AssetGUID;
                if (string.IsNullOrEmpty(guid)) continue;

                // AssetDatabase.GUIDToAssetPath는 에디터 전용 API로,
                // GUID를 실제 프로젝트 경로(Assets/Addressables/CharacterParts/.../Chr_Torso_Female_04.prefab)로 되돌려줌.
                // AssetReferenceGameObject 자체는 이름을 모르니 (GUID 만 알고 있음),
                // GUID → 경로 → 파일명 순서로 "이름"을 역으로 알아내는 것.
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == leafName)
                {
                    // leafName = GetCandidates 에서 찾은 이름이 똑같을 때 이름으로 매칭
                    index = i;
                    return true;
                }
            }
            // 끝까지 못 찾으면 -1. 이건 이 코드베이스 전체에서 "미착용/없음"을 뜻함
            index = -1;
            return false;
        }

        // ── 색상 추출 ──────────────────────────────────────────────────────────
        // Synty 캐릭터는 파츠별로 별도 머티리얼을 안 쓰고, 전신이 "FantasyHero" 같은 셰이더 하나를 공유하며
        // _Color_Primary 등 프로퍼티로 부위별 색을 마스크 기반으로 칠한다. 프리셋 프리팹은 이 프로퍼티 값을
        // 이미 캐릭터에 맞게 세팅해둔 상태이므로, 아무 활성 SkinnedMeshRenderer 하나의 sharedMaterial만 읽으면
        // 그 캐릭터의 색상 세트 전체를 그대로 가져올 수 있다(모든 파츠가 같은 머티리얼 인스턴스를 참조하기 때문).
        private static bool TryExtractColors(Transform modularCharacters, out Color[] colors)
        {
            colors = new Color[System.Enum.GetValues(typeof(ColorSlot)).Length];

            Material material = null;
            // includeInactive=false → 비활성(미착용 대안) 파츠는 건너뛰고, 실제로 켜져있는 파츠의 렌더러만 대상으로 함
            foreach (var smr in modularCharacters.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (smr.sharedMaterial != null)
                {
                    material = smr.sharedMaterial;
                    break; // 전신이 머티리얼 하나를 공유하므로 첫 번째로 찾은 것으로 충분
                }
            }

            if (material == null)
                return false;

            var foundAny = false;
            foreach (var (shaderProp, slot) in ColorPropertyMap)
            {
                if (!material.HasProperty(shaderProp))
                    continue;

                var c = material.GetColor(shaderProp);
                // 알파는 셰이더에서 안 쓰는 경우가 많고(프리셋 머티리얼에 0으로 저장된 경우 확인됨),
                // CharacterAppearanceSO.SetDefaultColors 관례(항상 alpha=1)와 맞추기 위해 강제로 1로 고정.
                c.a = 1f;
                colors[(int)slot] = c;
                foundAny = true;
            }
            return foundAny;
        }

        // TryExtractColors가 채운 배열을 CharacterAppearanceData의 실제 필드에 대입.
        // (색상 필드는 PartType처럼 공용 인덱서가 없어서 여기서만 쓰는 로컬 스위치로 처리)
        private static void ApplyExtractedColors(ref CharacterAppearanceData data, Color[] colors)
        {
            data.PrimaryColor = colors[(int)ColorSlot.Primary];
            data.SecondaryColor = colors[(int)ColorSlot.Secondary];
            data.LeatherPrimaryColor = colors[(int)ColorSlot.LeatherPrimary];
            data.LeatherSecondaryColor = colors[(int)ColorSlot.LeatherSecondary];
            data.MetalPrimaryColor = colors[(int)ColorSlot.MetalPrimary];
            data.MetalSecondaryColor = colors[(int)ColorSlot.MetalSecondary];
            data.MetalDarkColor = colors[(int)ColorSlot.MetalDark];
            data.HairColor = colors[(int)ColorSlot.Hair];
            data.SkinColor = colors[(int)ColorSlot.Skin];
            data.StubbleColor = colors[(int)ColorSlot.Stubble];
            data.ScarColor = colors[(int)ColorSlot.Scar];
            data.EyesColor = colors[(int)ColorSlot.Eyes];
            data.BodyArtColor = colors[(int)ColorSlot.BodyArt];
        }

        // 폴더가 없으면 재귀적으로 상위부터 만든다 (ModularCharacterPartExtractor.EnsureFolder와 동일 패턴).
        // System.IO.Directory.CreateDirectory를 안 쓰는 이유: 그렇게 만들면 .meta가 없는 "고스트 폴더"가 생겨서
        // Unity의 GUID 추적에서 벗어난다. 반드시 AssetDatabase.CreateFolder로 만들어야 한다.
        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
