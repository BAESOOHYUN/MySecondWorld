using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using MySecondWorld.UI;
using MySecondWorld.Customization;

namespace MySecondWorld.EditorTools
{
    public static class SetupCustomizationCodexUI
    {
        [MenuItem("Tools/Setup Customization Codex UI")]
        public static void Execute()
        {
            SetupPartThumbnailButtonPrefab();
            SetupColorChannelRowPrefab();

            string prefabPath = "Assets/03.Prefab/UI/Canvas/CharacterCustomizeCanvas.prefab";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            bool isStageOpen = stage != null && stage.assetPath == prefabPath;
            GameObject root = isStageOpen ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                // 1. 에셋 로드
                Sprite squareFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Square02_White1.png");
                Sprite circleFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Circle_116_White.png");
                Sprite bgGradient = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Demo/Demo_Background/Background_Gradatient02_Dark.png");
                Sprite platformSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Demo/Demo_Image/Image_Platform_Dark.png");
                Sprite sliderFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Slider/Slider_Demo_Dark/Slider05_Frame.png");
                Sprite sliderFill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Slider/Slider_Demo_Dark/Slider05_Fill_Blue.png");
                Sprite bookIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_ItemIcons/256/Itemicon_Book_Blue.Png");
                Sprite diceIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_ItemIcons/256/Itemicon_Dice_White.Png");
                Sprite refreshIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Refresh.Png");
                Sprite closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Symbol_X.Png");
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/NANUM/DynamicFonts/NanumBarunGothicBold Dynamic SDF.asset");
                RenderTexture previewRT = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/07.Textures/PreviewChracter Render Texture.renderTexture");

                // 2. CanvasScaler 설정: Match Height 1.0 (1280x720)
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1280f, 720f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1.0f;
                }

                CharacterCustomizeUI ui = root.GetComponent<CharacterCustomizeUI>();

                // 3. Panel (전체 배경) 리스타일
                Transform panelT = root.transform.Find("Panel");
                if (panelT == null)
                {
                    var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                    panelGO.transform.SetParent(root.transform, false);
                    panelT = panelGO.transform;
                }
                var panelRT = panelT.GetComponent<RectTransform>();
                panelRT.anchorMin = Vector2.zero;
                panelRT.anchorMax = Vector2.one;
                panelRT.sizeDelta = Vector2.zero;
                panelRT.anchoredPosition = Vector2.zero;
                var panelImg = panelT.GetComponent<Image>();
                if (bgGradient != null)
                {
                    panelImg.sprite = bgGradient;
                    panelImg.type = Image.Type.Simple;
                    panelImg.color = new Color(0.9f, 0.95f, 1f, 1f);
                }
                else
                {
                    panelImg.color = new Color(0.08f, 0.10f, 0.14f, 1f);
                }

                // 4. 상단 헤더 바 (Top Header Bar)
                Transform headerT = panelT.Find("TopHeader_Bar");
                if (headerT == null)
                {
                    var headerGO = new GameObject("TopHeader_Bar", typeof(RectTransform), typeof(Image));
                    headerGO.transform.SetParent(panelT, false);
                    headerT = headerGO.transform;
                }
                var headerRT = headerT.GetComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0f, 1f);
                headerRT.anchorMax = new Vector2(1f, 1f);
                headerRT.pivot = new Vector2(0.5f, 1f);
                headerRT.anchoredPosition = Vector2.zero;
                headerRT.sizeDelta = new Vector2(0f, 52f);
                var headerImg = headerT.GetComponent<Image>();
                headerImg.sprite = squareFrame;
                headerImg.type = Image.Type.Sliced;
                headerImg.color = new Color(0.07f, 0.09f, 0.13f, 0.94f);

                // 4-1. 타이틀 그룹: [도감 아이콘] + "외형 도감 (Wardrobe Codex)"
                Transform titleT = headerT.Find("Title_Group");
                if (titleT == null)
                {
                    var titleGO = new GameObject("Title_Group", typeof(RectTransform));
                    titleGO.transform.SetParent(headerT, false);
                    titleT = titleGO.transform;
                }
                var titleRT = titleT.GetComponent<RectTransform>();
                titleRT.anchorMin = new Vector2(0f, 0.5f);
                titleRT.anchorMax = new Vector2(0f, 0.5f);
                titleRT.pivot = new Vector2(0f, 0.5f);
                titleRT.anchoredPosition = new Vector2(25f, 0f);
                titleRT.sizeDelta = new Vector2(300f, 40f);

                var bookIconT = titleT.Find("Icon");
                if (bookIconT == null)
                {
                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGO.transform.SetParent(titleT, false);
                    bookIconT = iconGO.transform;
                }
                var bookIconRT = bookIconT.GetComponent<RectTransform>();
                bookIconRT.anchorMin = new Vector2(0f, 0.5f);
                bookIconRT.anchorMax = new Vector2(0f, 0.5f);
                bookIconRT.pivot = new Vector2(0f, 0.5f);
                bookIconRT.anchoredPosition = new Vector2(0f, 0f);
                bookIconRT.sizeDelta = new Vector2(32f, 32f);
                var bookIconImg = bookIconT.GetComponent<Image>();
                if (bookIcon != null) bookIconImg.sprite = bookIcon;
                bookIconImg.raycastTarget = false;

                var titleTextT = titleT.Find("Text");
                if (titleTextT == null)
                {
                    var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    textGO.transform.SetParent(titleT, false);
                    titleTextT = textGO.transform;
                }
                var titleTextRT = titleTextT.GetComponent<RectTransform>();
                titleTextRT.anchorMin = new Vector2(0f, 0.5f);
                titleTextRT.anchorMax = new Vector2(0f, 0.5f);
                titleTextRT.pivot = new Vector2(0f, 0.5f);
                titleTextRT.anchoredPosition = new Vector2(40f, 0f);
                titleTextRT.sizeDelta = new Vector2(260f, 30f);
                var titleTMP = titleTextT.GetComponent<TextMeshProUGUI>();
                if (font != null) titleTMP.font = font;
                titleTMP.text = "외형 도감 <size=12><color=#8EA0B8>WARDROBE CODEX</color></size>";
                titleTMP.fontSize = 18;
                titleTMP.fontStyle = FontStyles.Bold;
                titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
                titleTMP.color = new Color(0.95f, 0.92f, 0.85f, 1f);

                // 4-2. 수집률 그룹 (Collection Progress)
                Transform progT = headerT.Find("Progress_Group");
                if (progT == null)
                {
                    var progGO = new GameObject("Progress_Group", typeof(RectTransform));
                    progGO.transform.SetParent(headerT, false);
                    progT = progGO.transform;
                }
                var progRT = progT.GetComponent<RectTransform>();
                progRT.anchorMin = new Vector2(0.5f, 0.5f);
                progRT.anchorMax = new Vector2(0.5f, 0.5f);
                progRT.pivot = new Vector2(0.5f, 0.5f);
                progRT.anchoredPosition = new Vector2(40f, 0f);
                progRT.sizeDelta = new Vector2(340f, 36f);

                var progTextT = progT.Find("Text_Rate");
                if (progTextT == null)
                {
                    var ptGO = new GameObject("Text_Rate", typeof(RectTransform), typeof(TextMeshProUGUI));
                    ptGO.transform.SetParent(progT, false);
                    progTextT = ptGO.transform;
                }
                var progTextRT = progTextT.GetComponent<RectTransform>();
                progTextRT.anchorMin = new Vector2(0f, 0.5f);
                progTextRT.anchorMax = new Vector2(0f, 0.5f);
                progTextRT.pivot = new Vector2(0f, 0.5f);
                progTextRT.anchoredPosition = new Vector2(0f, 0f);
                progTextRT.sizeDelta = new Vector2(170f, 26f);
                var progTMP = progTextT.GetComponent<TextMeshProUGUI>();
                if (font != null) progTMP.font = font;
                progTMP.text = "도감 수집률: <color=#FFE680>0</color> / 0 (0%)";
                progTMP.fontSize = 13;
                progTMP.fontStyle = FontStyles.Bold;
                progTMP.alignment = TextAlignmentOptions.MidlineLeft;
                progTMP.color = new Color(0.82f, 0.89f, 0.98f, 0.95f);

                // Slider
                Transform sliderT = progT.Find("Slider");
                Slider sliderComp = null;
                if (sliderT == null)
                {
                    var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
                    sliderGO.transform.SetParent(progT, false);
                    sliderT = sliderGO.transform;
                    sliderComp = sliderGO.GetComponent<Slider>();

                    // Background
                    var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
                    bgGO.transform.SetParent(sliderT, false);
                    var bgRT = bgGO.GetComponent<RectTransform>();
                    bgRT.anchorMin = Vector2.zero;
                    bgRT.anchorMax = Vector2.one;
                    bgRT.sizeDelta = Vector2.zero;
                    var bImg = bgGO.GetComponent<Image>();
                    bImg.sprite = sliderFrame != null ? sliderFrame : squareFrame;
                    bImg.type = Image.Type.Sliced;
                    bImg.color = new Color(0.10f, 0.13f, 0.18f, 0.95f);

                    // Fill Area
                    var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
                    fillAreaGO.transform.SetParent(sliderT, false);
                    var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
                    fillAreaRT.anchorMin = Vector2.zero;
                    fillAreaRT.anchorMax = Vector2.one;
                    fillAreaRT.sizeDelta = new Vector2(-4f, -4f);

                    // Fill
                    var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                    fillGO.transform.SetParent(fillAreaGO.transform, false);
                    var fillRT = fillGO.GetComponent<RectTransform>();
                    fillRT.anchorMin = Vector2.zero;
                    fillRT.anchorMax = Vector2.one;
                    fillRT.sizeDelta = Vector2.zero;
                    var fImg = fillGO.GetComponent<Image>();
                    fImg.sprite = sliderFill != null ? sliderFill : squareFrame;
                    fImg.type = Image.Type.Sliced;
                    fImg.color = new Color(0.29f, 0.78f, 0.87f, 1f); // Cyan Blue

                    sliderComp.fillRect = fillRT;
                    sliderComp.targetGraphic = fImg;
                    sliderComp.direction = Slider.Direction.LeftToRight;
                    sliderComp.minValue = 0f;
                    sliderComp.maxValue = 100f;
                    sliderComp.value = 0f;
                }
                else
                {
                    sliderComp = sliderT.GetComponent<Slider>();
                }
                var sliderRT = sliderT.GetComponent<RectTransform>();
                sliderRT.anchorMin = new Vector2(1f, 0.5f);
                sliderRT.anchorMax = new Vector2(1f, 0.5f);
                sliderRT.pivot = new Vector2(1f, 0.5f);
                sliderRT.anchoredPosition = new Vector2(0f, 0f);
                sliderRT.sizeDelta = new Vector2(160f, 14f);

                // 4-3. 닫기 버튼 (ExitButton)
                Transform exitT = panelT.Find("ExitButton") ?? headerT.Find("ExitButton");
                Button exitBtn = null;
                if (exitT != null)
                {
                    exitT.SetParent(headerT, false);
                    var exitRT = exitT.GetComponent<RectTransform>();
                    exitRT.anchorMin = new Vector2(1f, 0.5f);
                    exitRT.anchorMax = new Vector2(1f, 0.5f);
                    exitRT.pivot = new Vector2(1f, 0.5f);
                    exitRT.anchoredPosition = new Vector2(-20f, 0f);
                    exitRT.sizeDelta = new Vector2(34f, 34f);
                    var exitImg = exitT.GetComponent<Image>();
                    if (closeIcon != null) exitImg.sprite = closeIcon;
                    exitImg.color = new Color(0.85f, 0.9f, 1f, 0.9f);
                    exitBtn = exitT.GetComponent<Button>();
                }

                // 5. 좌측 아바타 전시대 (Left Stage Frame)
                Transform leftStageT = panelT.Find("Left_Stage_Frame");
                if (leftStageT == null)
                {
                    var stageGO = new GameObject("Left_Stage_Frame", typeof(RectTransform), typeof(Image));
                    stageGO.transform.SetParent(panelT, false);
                    leftStageT = stageGO.transform;
                }
                var leftStageRT = leftStageT.GetComponent<RectTransform>();
                leftStageRT.anchorMin = new Vector2(0f, 0.5f);
                leftStageRT.anchorMax = new Vector2(0f, 0.5f);
                leftStageRT.pivot = new Vector2(0f, 0.5f);
                leftStageRT.anchoredPosition = new Vector2(25f, -24f);
                leftStageRT.sizeDelta = new Vector2(400f, 620f);
                var leftStageImg = leftStageT.GetComponent<Image>();
                leftStageImg.sprite = squareFrame;
                leftStageImg.type = Image.Type.Sliced;
                leftStageImg.color = new Color(0.10f, 0.13f, 0.18f, 0.96f);

                // 5-1. 전시대 상단 배너 타이틀
                Transform bannerT = leftStageT.Find("Banner_Title");
                if (bannerT == null)
                {
                    var bGO = new GameObject("Banner_Title", typeof(RectTransform), typeof(Image));
                    bGO.transform.SetParent(leftStageT, false);
                    bannerT = bGO.transform;
                }
                var bannerRT = bannerT.GetComponent<RectTransform>();
                bannerRT.anchorMin = new Vector2(0.5f, 1f);
                bannerRT.anchorMax = new Vector2(0.5f, 1f);
                bannerRT.pivot = new Vector2(0.5f, 1f);
                bannerRT.anchoredPosition = new Vector2(0f, -12f);
                bannerRT.sizeDelta = new Vector2(240f, 28f);
                var bannerImg = bannerT.GetComponent<Image>();
                bannerImg.sprite = circleFrame;
                bannerImg.type = Image.Type.Sliced;
                bannerImg.color = new Color(0.15f, 0.20f, 0.28f, 0.8f);

                var bannerTextT = bannerT.Find("Text");
                if (bannerTextT == null)
                {
                    var btGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    btGO.transform.SetParent(bannerT, false);
                    bannerTextT = btGO.transform;
                }
                var btRT = bannerTextT.GetComponent<RectTransform>();
                btRT.anchorMin = Vector2.zero;
                btRT.anchorMax = Vector2.one;
                btRT.sizeDelta = Vector2.zero;
                var btTMP = bannerTextT.GetComponent<TextMeshProUGUI>();
                if (font != null) btTMP.font = font;
                btTMP.text = "아바타 쇼룸";
                btTMP.fontSize = 13;
                btTMP.fontStyle = FontStyles.Bold;
                btTMP.alignment = TextAlignmentOptions.Center;
                btTMP.color = new Color(0.85f, 0.92f, 1f, 0.9f);

                // 5-2. 원형 발판 (Pedestal - 캐릭터 발밑으로 높이 조정)
                Transform pedestalT = leftStageT.Find("Image_Platform");
                if (pedestalT == null)
                {
                    var pedGO = new GameObject("Image_Platform", typeof(RectTransform), typeof(Image));
                    pedGO.transform.SetParent(leftStageT, false);
                    pedestalT = pedGO.transform;
                }
                var pedRT = pedestalT.GetComponent<RectTransform>();
                pedRT.anchorMin = new Vector2(0.5f, 0.5f);
                pedRT.anchorMax = new Vector2(0.5f, 0.5f);
                pedRT.pivot = new Vector2(0.5f, 0.5f);
                pedRT.anchoredPosition = new Vector2(0f, -160f);
                pedRT.sizeDelta = new Vector2(290f, 58f);
                var pedImg = pedestalT.GetComponent<Image>();
                if (platformSprite != null)
                {
                    pedImg.sprite = platformSprite;
                    pedImg.type = Image.Type.Simple;
                    pedImg.color = new Color(1f, 1f, 1f, 0.85f);
                }
                else
                {
                    pedImg.sprite = circleFrame;
                    pedImg.color = new Color(0.08f, 0.12f, 0.18f, 0.7f);
                }
                pedImg.raycastTarget = false;

                // 5-3. RawImage (3D 캐릭터 렌더 텍스처)
                Transform rawImageT = panelT.Find("RawImage") ?? leftStageT.Find("RawImage");
                if (rawImageT != null)
                {
                    rawImageT.SetParent(leftStageT, false);
                    var rRT = rawImageT.GetComponent<RectTransform>();
                    rRT.anchorMin = new Vector2(0.5f, 0.5f);
                    rRT.anchorMax = new Vector2(0.5f, 0.5f);
                    rRT.pivot = new Vector2(0.5f, 0.5f);
                    rRT.anchoredPosition = new Vector2(0f, 35f);
                    rRT.sizeDelta = new Vector2(370f, 440f);
                    var rawImg = rawImageT.GetComponent<RawImage>();
                    if (previewRT != null) rawImg.texture = previewRT;
                }

                // 발판을 3D 캐릭터 뒤쪽으로 배치
                pedestalT.SetSiblingIndex(1);

                // 5-4. 조작 안내 텍스트 (발판 아래, 하단 버튼 위 여백에 배치)
                Transform hintT = leftStageT.Find("Control_Hint");
                if (hintT == null)
                {
                    var hGO = new GameObject("Control_Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
                    hGO.transform.SetParent(leftStageT, false);
                    hintT = hGO.transform;
                }
                var hintRT = hintT.GetComponent<RectTransform>();
                hintRT.anchorMin = new Vector2(0.5f, 0.5f);
                hintRT.anchorMax = new Vector2(0.5f, 0.5f);
                hintRT.pivot = new Vector2(0.5f, 0.5f);
                hintRT.anchoredPosition = new Vector2(0f, -195f);
                hintRT.sizeDelta = new Vector2(300f, 18f);
                var hintTMP = hintT.GetComponent<TextMeshProUGUI>();
                if (font != null) hintTMP.font = font;
                hintTMP.text = "마우스 드래그: 회전  |  휠 스크롤: 줌";
                hintTMP.fontSize = 10.5f;
                hintTMP.alignment = TextAlignmentOptions.Center;
                hintTMP.color = new Color(0.55f, 0.65f, 0.78f, 0.75f);
                hintTMP.raycastTarget = false;

                // 5-5. 좌측 전시대 하단 컨트롤 툴바
                Transform rightPanelsT = panelT.Find("Right Panels") ?? panelT.Find("Right_Codex_Book");
                Transform topButtonsT = rightPanelsT != null ? rightPanelsT.Find("Top Buttons") : null;

                Button maleBtn = null;
                Button femaleBtn = null;
                Button randomBtn = null;
                Button resetBtn = null;
                Button saveBtn = null;

                Transform maleT = leftStageT.Find("Male") ?? (topButtonsT != null ? topButtonsT.Find("Male") : null);
                Transform femaleT = leftStageT.Find("Female") ?? (topButtonsT != null ? topButtonsT.Find("Female") : null);
                Transform randomT = leftStageT.Find("Random") ?? (topButtonsT != null ? topButtonsT.Find("Random") : null);
                Transform resetT = leftStageT.Find("Reset") ?? (topButtonsT != null ? topButtonsT.Find("Reset") : null);
                Transform saveT = leftStageT.Find("Save") ?? (topButtonsT != null ? topButtonsT.Find("Save") : null);

                if (maleT != null)
                {
                    maleT.SetParent(leftStageT, false);
                    var rt = maleT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(-125f, 75f);
                    rt.sizeDelta = new Vector2(65f, 32f);
                    StyleButton(maleT.gameObject, "♂ 남성", squareFrame, font, new Color(0.16f, 0.24f, 0.38f, 0.95f), new Color(0.85f, 0.92f, 1f, 1f), 12);
                    maleBtn = maleT.GetComponent<Button>();
                }

                if (femaleT != null)
                {
                    femaleT.SetParent(leftStageT, false);
                    var rt = femaleT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(-55f, 75f);
                    rt.sizeDelta = new Vector2(65f, 32f);
                    StyleButton(femaleT.gameObject, "♀ 여성", squareFrame, font, new Color(0.16f, 0.24f, 0.38f, 0.95f), new Color(0.85f, 0.92f, 1f, 1f), 12);
                    femaleBtn = femaleT.GetComponent<Button>();
                }

                if (randomT != null)
                {
                    randomT.SetParent(leftStageT, false);
                    var rt = randomT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(35f, 75f);
                    rt.sizeDelta = new Vector2(90f, 32f);
                    StyleButton(randomT.gameObject, "주사위", squareFrame, font, new Color(0.20f, 0.18f, 0.28f, 0.95f), new Color(0.95f, 0.88f, 1f, 1f), 12);
                    randomBtn = randomT.GetComponent<Button>();
                }

                if (resetT != null)
                {
                    resetT.SetParent(leftStageT, false);
                    var rt = resetT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(135f, 75f);
                    rt.sizeDelta = new Vector2(80f, 32f);
                    StyleButton(resetT.gameObject, "초기화", squareFrame, font, new Color(0.24f, 0.16f, 0.16f, 0.95f), new Color(1f, 0.85f, 0.85f, 1f), 12);
                    resetBtn = resetT.GetComponent<Button>();
                }

                if (saveT != null)
                {
                    saveT.SetParent(leftStageT, false);
                    var rt = saveT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 18f);
                    rt.sizeDelta = new Vector2(360f, 44f);
                    StyleButton(saveT.gameObject, "외형 저장 & 도감 적용", squareFrame, font, new Color(0.92f, 0.68f, 0.22f, 1f), new Color(0.15f, 0.10f, 0.03f, 1f), 15);
                    saveBtn = saveT.GetComponent<Button>();
                }

                // 6. 우측 도감 서가 (Right Codex Book)
                Transform rightBookT = rightPanelsT;
                if (rightBookT != null)
                {
                    rightBookT.name = "Right_Codex_Book";
                    var rt = rightBookT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-25f, -24f);
                    rt.sizeDelta = new Vector2(810f, 620f);

                    var img = rightBookT.GetComponent<Image>();
                    img.sprite = squareFrame;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.09f, 0.12f, 0.17f, 0.98f);
                }

                // 6-1. 상단 대분류 탭 (Top Taps: 의상&파츠 / 장신구 / 염색 공방)
                Transform topTabsT = rightBookT.Find("Top Taps");
                Toggle partsTopTab = null;
                Toggle accTopTab = null;
                Toggle colorTopTab = null;
                if (topTabsT != null)
                {
                    var rt = topTabsT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -12f);
                    rt.sizeDelta = new Vector2(-30f, 40f);

                    var hl = topTabsT.GetComponent<HorizontalLayoutGroup>();
                    if (hl != null)
                    {
                        hl.spacing = 15f;
                        hl.childAlignment = TextAnchor.MiddleLeft;
                        hl.childForceExpandWidth = false;
                        hl.childForceExpandHeight = true;
                    }

                    var topToggleGroup = topTabsT.GetComponent<ToggleGroup>();
                    if (topToggleGroup == null) topToggleGroup = topTabsT.gameObject.AddComponent<ToggleGroup>();
                    topToggleGroup.allowSwitchOff = false;

                    var pTab = topTabsT.Find("PartsTopTab");
                    var aTab = topTabsT.Find("AccessoryTopTab");
                    var cTab = topTabsT.Find("ColorTopTab");

                    if (pTab != null)
                    {
                        pTab.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 38f);
                        StyleToggle(pTab.gameObject, "의상 & 파츠", circleFrame, font, new Color(0.16f, 0.22f, 0.32f, 0.95f), 13);
                        partsTopTab = pTab.GetComponent<Toggle>();
                        if (partsTopTab != null)
                        {
                            partsTopTab.group = topToggleGroup;
                            partsTopTab.isOn = true;
                        }
                    }
                    if (aTab != null)
                    {
                        aTab.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 38f);
                        StyleToggle(aTab.gameObject, "장신구", circleFrame, font, new Color(0.16f, 0.22f, 0.32f, 0.95f), 13);
                        accTopTab = aTab.GetComponent<Toggle>();
                        if (accTopTab != null)
                        {
                            accTopTab.group = topToggleGroup;
                            accTopTab.isOn = false;
                        }
                    }
                    if (cTab != null)
                    {
                        cTab.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 38f);
                        StyleToggle(cTab.gameObject, "염색 공방", circleFrame, font, new Color(0.16f, 0.22f, 0.32f, 0.95f), 13);
                        colorTopTab = cTab.GetComponent<Toggle>();
                        if (colorTopTab != null)
                        {
                            colorTopTab.group = topToggleGroup;
                            colorTopTab.isOn = false;
                        }
                    }
                }

                // 6-2. 파츠 탭 그룹 (PartsTabGroup - 좌측 책갈피 컬럼)
                Transform partsTabGroupT = rightBookT.Find("PartsTabGroup");
                var partsEntries = new List<PartTabEntry>();
                if (partsTabGroupT != null)
                {
                    partsTabGroupT.gameObject.SetActive(true);
                    var rt = partsTabGroupT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.offsetMin = new Vector2(15f, 15f);
                    rt.offsetMax = new Vector2(140f, -65f);

                    var img = partsTabGroupT.GetComponent<Image>();
                    img.sprite = squareFrame;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.06f, 0.08f, 0.12f, 0.7f);

                    var sr = partsTabGroupT.GetComponent<ScrollRect>();
                    ConfigureScrollRect(sr, squareFrame, hideVertical: true);

                    Transform content = partsTabGroupT.Find("Viewport/Content");
                    if (content != null)
                    {
                        var vl = content.GetComponent<VerticalLayoutGroup>();
                        if (vl != null)
                        {
                            vl.spacing = 6f;
                            vl.padding = new RectOffset(6, 6, 8, 8);
                            vl.childControlWidth = true;
                            vl.childForceExpandWidth = true;
                            vl.childControlHeight = false;
                            vl.childForceExpandHeight = false;
                        }

                        var group = content.GetComponent<ToggleGroup>();
                        if (group == null) group = content.gameObject.AddComponent<ToggleGroup>();
                        group.allowSwitchOff = false;

                        var partLabels = new (string childName, PartType part, string label, string iconPath)[]
                        {
                            ("Hair",       PartType.Hair,          "헤어",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_User_1.Png"),
                            ("Head",       PartType.Head,          "얼굴",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_User_2.Png"),
                            ("Eyebrows",   PartType.Eyebrows,      "눈썹",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_User_3.Png"),
                            ("FacialHair", PartType.FacialHair,    "수염",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_User_4.Png"),
                            ("Torso",      PartType.Torso,         "상의",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Armor.Png"),
                            ("ArmUpper",   PartType.ArmUpperLeft,  "상완",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_ArmUpper.png"),
                            ("ArmLower",   PartType.ArmLowerLeft,  "하완",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_ArmLower.png"),
                            ("Hand",       PartType.HandLeft,      "장갑",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Hand.Png"),
                            ("Hips",       PartType.Hips,          "하의",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Pants.png"),
                            ("Leg",        PartType.LegLeft,       "다리",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Greaves.png"),
                            ("Ear",        PartType.Ear,           "귀",       "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Earing.Png")
                        };

                        foreach (var def in partLabels)
                        {
                            var child = content.Find(def.childName);
                            if (child != null)
                            {
                                var childRT = child.GetComponent<RectTransform>();
                                childRT.anchorMin = new Vector2(0f, 1f);
                                childRT.anchorMax = new Vector2(1f, 1f);
                                childRT.pivot = new Vector2(0.5f, 1f);
                                childRT.sizeDelta = new Vector2(0f, 36f);
                                StyleBookmarkToggle(child.gameObject, def.label, def.iconPath, squareFrame, font);
                                var tog = child.GetComponent<Toggle>();
                                if (tog != null)
                                {
                                    tog.group = group;
                                    tog.isOn = (def.part == PartType.Hair);
                                }
                                partsEntries.Add(new PartTabEntry { Part = def.part, Toggle = tog });
                            }
                        }
                    }
                }

                // 6-3. 장신구 탭 그룹 (AccessoryTabGroup - 좌측 책갈피 컬럼)
                Transform accTabGroupT = rightBookT.Find("AccessoryTabGroup");
                var accEntries = new List<PartTabEntry>();
                if (accTabGroupT != null)
                {
                    accTabGroupT.gameObject.SetActive(false); // Default inactive in prefab
                    var rt = accTabGroupT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.offsetMin = new Vector2(15f, 15f);
                    rt.offsetMax = new Vector2(140f, -65f);

                    var img = accTabGroupT.GetComponent<Image>();
                    img.sprite = squareFrame;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.06f, 0.08f, 0.12f, 0.7f);

                    var sr = accTabGroupT.GetComponent<ScrollRect>();
                    ConfigureScrollRect(sr, squareFrame, hideVertical: true);

                    Transform content = accTabGroupT.Find("Viewport/Content");
                    if (content != null)
                    {
                        var vl = content.GetComponent<VerticalLayoutGroup>();
                        if (vl != null)
                        {
                            vl.spacing = 6f;
                            vl.padding = new RectOffset(6, 6, 8, 8);
                            vl.childControlWidth = true;
                            vl.childForceExpandWidth = true;
                            vl.childControlHeight = false;
                            vl.childForceExpandHeight = false;
                        }

                        var group = content.GetComponent<ToggleGroup>();
                        if (group == null) group = content.gameObject.AddComponent<ToggleGroup>();
                        group.allowSwitchOff = false;

                        var accLabels = new (string childName, PartType part, string label, string iconPath)[]
                        {
                            ("HeadCoverings",   PartType.HeadCoverings,       "모자/투구", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_HelmetAttach.png"),
                            ("BackAttachment",  PartType.BackAttachment,      "등/망토",   "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Wing.Png"),
                            ("ShoulderAttach",  PartType.ShoulderAttachLeft,  "어깨",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Shoulder.png"),
                            ("ElbowAttach",     PartType.ElbowAttachLeft,     "팔꿈치",   "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Elbow.png"),
                            ("KneeAttach",      PartType.KneeAttachLeft,      "무릎",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Greaves.png"),
                            ("HelmetAttachment",PartType.HelmetAttachment,    "투구장식", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_HelmetAttach.png"),
                            ("HipsAttachment",  PartType.HipsAttachment,      "허리장식", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Belt.png")
                        };

                        foreach (var def in accLabels)
                        {
                            var child = content.Find(def.childName);
                            if (child != null)
                            {
                                var childRT = child.GetComponent<RectTransform>();
                                childRT.anchorMin = new Vector2(0f, 1f);
                                childRT.anchorMax = new Vector2(1f, 1f);
                                childRT.pivot = new Vector2(0.5f, 1f);
                                childRT.sizeDelta = new Vector2(0f, 36f);
                                StyleBookmarkToggle(child.gameObject, def.label, def.iconPath, squareFrame, font);
                                var tog = child.GetComponent<Toggle>();
                                if (tog != null)
                                {
                                    tog.group = group;
                                    tog.isOn = (def.part == PartType.HeadCoverings);
                                }
                                accEntries.Add(new PartTabEntry { Part = def.part, Toggle = tog });
                            }
                        }
                    }
                }

                // 6-4. 썸네일 그리드 스크롤 뷰 (Thumbnail Scroll View)
                Transform thumbScrollT = rightBookT.Find("Thumbnail Scroll View");
                ScrollRect thumbScrollRect = null;
                Transform thumbContent = null;
                if (thumbScrollT != null)
                {
                    thumbScrollT.gameObject.SetActive(true);
                    var rt = thumbScrollT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = new Vector2(150f, 130f);
                    rt.offsetMax = new Vector2(-20f, -65f);

                    var img = thumbScrollT.GetComponent<Image>();
                    img.sprite = squareFrame;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.06f, 0.08f, 0.12f, 0.5f);

                    thumbScrollRect = thumbScrollT.GetComponent<ScrollRect>();
                    ConfigureScrollRect(thumbScrollRect, squareFrame, hideVertical: false);

                    thumbContent = thumbScrollT.Find("Viewport/Content");
                    if (thumbContent != null)
                    {
                        var grid = thumbContent.GetComponent<GridLayoutGroup>();
                        if (grid != null)
                        {
                            grid.cellSize = new Vector2(86f, 98f);
                            grid.spacing = new Vector2(10f, 10f);
                            grid.padding = new RectOffset(10, 10, 10, 10);
                        }
                    }
                }

                // 6-5. 하단 도감 상세 정보 카드 (Item Detail Card)
                Transform detailCardT = rightBookT.Find("Item_Detail_Card");
                if (detailCardT == null)
                {
                    var dcGO = new GameObject("Item_Detail_Card", typeof(RectTransform), typeof(Image));
                    dcGO.transform.SetParent(rightBookT, false);
                    detailCardT = dcGO.transform;
                }
                detailCardT.gameObject.SetActive(true);
                var dcRT = detailCardT.GetComponent<RectTransform>();
                dcRT.anchorMin = new Vector2(0f, 0f);
                dcRT.anchorMax = new Vector2(1f, 0f);
                dcRT.pivot = new Vector2(0.5f, 0f);
                dcRT.offsetMin = new Vector2(150f, 15f);
                dcRT.offsetMax = new Vector2(-20f, 122f); // Height: 107px
                var dcImg = detailCardT.GetComponent<Image>();
                dcImg.sprite = squareFrame;
                dcImg.type = Image.Type.Sliced;
                dcImg.color = new Color(0.12f, 0.16f, 0.23f, 0.98f);

                // Detail Card 요소들
                // 1. Name
                Transform nameT = detailCardT.Find("Text_ItemName");
                if (nameT == null)
                {
                    var nGO = new GameObject("Text_ItemName", typeof(RectTransform), typeof(TextMeshProUGUI));
                    nGO.transform.SetParent(detailCardT, false);
                    nameT = nGO.transform;
                }
                var nameRT = nameT.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0f, 1f);
                nameRT.anchorMax = new Vector2(0f, 1f);
                nameRT.pivot = new Vector2(0f, 1f);
                nameRT.anchoredPosition = new Vector2(20f, -14f);
                nameRT.sizeDelta = new Vector2(220f, 24f);
                var nameTMP = nameT.GetComponent<TextMeshProUGUI>();
                if (font != null) nameTMP.font = font;
                nameTMP.text = "아이템 이름";
                nameTMP.fontSize = 15;
                nameTMP.fontStyle = FontStyles.Bold;
                nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
                nameTMP.color = Color.white;

                // 2. Rarity
                Transform rarityT = detailCardT.Find("Text_Rarity");
                if (rarityT == null)
                {
                    var rGO = new GameObject("Text_Rarity", typeof(RectTransform), typeof(TextMeshProUGUI));
                    rGO.transform.SetParent(detailCardT, false);
                    rarityT = rGO.transform;
                }
                var rarityRT = rarityT.GetComponent<RectTransform>();
                rarityRT.anchorMin = new Vector2(0f, 1f);
                rarityRT.anchorMax = new Vector2(0f, 1f);
                rarityRT.pivot = new Vector2(0f, 1f);
                rarityRT.anchoredPosition = new Vector2(245f, -14f);
                rarityRT.sizeDelta = new Vector2(110f, 24f);
                var rarityTMP = rarityT.GetComponent<TextMeshProUGUI>();
                if (font != null) rarityTMP.font = font;
                rarityTMP.text = "[기본]";
                rarityTMP.fontSize = 13;
                rarityTMP.fontStyle = FontStyles.Bold;
                rarityTMP.alignment = TextAlignmentOptions.MidlineLeft;
                rarityTMP.color = new Color(0.85f, 0.9f, 1f, 0.9f);

                // 3. Status
                Transform statusT = detailCardT.Find("Text_Status");
                if (statusT == null)
                {
                    var sGO = new GameObject("Text_Status", typeof(RectTransform), typeof(TextMeshProUGUI));
                    sGO.transform.SetParent(detailCardT, false);
                    statusT = sGO.transform;
                }
                var statusRT = statusT.GetComponent<RectTransform>();
                statusRT.anchorMin = new Vector2(1f, 1f);
                statusRT.anchorMax = new Vector2(1f, 1f);
                statusRT.pivot = new Vector2(1f, 1f);
                statusRT.anchoredPosition = new Vector2(-20f, -14f);
                statusRT.sizeDelta = new Vector2(220f, 24f);
                var statusTMP = statusT.GetComponent<TextMeshProUGUI>();
                if (font != null) statusTMP.font = font;
                statusTMP.text = "● 수집 완료";
                statusTMP.fontSize = 12;
                statusTMP.fontStyle = FontStyles.Bold;
                statusTMP.alignment = TextAlignmentOptions.MidlineRight;
                statusTMP.color = new Color(0.33f, 0.91f, 0.56f, 1f);

                // 4. Divider Line
                Transform divT = detailCardT.Find("Divider_Line");
                if (divT == null)
                {
                    var dGO = new GameObject("Divider_Line", typeof(RectTransform), typeof(Image));
                    dGO.transform.SetParent(detailCardT, false);
                    divT = dGO.transform;
                }
                var divRT = divT.GetComponent<RectTransform>();
                divRT.anchorMin = new Vector2(0f, 1f);
                divRT.anchorMax = new Vector2(1f, 1f);
                divRT.pivot = new Vector2(0.5f, 1f);
                divRT.anchoredPosition = new Vector2(0f, -38f);
                divRT.sizeDelta = new Vector2(-40f, 1f);
                var divImg = divT.GetComponent<Image>();
                divImg.color = new Color(0.24f, 0.32f, 0.44f, 0.7f);
                divImg.raycastTarget = false;

                // 5. Desc
                Transform descT = detailCardT.Find("Text_Desc");
                if (descT == null)
                {
                    var dGO = new GameObject("Text_Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
                    dGO.transform.SetParent(detailCardT, false);
                    descT = dGO.transform;
                }
                var descRT = descT.GetComponent<RectTransform>();
                descRT.anchorMin = new Vector2(0f, 0f);
                descRT.anchorMax = new Vector2(1f, 1f);
                descRT.pivot = new Vector2(0.5f, 0.5f);
                descRT.offsetMin = new Vector2(20f, 10f);
                descRT.offsetMax = new Vector2(-20f, -44f);
                var descTMP = descT.GetComponent<TextMeshProUGUI>();
                if (font != null) descTMP.font = font;
                descTMP.text = "파츠에 대한 상세 설명이 표시됩니다.";
                descTMP.fontSize = 12;
                descTMP.alignment = TextAlignmentOptions.TopLeft;
                descTMP.color = new Color(0.72f, 0.82f, 0.94f, 0.9f);
                descTMP.enableWordWrapping = true;

                // 6-6. 염색 공방 패널 (ColorTabGroup)
                Transform colorTabGroupT = rightBookT.Find("ColorTabGroup");
                Transform colorRowParentT = null;
                FlexibleColorPicker fcpComp = null;
                if (colorTabGroupT != null)
                {
                    colorTabGroupT.gameObject.SetActive(false); // Default inactive
                    var rt = colorTabGroupT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = new Vector2(15f, 15f);
                    rt.offsetMax = new Vector2(-20f, -65f);
                    var img = colorTabGroupT.GetComponent<Image>();
                    img.color = new Color(0f, 0f, 0f, 0f); // 투명

                    // ColorRowParent (좌측 스크롤)
                    var crpT = colorTabGroupT.Find("ColorRowParent ");
                    if (crpT != null)
                    {
                        var crpRT = crpT.GetComponent<RectTransform>();
                        crpRT.anchorMin = new Vector2(0f, 0f);
                        crpRT.anchorMax = new Vector2(0f, 1f);
                        crpRT.pivot = new Vector2(0f, 0.5f);
                        crpRT.anchoredPosition = new Vector2(0f, 0f);
                        crpRT.sizeDelta = new Vector2(300f, 0f);
                        var crpImg = crpT.GetComponent<Image>();
                        crpImg.sprite = squareFrame;
                        crpImg.type = Image.Type.Sliced;
                        crpImg.color = new Color(0.06f, 0.08f, 0.12f, 0.7f);

                        var crpSR = crpT.GetComponent<ScrollRect>();
                        ConfigureScrollRect(crpSR, squareFrame, hideVertical: false);

                        colorRowParentT = crpT.Find("Viewport/Content");
                        if (colorRowParentT != null)
                        {
                            var vl = colorRowParentT.GetComponent<VerticalLayoutGroup>();
                            if (vl != null)
                            {
                                vl.spacing = 5f;
                                vl.padding = new RectOffset(6, 6, 8, 8);
                                vl.childControlWidth = true;
                                vl.childForceExpandWidth = true;
                                vl.childControlHeight = false;
                                vl.childForceExpandHeight = false;
                            }
                        }
                    }

                    // FlexibleColorPicker (우측 일체형)
                    var fcpT = colorTabGroupT.Find("FlexibleColorPicker");
                    if (fcpT != null)
                    {
                        fcpT.gameObject.SetActive(false);
                        var fcpRT = fcpT.GetComponent<RectTransform>();
                        fcpRT.anchorMin = new Vector2(0f, 0f);
                        fcpRT.anchorMax = new Vector2(1f, 1f);
                        fcpRT.pivot = new Vector2(0.5f, 0.5f);
                        fcpRT.offsetMin = new Vector2(320f, 0f);
                        fcpRT.offsetMax = new Vector2(0f, 0f);
                        var fcpImg = fcpT.GetComponent<Image>();
                        fcpImg.sprite = squareFrame;
                        fcpImg.type = Image.Type.Sliced;
                        fcpImg.color = new Color(0.12f, 0.16f, 0.23f, 0.98f);
                        fcpComp = fcpT.GetComponent<FlexibleColorPicker>();
                    }
                }

                // 7. Reflection을 활용해 CharacterCustomizeUI 필드 완벽 바인딩
                var tUI = typeof(CharacterCustomizeUI);
                tUI.GetField("_partsTopTab", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, partsTopTab);
                tUI.GetField("_accessoryTopTab", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, accTopTab);
                tUI.GetField("_colorTopTab", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, colorTopTab);
                tUI.GetField("_partsTabGroup", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, partsTabGroupT?.gameObject);
                tUI.GetField("_accessoryTabGroup", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, accTabGroupT?.gameObject);
                tUI.GetField("_colorPanel", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, colorTabGroupT?.gameObject);

                tUI.GetField("_partsTabEntries", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, partsEntries.ToArray());
                tUI.GetField("_accessoryTabEntries", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, accEntries.ToArray());

                tUI.GetField("_thumbnailScrollRect", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, thumbScrollRect);
                tUI.GetField("_thumbnailContent", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, thumbContent);

                // 도감 신규 필드
                tUI.GetField("_collectionRateText", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, progTMP);
                tUI.GetField("_collectionProgressBar", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, sliderComp);
                tUI.GetField("_detailCardRoot", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, detailCardT?.gameObject);
                tUI.GetField("_detailCardName", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, nameTMP);
                tUI.GetField("_detailCardRarity", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, rarityTMP);
                tUI.GetField("_detailCardStatus", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, statusTMP);
                tUI.GetField("_detailCardDesc", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, descTMP);

                // 색상 및 버튼
                tUI.GetField("_colorRowParent", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, colorRowParentT);
                tUI.GetField("_colorPicker", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, fcpComp);

                tUI.GetField("_maleButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, maleBtn);
                tUI.GetField("_femaleButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, femaleBtn);
                tUI.GetField("_saveButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, saveBtn);
                tUI.GetField("_randomButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, randomBtn);
                tUI.GetField("_resetButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, resetBtn);
                tUI.GetField("_exitButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ui, exitBtn);

                if (isStageOpen)
                {
                    EditorUtility.SetDirty(root);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SetupCustomizationCodexUI] CharacterCustomizeCanvas 도감 리디자인 프리팹 구축 완료!");

                // ProjectLifetimeScope 내 중첩 인스턴스 동기화 (오버라이드로 인한 레이아웃 깨짐 방지)
                SyncProjectLifetimeScope();

                // 프리뷰 렌더링 캡처
                CapturePreview("customization_codex_rendered.png");
                CaptureColorTabPreview("customization_colortab_rendered.png");
            }
            finally
            {
                if (!isStageOpen)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void SyncProjectLifetimeScope()
        {
            string scopePath = "Assets/03.Prefab/LifetimeScope/ProjectLifetimeScope.prefab";
            var scopeRoot = PrefabUtility.LoadPrefabContents(scopePath);
            try
            {
                var scopeCanvas = scopeRoot.transform.Find("UIManager/CharacterCustomizeCanvas");
                if (scopeCanvas != null)
                {
                    PrefabUtility.RevertPrefabInstance(scopeCanvas.gameObject, InteractionMode.AutomatedAction);
                    EditorUtility.SetDirty(scopeRoot);
                    PrefabUtility.SaveAsPrefabAsset(scopeRoot, scopePath);
                    Debug.Log("[SetupCustomizationCodexUI] ProjectLifetimeScope 내 CharacterCustomizeCanvas 오버라이드 동기화 완료!");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(scopeRoot);
            }
        }

        private static void ConfigureScrollRect(ScrollRect sr, Sprite frameSprite, bool hideVertical = false)
        {
            if (sr == null) return;
            sr.horizontal = false;

            // Deactivate horizontal scrollbar
            Transform hBar = sr.transform.Find("Scrollbar Horizontal");
            if (hBar != null)
            {
                sr.horizontalScrollbar = null;
                hBar.gameObject.SetActive(false);
            }

            // Configure vertical scrollbar
            Transform vBar = sr.transform.Find("Scrollbar Vertical");
            if (vBar != null)
            {
                if (hideVertical)
                {
                    sr.verticalScrollbar = null;
                    vBar.gameObject.SetActive(false);
                }
                else
                {
                    var vScrollbar = vBar.GetComponent<Scrollbar>();
                    if (vScrollbar != null)
                    {
                        sr.verticalScrollbar = vScrollbar;
                        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                        var vBarRT = vBar.GetComponent<RectTransform>();
                        vBarRT.anchorMin = new Vector2(1f, 0f);
                        vBarRT.anchorMax = new Vector2(1f, 1f);
                        vBarRT.pivot = new Vector2(1f, 1f);
                        vBarRT.anchoredPosition = new Vector2(-2f, 0f);
                        vBarRT.sizeDelta = new Vector2(6f, 0f);

                        var vBarImg = vBar.GetComponent<Image>();
                        if (vBarImg != null)
                        {
                            vBarImg.sprite = frameSprite;
                            vBarImg.type = Image.Type.Sliced;
                            vBarImg.color = new Color(0.08f, 0.10f, 0.14f, 0.6f);
                        }

                        Transform handle = vBar.Find("Sliding Area/Handle");
                        if (handle != null)
                        {
                            var handleRT = handle.GetComponent<RectTransform>();
                            if (handleRT != null)
                            {
                                handleRT.sizeDelta = Vector2.zero;
                            }
                            var hImg = handle.GetComponent<Image>();
                            if (hImg != null)
                            {
                                hImg.sprite = frameSprite;
                                hImg.type = Image.Type.Sliced;
                                hImg.color = new Color(0.35f, 0.45f, 0.60f, 0.8f);
                            }
                        }
                    }
                }
            }

            Transform viewport = sr.transform.Find("Viewport");
            if (viewport != null)
            {
                var vpRT = viewport.GetComponent<RectTransform>();
                vpRT.anchorMin = Vector2.zero;
                vpRT.anchorMax = Vector2.one;
                vpRT.offsetMin = Vector2.zero;
                vpRT.offsetMax = hideVertical ? Vector2.zero : new Vector2(-10f, 0f);
            }
        }

        private static GameObject CreateBorderFrame(Transform parent, string name, float thickness, Color color)
        {
            var frameGO = new GameObject(name, typeof(RectTransform));
            frameGO.transform.SetParent(parent, false);
            var frameRT = frameGO.GetComponent<RectTransform>();
            frameRT.anchorMin = Vector2.zero;
            frameRT.anchorMax = Vector2.one;
            frameRT.offsetMin = Vector2.zero;
            frameRT.offsetMax = Vector2.zero;

            // Top
            CreateEdge(frameGO.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thickness), color);
            // Bottom
            CreateEdge(frameGO.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
            // Left
            CreateEdge(frameGO.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thickness, 0f), color);
            // Right
            CreateEdge(frameGO.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thickness, 0f), color);

            return frameGO;
        }

        private static void CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var edgeGO = new GameObject(name, typeof(RectTransform), typeof(Image));
            edgeGO.transform.SetParent(parent, false);
            var rt = edgeGO.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = edgeGO.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void StyleButton(GameObject go, string text, Sprite sprite, TMP_FontAsset font, Color bgColor, Color textColor, float fontSize)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                if (font != null) tmp.font = font;
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = textColor;
            }
        }

        private static void StyleToggle(GameObject go, string text, Sprite sprite, TMP_FontAsset font, Color bgColor, float fontSize)
        {
            var toggle = go.GetComponent<Toggle>();
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            if (toggle != null)
            {
                toggle.targetGraphic = img;
                var colors = toggle.colors;
                colors.normalColor = bgColor;
                colors.highlightedColor = new Color(0.22f, 0.32f, 0.48f, 1f);
                colors.pressedColor = new Color(0.12f, 0.16f, 0.22f, 1f);
                colors.selectedColor = new Color(0.26f, 0.42f, 0.68f, 1f);
                toggle.colors = colors;
            }

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                if (font != null) tmp.font = font;
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.92f, 0.96f, 1f, 0.95f);
            }
        }

        private static void StyleBookmarkToggle(GameObject go, string label, string iconPath, Sprite sprite, TMP_FontAsset font)
        {
            var toggle = go.GetComponent<Toggle>();
            var img = go.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);
            }

            if (toggle != null)
            {
                toggle.targetGraphic = img;
                var colors = toggle.colors;
                colors.normalColor = new Color(0.12f, 0.16f, 0.24f, 0.9f);
                colors.highlightedColor = new Color(0.18f, 0.26f, 0.38f, 1f);
                colors.pressedColor = new Color(0.10f, 0.14f, 0.20f, 1f);
                colors.selectedColor = new Color(0.22f, 0.38f, 0.62f, 1f);
                toggle.colors = colors;
            }

            // Checkmark highlight
            Transform checkmark = go.transform.Find("Background/Checkmark") ?? go.transform.Find("Checkmark");
            if (checkmark != null)
            {
                var cImg = checkmark.GetComponent<Image>();
                if (cImg != null)
                {
                    cImg.sprite = sprite;
                    cImg.type = Image.Type.Sliced;
                    cImg.color = new Color(0.28f, 0.52f, 0.85f, 0.75f);
                }
            }

            // Icon
            var iconT = go.transform.Find("Bookmark_Icon");
            if (iconT == null)
            {
                var iconGO = new GameObject("Bookmark_Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(go.transform, false);
                iconT = iconGO.transform;
            }
            var iconRT = iconT.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(8f, 0f);
            iconRT.sizeDelta = new Vector2(22f, 22f);
            var iconImg = iconT.GetComponent<Image>();
            if (!string.IsNullOrEmpty(iconPath))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (s != null) iconImg.sprite = s;
            }
            iconImg.color = new Color(0.85f, 0.92f, 1f, 0.9f);
            iconImg.raycastTarget = false;

            // Text
            var textT = go.transform.Find("Text (TMP)") ?? go.transform.Find("Label");
            if (textT != null)
            {
                var textRT = textT.GetComponent<RectTransform>();
                textRT.anchorMin = new Vector2(0f, 0f);
                textRT.anchorMax = new Vector2(1f, 1f);
                textRT.offsetMin = new Vector2(36f, 0f);
                textRT.offsetMax = new Vector2(-4f, 0f);
                var tmp = textT.GetComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.text = label;
                tmp.fontSize = 12;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.color = new Color(0.88f, 0.93f, 1f, 0.95f);
                tmp.raycastTarget = false;
            }
        }

        public static void CapturePreview(string filename = "customization_codex_rendered.png")
        {
            string prefabPath = "Assets/03.Prefab/UI/Canvas/CharacterCustomizeCanvas.prefab";
            GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (canvasPrefab == null) return;

            GameObject root = PrefabUtility.InstantiatePrefab(canvasPrefab) as GameObject;
            int width = 1280;
            int height = 720;

            GameObject camGO = new GameObject("TempUICam", typeof(Camera));
            Camera cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                root.SetActive(true);

                // Populate dummy thumbnails for realistic preview
                var thumbContent = root.transform.Find("Panel/Right_Codex_Book/Thumbnail Scroll View/Viewport/Content");
                var thumbBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefab/UI/Buttons/PartThumbnailButton.prefab");
                var helmetIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/07.Textures/UI/PaperdollIcons/Silhouette_HelmetAttach.png");
                var armorIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/07.Textures/UI/PaperdollIcons/Silhouette_Armor.png");

                if (thumbContent != null && thumbBtnPrefab != null)
                {
                    // Sample items for preview (일반 등급은 Color.white로 전달하여 하얗게 덮이지 않는지 검증)
                    var samples = new (string label, Color color, bool selected, bool locked, Sprite icon)[]
                    {
                        ("해제", Color.white, false, false, null),
                        ("수호자 투구", new Color(0.75f, 0.45f, 1f), true, false, helmetIcon),      // Epic (Purple)
                        ("기사단 투구", new Color(0.35f, 0.70f, 1f), false, false, helmetIcon),     // Rare (Blue)
                        ("경비병 흉갑", new Color(0.35f, 0.70f, 1f), false, false, armorIcon),      // Rare
                        ("수련자 모자", new Color(0.40f, 0.85f, 0.50f), false, false, helmetIcon),   // Uncommon (Green)
                        ("가죽 두건", Color.white, false, false, helmetIcon),                         // Common (White)
                        ("천 머리띠", Color.white, false, false, helmetIcon),                         // Common (White)
                        ("사냥꾼 흉갑", new Color(0.40f, 0.85f, 0.50f), false, false, armorIcon),   // Uncommon
                        ("황혼의 관", new Color(1f, 0.75f, 0.25f), false, true, helmetIcon),       // Legendary (Gold) Locked
                        ("용기사 투구", new Color(0.75f, 0.45f, 1f), false, true, helmetIcon),      // Epic Locked
                        ("심연의 가면", new Color(1f, 0.35f, 0.35f), false, true, helmetIcon),      // Mythic Locked
                        ("까마귀가면", Color.white, false, true, helmetIcon),                        // Common (White) Locked (사용자 제보 버그 검증)
                    };

                    for (int i = 0; i < samples.Length; i++)
                    {
                        var s = samples[i];
                        var btnGO = Object.Instantiate(thumbBtnPrefab, thumbContent);
                        var ptb = btnGO.GetComponent<PartThumbnailButton>();
                        if (ptb != null)
                        {
                            ptb.Setup(PartType.HeadCoverings, i - 1, s.icon, null);
                            ptb.SetLabel(s.label);
                            ptb.SetRarity(s.color);
                            ptb.SetSelected(s.selected);
                            ptb.SetLocked(s.locked);
                        }
                    }

                    // Update detail card with selected item info
                    var nameText = root.transform.Find("Panel/Right_Codex_Book/Item_Detail_Card/Text_ItemName")?.GetComponent<TextMeshProUGUI>();
                    var rarityText = root.transform.Find("Panel/Right_Codex_Book/Item_Detail_Card/Text_Rarity")?.GetComponent<TextMeshProUGUI>();
                    var statusText = root.transform.Find("Panel/Right_Codex_Book/Item_Detail_Card/Text_Status")?.GetComponent<TextMeshProUGUI>();
                    var descText = root.transform.Find("Panel/Right_Codex_Book/Item_Detail_Card/Text_Desc")?.GetComponent<TextMeshProUGUI>();
                    if (nameText != null) nameText.text = "수호자의 의전 투구";
                    if (rarityText != null) rarityText.text = "<color=#BF73FF>[에픽]</color>";
                    if (statusText != null) statusText.text = "<color=#55E88F>● 도감 수집 완료 (소유 중)</color>";
                    if (descText != null) descText.text = "고대 왕실 수호 기사단이 착용하던 성스러운 의전용 투구. 착용 시 주변에 은은한 수호의 빛이 감돕니다.";

                    // Update progress bar
                    var rateText = root.transform.Find("Panel/TopHeader_Bar/Progress_Group/Text_Rate")?.GetComponent<TextMeshProUGUI>();
                    var slider = root.transform.Find("Panel/TopHeader_Bar/Progress_Group/Slider")?.GetComponent<Slider>();
                    if (rateText != null) rateText.text = "도감 수집률: <color=#FFE680>18</color> / 45 (40%)";
                    if (slider != null)
                    {
                        slider.minValue = 0;
                        slider.maxValue = 45;
                        slider.value = 18;
                    }
                }

                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture.active = rt;
                Texture2D screenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTex.Apply();
                RenderTexture.active = null;

                byte[] bytes = screenTex.EncodeToPNG();
                Object.DestroyImmediate(screenTex);

                string brainDir = "C:/Users/BaeSoohyun/.gemini/antigravity/brain/3968999b-f68a-4325-9c6b-639753855725";
                string outPath = Path.Combine(brainDir, filename);
                File.WriteAllBytes(outPath, bytes);
                Debug.Log($"[SetupCustomizationCodexUI] Preview screenshot saved: {outPath}");
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
                Object.DestroyImmediate(root);
            }
        }

        public static void CaptureColorTabPreview(string filename = "customization_colortab_rendered.png")
        {
            string prefabPath = "Assets/03.Prefab/UI/Canvas/CharacterCustomizeCanvas.prefab";
            GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (canvasPrefab == null) return;

            GameObject root = PrefabUtility.InstantiatePrefab(canvasPrefab) as GameObject;
            int width = 1280;
            int height = 720;

            GameObject camGO = new GameObject("TempUICam", typeof(Camera));
            Camera cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                root.SetActive(true);

                // 염색 공방 탭 활성화 & 다른 도감 패널 숨김
                var thumbView = root.transform.Find("Panel/Right_Codex_Book/Thumbnail Scroll View");
                var detailCard = root.transform.Find("Panel/Right_Codex_Book/Item_Detail_Card");
                var partsTab = root.transform.Find("Panel/Right_Codex_Book/PartsTabGroup");
                var colorTab = root.transform.Find("Panel/Right_Codex_Book/ColorTabGroup");
                var fcp = colorTab != null ? colorTab.Find("FlexibleColorPicker") : null;

                if (thumbView != null) thumbView.gameObject.SetActive(false);
                if (detailCard != null) detailCard.gameObject.SetActive(false);
                if (partsTab != null) partsTab.gameObject.SetActive(false);
                if (colorTab != null) colorTab.gameObject.SetActive(true);
                if (fcp != null) fcp.gameObject.SetActive(true);

                // 상단 탭 토글 활성화 표시 변경
                var topTabsT = root.transform.Find("Panel/Right_Codex_Book/Top Taps");
                if (topTabsT != null)
                {
                    var pTab = topTabsT.Find("PartsTopTab")?.GetComponent<Toggle>();
                    var cTab = topTabsT.Find("ColorTopTab")?.GetComponent<Toggle>();
                    if (pTab != null) pTab.isOn = false;
                    if (cTab != null) cTab.isOn = true;
                }

                // 더미 색상 채널 행 생성
                var colorContent = colorTab != null ? (colorTab.Find("ColorRowParent /Viewport/Content") ?? colorTab.Find("ColorRowParent/Viewport/Content")) : null;
                var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefab/UI/Buttons/ColorChannelRow.prefab");
                if (colorContent != null && rowPrefab != null)
                {
                    var channels = new (string name, Color col)[]
                    {
                        ("주 색상", new Color(0.38f, 0.30f, 0.22f)),
                        ("보조 색상", new Color(0.72f, 0.58f, 0.25f)),
                        ("가죽 주색", new Color(0.55f, 0.33f, 0.15f)),
                        ("가죽 보조", new Color(0.35f, 0.22f, 0.10f)),
                        ("금속 주색", new Color(0.75f, 0.75f, 0.78f)),
                        ("금속 보조", new Color(0.50f, 0.50f, 0.52f)),
                        ("금속 어둠", new Color(0.25f, 0.25f, 0.26f)),
                        ("머리카락", new Color(0.18f, 0.12f, 0.08f)),
                        ("피부", new Color(0.88f, 0.72f, 0.58f)),
                        ("수염 잔털", new Color(0.35f, 0.25f, 0.18f)),
                        ("흉터", new Color(0.75f, 0.48f, 0.48f)),
                        ("눈동자", new Color(0.25f, 0.50f, 0.85f)),
                        ("문신", new Color(0.45f, 0.12f, 0.12f)),
                    };

                    foreach (var ch in channels)
                    {
                        var rowGO = Object.Instantiate(rowPrefab, colorContent);
                        var label = rowGO.GetComponent<TextMeshProUGUI>();
                        if (label != null) label.text = ch.name;
                        var btnImg = rowGO.transform.Find("ColorButton")?.GetComponent<Image>();
                        if (btnImg != null) btnImg.color = ch.col;
                    }
                }

                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture.active = rt;
                Texture2D screenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTex.Apply();
                RenderTexture.active = null;

                byte[] bytes = screenTex.EncodeToPNG();
                Object.DestroyImmediate(screenTex);

                string brainDir = "C:/Users/BaeSoohyun/.gemini/antigravity/brain/3968999b-f68a-4325-9c6b-639753855725";
                string outPath = Path.Combine(brainDir, filename);
                File.WriteAllBytes(outPath, bytes);
                Debug.Log($"[SetupCustomizationCodexUI] Color tab preview screenshot saved: {outPath}");
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
                Object.DestroyImmediate(root);
            }
        }

        public static void SetupPartThumbnailButtonPrefab()
        {
            string path = "Assets/03.Prefab/UI/Buttons/PartThumbnailButton.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Sprite squareFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Square02_White1.png");
                Sprite lineFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/LineFrame_White.png");
                Sprite darkSquare = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/BasicFrame_Square01.png");
                Sprite circleFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Circle_116_White.png");
                Sprite lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Lock.Png");
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/NANUM/DynamicFonts/NanumBarunGothicBold Dynamic SDF.asset");

                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(86f, 98f);

                // Root image: Dark slate card base
                var rootImg = root.GetComponent<Image>();
                rootImg.sprite = darkSquare != null ? darkSquare : squareFrame;
                rootImg.type = Image.Type.Sliced;
                rootImg.color = new Color(0.11f, 0.14f, 0.20f, 0.98f);

                // 1. RarityBorder: Top Accent Stripe (Ribbon) - 상단 3.5px 얇은 띠 형태 (카드 전체를 하얗게 덮지 않음)
                Transform rarityT = root.transform.Find("RarityBorder");
                if (rarityT == null)
                {
                    var rGO = new GameObject("RarityBorder", typeof(RectTransform), typeof(Image));
                    rGO.transform.SetParent(root.transform, false);
                    rarityT = rGO.transform;
                }
                var rarityRT = rarityT.GetComponent<RectTransform>();
                rarityRT.anchorMin = new Vector2(0f, 1f);
                rarityRT.anchorMax = new Vector2(1f, 1f);
                rarityRT.pivot = new Vector2(0.5f, 1f);
                rarityRT.anchoredPosition = new Vector2(0f, -2f);
                rarityRT.sizeDelta = new Vector2(-8f, 3.5f);
                var rarityImg = rarityT.GetComponent<Image>();
                rarityImg.sprite = squareFrame;
                rarityImg.type = Image.Type.Sliced;
                rarityImg.color = new Color(1f, 1f, 1f, 0f);
                rarityImg.raycastTarget = false;
                rarityT.gameObject.SetActive(false); // 기본은 비활성화 (SetRarity에서 특수 등급 시 활성화)

                // 2. Thumbnail Slot Backdrop: Inner rounded disc/rect
                Transform slotBgT = root.transform.Find("Slot_Backdrop");
                if (slotBgT == null)
                {
                    var sbGO = new GameObject("Slot_Backdrop", typeof(RectTransform), typeof(Image));
                    sbGO.transform.SetParent(root.transform, false);
                    slotBgT = sbGO.transform;
                }
                var sbRT = slotBgT.GetComponent<RectTransform>();
                sbRT.anchorMin = new Vector2(0.5f, 0.5f);
                sbRT.anchorMax = new Vector2(0.5f, 0.5f);
                sbRT.pivot = new Vector2(0.5f, 0.5f);
                sbRT.anchoredPosition = new Vector2(0f, 10f);
                sbRT.sizeDelta = new Vector2(64f, 64f);
                var sbImg = slotBgT.GetComponent<Image>();
                sbImg.sprite = circleFrame != null ? circleFrame : squareFrame;
                sbImg.type = Image.Type.Sliced;
                sbImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
                sbImg.raycastTarget = false;

                // 3. Thumbnail (Sprite)
                Transform thumbT = root.transform.Find("Thumbnail ") ?? root.transform.Find("Thumbnail");
                if (thumbT != null)
                {
                    var tRT = thumbT.GetComponent<RectTransform>();
                    tRT.anchorMin = new Vector2(0.5f, 0.5f);
                    tRT.anchorMax = new Vector2(0.5f, 0.5f);
                    tRT.pivot = new Vector2(0.5f, 0.5f);
                    tRT.anchoredPosition = new Vector2(0f, 10f);
                    tRT.sizeDelta = new Vector2(56f, 56f);
                    var tImg = thumbT.GetComponent<Image>();
                    tImg.preserveAspect = true;
                    tImg.color = Color.white;
                    tImg.raycastTarget = false;
                }

                // 4. Label (Bottom item title)
                Transform labelT = root.transform.Find("Label");
                if (labelT == null)
                {
                    var lGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    lGO.transform.SetParent(root.transform, false);
                    labelT = lGO.transform;
                }
                var lRT = labelT.GetComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0f, 0f);
                lRT.anchorMax = new Vector2(1f, 0f);
                lRT.pivot = new Vector2(0.5f, 0f);
                lRT.anchoredPosition = new Vector2(0f, 6f);
                lRT.sizeDelta = new Vector2(-6f, 18f);
                var lTMP = labelT.GetComponent<TextMeshProUGUI>();
                if (font != null) lTMP.font = font;
                lTMP.fontSize = 11;
                lTMP.fontStyle = FontStyles.Bold;
                lTMP.alignment = TextAlignmentOptions.Center;
                lTMP.color = new Color(0.85f, 0.90f, 0.98f, 0.95f);
                lTMP.raycastTarget = false;

                // 5. Highlight (_selectedFrame): Bright Gold Glowing Outer 4-edge Border
                Transform hlT = root.transform.Find("Highlight");
                if (hlT != null) Object.DestroyImmediate(hlT.gameObject);
                var hlGO = CreateBorderFrame(root.transform, "Highlight", 3f, new Color(1f, 0.88f, 0.25f, 1f));
                hlT = hlGO.transform;
                hlT.gameObject.SetActive(false);

                // 6. Lock Icon (_lockIcon): Top right corner badge
                Transform lockT = root.transform.Find("Lock");
                if (lockT == null)
                {
                    var lockGO = new GameObject("Lock", typeof(RectTransform), typeof(Image));
                    lockGO.transform.SetParent(root.transform, false);
                    lockT = lockGO.transform;
                }
                var lockRT = lockT.GetComponent<RectTransform>();
                lockRT.anchorMin = new Vector2(1f, 1f);
                lockRT.anchorMax = new Vector2(1f, 1f);
                lockRT.pivot = new Vector2(1f, 1f);
                lockRT.anchoredPosition = new Vector2(-4f, -4f);
                lockRT.sizeDelta = new Vector2(20f, 20f);
                var lockImg = lockT.GetComponent<Image>();
                if (lockSprite != null) lockImg.sprite = lockSprite;
                lockImg.color = new Color(1f, 0.65f, 0.40f, 1f);
                lockImg.raycastTarget = false;
                lockT.gameObject.SetActive(false);

                // Re-bind PartThumbnailButton serialized fields via reflection
                var ptb = root.GetComponent<PartThumbnailButton>();
                var tPtb = typeof(PartThumbnailButton);
                tPtb.GetField("_thumbnail", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, thumbT?.GetComponent<Image>());
                tPtb.GetField("_selectedFrame", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, hlT.gameObject);
                tPtb.GetField("_button", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, root.GetComponent<Button>());
                tPtb.GetField("_lockIcon", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, lockT.gameObject);
                tPtb.GetField("_label", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, lTMP);
                tPtb.GetField("_rarityBorder", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, rarityImg);
                tPtb.GetField("_cardBg", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ptb, rootImg);

                // Ensure hierarchy order: RarityBorder(0), Slot_Backdrop(1), Thumbnail(2), Label(3), Highlight(4), Lock(5)
                rarityT.SetSiblingIndex(0);
                slotBgT.SetSiblingIndex(1);
                if (thumbT != null) thumbT.SetSiblingIndex(2);
                labelT.SetSiblingIndex(3);
                hlT.SetSiblingIndex(4);
                lockT.SetSiblingIndex(5);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[SetupCustomizationCodexUI] PartThumbnailButton.prefab updated successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void SetupColorChannelRowPrefab()
        {
            string path = "Assets/03.Prefab/UI/Buttons/ColorChannelRow.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Sprite squareFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Square02_White1.png");
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/NANUM/DynamicFonts/NanumBarunGothicBold Dynamic SDF.asset");

                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0f, 34f);

                // 깔끔한 다크 로우 플레이트 배경 (자식 Bg로 생성하여 TextMeshProUGUI와 Graphic 컴포넌트 충돌 방지)
                Transform bgT = root.transform.Find("Bg");
                if (bgT == null)
                {
                    var bgGO = new GameObject("Bg", typeof(RectTransform), typeof(Image));
                    bgGO.transform.SetParent(root.transform, false);
                    bgT = bgGO.transform;
                    bgT.SetSiblingIndex(0);
                }
                var bgRT = bgT.GetComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;
                var bgImg = bgT.GetComponent<Image>();
                if (bgImg != null)
                {
                    if (squareFrame != null) { bgImg.sprite = squareFrame; bgImg.type = Image.Type.Sliced; }
                    bgImg.color = new Color(0.10f, 0.14f, 0.20f, 0.45f);
                    bgImg.raycastTarget = false;
                }

                // 텍스트 라벨: 11.5pt, 줄바꿈 없음(NoWrap), 넉넉한 우측 여백(95px)으로 컬러 버튼과 충돌 방지
                var label = root.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    if (font != null) label.font = font;
                    label.fontSize = 11.5f;
                    label.fontStyle = FontStyles.Bold;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.color = new Color(0.85f, 0.92f, 1f, 0.95f);
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.raycastTarget = false;
                    label.margin = new Vector4(12f, 0f, 95f, 0f);
                }

                // 컬러 프리뷰 & 선택 버튼: 컴팩트한 76x22 사이즈로 우측 정렬
                Transform btnT = root.transform.Find("ColorButton");
                if (btnT != null)
                {
                    var btnRT = btnT.GetComponent<RectTransform>();
                    btnRT.anchorMin = new Vector2(1f, 0.5f);
                    btnRT.anchorMax = new Vector2(1f, 0.5f);
                    btnRT.pivot = new Vector2(1f, 0.5f);
                    btnRT.anchoredPosition = new Vector2(-10f, 0f);
                    btnRT.sizeDelta = new Vector2(76f, 22f);

                    var btnImg = btnT.GetComponent<Image>();
                    if (btnImg != null)
                    {
                        btnImg.sprite = squareFrame;
                        btnImg.type = Image.Type.Sliced;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[SetupCustomizationCodexUI] ColorChannelRow.prefab updated successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
