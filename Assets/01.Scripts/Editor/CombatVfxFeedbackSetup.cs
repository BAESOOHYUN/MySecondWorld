using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using MySecondWorld.Combat.Vfx;
using MySecondWorld.Combat.Player;
using MySecondWorld.Combat.Companion;
using MySecondWorld.Combat.Monster;
using MySecondWorld.Inventory;

namespace MySecondWorld.Editor
{
    // combat-vfx-feedback(20260830) 계획으로 만든 SO/컴포넌트를 실제 프리팹·에셋에
    // 일괄 연결하는 1회성(재실행 안전) 셋업 도구. 매핑표/경로는 CHECKLIST 문서 참고.
    public static class CombatVfxFeedbackSetup
    {
        private const string CombatSoFolder = "Assets/06.ScriptableObject/Combat";
        private const string WeaponMapAssetPath = CombatSoFolder + "/WeaponAttackVfxMapSO.asset";
        private const string VfxDefaultsAssetPath = CombatSoFolder + "/CombatVfxDefaultsSO.asset";

        private const string PlayerPrefabPath = "Assets/03.Prefab/Avatar/PlayerBase.prefab";
        private const string CompanionPrefabPath = "Assets/03.Prefab/Avatar/Companion_Ari.prefab";

        private static readonly string[] MonsterPrefabFolders =
        {
            "Assets/Models/PolygonDungeon/Prefabs/Characters",
            "Assets/Models/Low Poly Monster Bundle/MonsterPrefabs",
        };

        private const string CommonHitVfxPath =
            "Assets/Models/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit C 3D.prefab";
        private const string CommonDeathVfxPath =
            "Assets/Models/Epic Toon FX/Prefabs/Combat/Death/Souls/SoulGenericDeath.prefab";
        private const string BossSlamVfxPath =
            "Assets/Models/PolygonParticleFX/Prefabs/FX_GroundCrack_Blast_01.prefab";

        private static readonly (WeaponCategory Category, string Path)[] WeaponVfxMapping =
        {
            (WeaponCategory.Sword,
                "Assets/Models/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Slash (Blue).prefab"),
            (WeaponCategory.Dagger,
                "Assets/Models/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/Variants/CFXR Hit A (Blue).prefab"),
            (WeaponCategory.Axe,
                "Assets/Models/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit B 3D (Blue).prefab"),
            (WeaponCategory.Polearm,
                "Assets/Models/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/Variants/CFXR Slash (Cross, Blue).prefab"),
            (WeaponCategory.Bow,
                "Assets/Models/Hovl Studio/Fantastic cartoon VFX/Prefabs/Cartoon shoot.prefab"),
            (WeaponCategory.Ring,
                "Assets/Models/Hovl Studio/Fantastic cartoon VFX/Prefabs/Cartoon slash.prefab"),
            (WeaponCategory.Bracelet,
                "Assets/Models/Hovl Studio/Fantastic cartoon VFX/Prefabs/Cartoon slash.prefab"),
            (WeaponCategory.Necklace,
                "Assets/Models/Hovl Studio/Fantastic cartoon VFX/Prefabs/Cartoon slash.prefab"),
            (WeaponCategory.None,
                "Assets/Models/Epic Toon FX/Prefabs/Combat/Brawling/Toon/Directional/ToonPunchMedium.prefab"),
        };

        [MenuItem("Tools/Combat/Setup Combat Vfx Feedback")]
        public static void Setup()
        {
            var log = new StringBuilder();
            log.AppendLine("=== Combat Vfx Feedback Setup ===");

            EnsureFolder();

            var hitVfx = LoadOrWarn(CommonHitVfxPath, log);
            var deathVfx = LoadOrWarn(CommonDeathVfxPath, log);
            var slamVfx = LoadOrWarn(BossSlamVfxPath, log);

            var weaponMap = SetupWeaponVfxMap(log);
            var vfxDefaults = SetupVfxDefaults(hitVfx, deathVfx, log);

            AssetDatabase.SaveAssets();

            WirePlayerPrefab(weaponMap, vfxDefaults, log);
            WireCompanionPrefab(vfxDefaults, log);
            WireMonsterPrefabs(vfxDefaults, log);
            WireBossStats(slamVfx, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(log.ToString());
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(CombatSoFolder))
                AssetDatabase.CreateFolder("Assets/06.ScriptableObject", "Combat");
        }

        private static GameObject LoadOrWarn(string path, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                log.AppendLine($"[경로 없음] {path}");
            return prefab;
        }

        // ── SO 에셋 생성/갱신 ──────────────────────────────────────────

        private static WeaponAttackVfxMapSO SetupWeaponVfxMap(StringBuilder log)
        {
            var map = AssetDatabase.LoadAssetAtPath<WeaponAttackVfxMapSO>(WeaponMapAssetPath);
            bool isNew = map == null;
            if (isNew)
            {
                map = ScriptableObject.CreateInstance<WeaponAttackVfxMapSO>();
                AssetDatabase.CreateAsset(map, WeaponMapAssetPath);
            }

            var resolved = new List<(WeaponCategory Category, GameObject Prefab)>();
            foreach (var mapping in WeaponVfxMapping)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapping.Path);
                if (prefab == null)
                {
                    log.AppendLine($"[WeaponAttackVfxMapSO] 경로 없음 — {mapping.Category}: {mapping.Path}");
                    continue;
                }
                resolved.Add((mapping.Category, prefab));
            }

            var so = new SerializedObject(map);
            var entriesProp = so.FindProperty("_entries");
            entriesProp.arraySize = resolved.Count;
            for (int i = 0; i < resolved.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Category").enumValueIndex = (int)resolved[i].Category;
                element.FindPropertyRelative("VfxPrefab").objectReferenceValue = resolved[i].Prefab;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);

            log.AppendLine($"[WeaponAttackVfxMapSO] {(isNew ? "생성" : "갱신")} — 엔트리 {resolved.Count}/{WeaponVfxMapping.Length}개 연결");
            return map;
        }

        private static CombatVfxDefaultsSO SetupVfxDefaults(GameObject hitVfx, GameObject deathVfx, StringBuilder log)
        {
            var defaults = AssetDatabase.LoadAssetAtPath<CombatVfxDefaultsSO>(VfxDefaultsAssetPath);
            bool isNew = defaults == null;
            if (isNew)
            {
                defaults = ScriptableObject.CreateInstance<CombatVfxDefaultsSO>();
                AssetDatabase.CreateAsset(defaults, VfxDefaultsAssetPath);
            }

            var so = new SerializedObject(defaults);
            so.FindProperty("_hitVfxPrefab").objectReferenceValue = hitVfx;
            so.FindProperty("_deathVfxPrefab").objectReferenceValue = deathVfx;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(defaults);

            log.AppendLine($"[CombatVfxDefaultsSO] {(isNew ? "생성" : "갱신")} — Hit={(hitVfx != null)}, Death={(deathVfx != null)}");
            return defaults;
        }

        // ── 프리팹 와이어링 ────────────────────────────────────────────

        private static void WirePlayerPrefab(WeaponAttackVfxMapSO weaponMap, CombatVfxDefaultsSO vfxDefaults, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                log.AppendLine($"[PlayerBase] 프리팹 없음: {PlayerPrefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            bool changed = false;

            var attackController = root.GetComponentInChildren<PlayerAttackController>(true);
            if (attackController != null)
                changed |= SetObjectField(attackController, "_vfxMap", weaponMap);
            else
                log.AppendLine("[PlayerBase] PlayerAttackController 컴포넌트 없음");

            var healthState = root.GetComponentInChildren<PlayerHealthState>(true);
            if (healthState != null)
                changed |= SetObjectField(healthState, "_vfxDefaults", vfxDefaults);
            else
                log.AppendLine("[PlayerBase] PlayerHealthState 컴포넌트 없음");

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            log.AppendLine($"[PlayerBase] 와이어링 {(changed ? "완료" : "변경 없음")}");
        }

        private static void WireCompanionPrefab(CombatVfxDefaultsSO vfxDefaults, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompanionPrefabPath);
            if (prefab == null)
            {
                log.AppendLine($"[Companion_Ari] 프리팹 없음: {CompanionPrefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(CompanionPrefabPath);
            bool changed = false;

            var serverState = root.GetComponentInChildren<CompanionServerState>(true);
            if (serverState != null)
                changed |= SetObjectField(serverState, "_vfxDefaults", vfxDefaults);
            else
                log.AppendLine("[Companion_Ari] CompanionServerState 컴포넌트 없음");

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, CompanionPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            log.AppendLine($"[Companion_Ari] 와이어링 {(changed ? "완료" : "변경 없음")}");
        }

        private static void WireMonsterPrefabs(CombatVfxDefaultsSO vfxDefaults, StringBuilder log)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", MonsterPrefabFolders);
            int scanned = 0, matched = 0, wired = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                scanned++;

                var root = PrefabUtility.LoadPrefabContents(path);
                var serverState = root.GetComponentInChildren<MonsterServerState>(true);
                if (serverState != null)
                {
                    matched++;
                    if (SetObjectField(serverState, "_vfxDefaults", vfxDefaults))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        wired++;
                    }
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.AppendLine($"[Monster] 스캔 {scanned}개 / MonsterServerState 보유 {matched}개 / 와이어링 {wired}개");
        }

        private static void WireBossStats(GameObject slamVfx, StringBuilder log)
        {
            var guids = AssetDatabase.FindAssets("t:BossStatsSO");
            if (guids.Length == 0)
            {
                log.AppendLine("[BossStatsSO] 에셋 0개 — 아직 보스 스탯이 만들어지지 않음(정상일 수 있음)");
                return;
            }

            int wired = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var bossStats = AssetDatabase.LoadAssetAtPath<BossStatsSO>(path);
                if (bossStats == null) continue;

                var so = new SerializedObject(bossStats);
                so.FindProperty("_aoeSlamVfxPrefab").objectReferenceValue = slamVfx;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(bossStats);
                wired++;
            }

            log.AppendLine($"[BossStatsSO] 에셋 {guids.Length}개 중 {wired}개 슬램 VFX 연결");
        }

        // private [SerializeField] 필드에 오브젝트 참조를 설정. 값이 이미 같으면 false(변경 없음) 반환.
        private static bool SetObjectField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[CombatVfxFeedbackSetup] 필드 '{fieldName}' 못 찾음 on {target.GetType().Name}");
                return false;
            }

            if (prop.objectReferenceValue == value) return false;

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            return true;
        }
    }
}
