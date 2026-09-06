using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace MySecondWorld.Editor
{
    /// <summary>
    /// TextMesh Pro 한글 폰트 Dynamic SDF 에셋 자동 생성 및
    /// 전역 Fallback / UI 폰트 Fallback 자동 연동 에디터 툴.
    /// </summary>
    public static class TMPDynamicFontSetupTool
    {
        private const string OutputDirectory = "Assets/TextMesh Pro/NANUM/DynamicFonts";

        private static readonly (string ttfPath, string assetName)[] DefaultKoreanFonts = new[]
        {
            ("Assets/TextMesh Pro/NANUM/Fonts/NanumSquareRoundB.ttf", "NanumSquareRoundB Dynamic SDF"),
            ("Assets/TextMesh Pro/NANUM/Fonts/NanumSquareRoundR.ttf", "NanumSquareRoundR Dynamic SDF"),
            ("Assets/TextMesh Pro/NANUM/Fonts/NanumBarunGothicBold.ttf", "NanumBarunGothicBold Dynamic SDF")
        };

        private static readonly string[] UIFontFolders = new[]
        {
            "Assets/External/Layer Lab/GUI Pro-SimpleCasual",
            "Assets/TextMesh Pro/Resources/Fonts & Materials"
        };

        [MenuItem("Tools/TextMesh Pro/한글 Dynamic 폰트 자동 생성 및 Fallback 일괄 등록", false, 100)]
        public static void ExecuteFullPipeline()
        {
            EditorFolderUtils.EnsureFolder(OutputDirectory);

            // 1. Dynamic Font Assets 생성 또는 로드
            var generatedAssets = new List<TMP_FontAsset>();
            foreach (var (ttfPath, assetName) in DefaultKoreanFonts)
            {
                var fontAsset = CreateOrGetDynamicFontAsset(ttfPath, OutputDirectory, assetName);
                if (fontAsset != null)
                {
                    generatedAssets.Add(fontAsset);
                }
            }

            if (generatedAssets.Count == 0)
            {
                Debug.LogError("[TMPDynamicFontSetupTool] Dynamic 폰트 에셋을 생성하지 못했습니다.");
                return;
            }

            // 2. TMP Settings 전역 등록
            int globalAdded = RegisterToGlobalTMPSettings(generatedAssets);

            // 3. UI 폰트에 Fallback 연결
            int uiLinked = LinkFallbackToUIFonts(generatedAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "한글 TMP 폰트 설정 완료",
                $"[완료 보고]\n" +
                $"- 생성/확인된 동적 한글 폰트: {generatedAssets.Count}개\n" +
                $"- TMP Settings 전역 등록: {globalAdded}개 추가됨\n" +
                $"- UI 전용 폰트 Fallback 연결: {uiLinked}개 폰트 에셋 연동됨\n\n" +
                $"이제 모든 UI 텍스트에서 한글이 깨지지 않고 정상 표시됩니다!",
                "확인"
            );

            Debug.Log($"<color=cyan>[TMPDynamicFontSetupTool]</color> 완료: Dynamic 폰트 {generatedAssets.Count}개, 전역 등록 {globalAdded}개, UI 폰트 연동 {uiLinked}개");
        }

        /// <summary>
        /// TTF/OTF 파일로부터 Dynamic TMP_FontAsset을 생성하거나 기존 것을 반환합니다.
        /// </summary>
        public static TMP_FontAsset CreateOrGetDynamicFontAsset(string ttfPath, string outputDir, string assetName = null)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[TMPDynamicFontSetupTool] 폰트 파일을 찾을 수 없습니다: {ttfPath}");
                return null;
            }

            if (string.IsNullOrEmpty(assetName))
            {
                assetName = $"{font.name} Dynamic SDF";
            }

            string savePath = $"{outputDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(savePath);
            if (existing != null)
            {
                return existing;
            }

            // Unity 6 Dynamic Font Asset 생성 (Multi-Atlas 활성화)
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true
            );

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, savePath);

            // 서브 에셋 (Material) 등록
            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{assetName} Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            // 서브 에셋 (Atlas Textures) 등록
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var tex = fontAsset.atlasTextures[i];
                    if (tex != null)
                    {
                        tex.name = $"{assetName} Atlas {i}";
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                    }
                }
            }

            EditorUtility.SetDirty(fontAsset);
            Debug.Log($"[TMPDynamicFontSetupTool] Dynamic 폰트 에셋 생성 완료: {savePath}");
            return fontAsset;
        }

        /// <summary>
        /// TMP Settings 에셋의 전역 Fallback 리스트에 한글 폰트를 등록합니다.
        /// </summary>
        public static int RegisterToGlobalTMPSettings(List<TMP_FontAsset> koreanFontAssets)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[TMPDynamicFontSetupTool] TMP_Settings.instance를 가져올 수 없습니다.");
                return 0;
            }

            var fallbackList = TMP_Settings.fallbackFontAssets;
            if (fallbackList == null)
            {
                var field = typeof(TMP_Settings).GetField("m_fallbackFontAssets",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fallbackList = new List<TMP_FontAsset>();
                field?.SetValue(settings, fallbackList);
            }

            int addedCount = 0;
            foreach (var asset in koreanFontAssets)
            {
                if (asset != null && !fallbackList.Contains(asset))
                {
                    fallbackList.Add(asset);
                    addedCount++;
                }
            }

            // 한글 줄바꿈 규칙 활성화
            TMP_Settings.useModernHangulLineBreakingRules = true;

            EditorUtility.SetDirty(settings);
            return addedCount;
        }

        /// <summary>
        /// GUI Pro-SimpleCasual 등 프로젝트 내 영문 UI 폰트 에셋의 Fallback 리스트에 한글 폰트를 연결합니다.
        /// </summary>
        public static int LinkFallbackToUIFonts(List<TMP_FontAsset> koreanFontAssets)
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", UIFontFolders);
            int linkedCount = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

                if (targetFont == null || koreanFontAssets.Contains(targetFont))
                    continue;

                if (targetFont.fallbackFontAssetTable == null)
                {
                    targetFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }

                bool changed = false;
                foreach (var asset in koreanFontAssets)
                {
                    if (asset != null && !targetFont.fallbackFontAssetTable.Contains(asset))
                    {
                        targetFont.fallbackFontAssetTable.Add(asset);
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(targetFont);
                    linkedCount++;
                }
            }

            return linkedCount;
        }

        // 프로젝트 창에서 TTF/OTF를 우클릭했을 때 바로 Dynamic TMP 폰트로 변환하는 기능
        [MenuItem("Assets/Create/TextMeshPro/Dynamic Font Asset (Auto Multi-Atlas)", false, 20)]
        private static void CreateDynamicFontAssetFromSelection()
        {
            var font = Selection.activeObject as Font;
            if (font == null)
            {
                EditorUtility.DisplayDialog("오류", "선택된 에셋이 Font(TTF/OTF)가 아닙니다.", "확인");
                return;
            }

            string fontPath = AssetDatabase.GetAssetPath(font);
            string folder = Path.GetDirectoryName(fontPath)?.Replace("\\", "/");
            var created = CreateOrGetDynamicFontAsset(fontPath, folder);

            if (created != null)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = created;
                EditorGUIUtility.PingObject(created);
            }
        }

        [MenuItem("Assets/Create/TextMeshPro/Dynamic Font Asset (Auto Multi-Atlas)", true)]
        private static bool ValidateCreateDynamicFontAssetFromSelection()
        {
            return Selection.activeObject is Font;
        }
    }
}
