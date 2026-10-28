#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.UI;

namespace MySecondWorld.EditorTools
{
    public static class NPCDialogueCanvasSetup
    {
        private const string PrefabPath = "Assets/03.Prefab/UI/Canvas/NPCDialogueCanvas.prefab";
        private const string NpcBubblePrefabPath = "Assets/03.Prefab/UI/Bubble/NpcBubble.prefab";
        private const string PlayerBubblePrefabPath = "Assets/03.Prefab/UI/Bubble/PlayerBubble.prefab";
        private const string ThinkingBubblePrefabPath = "Assets/03.Prefab/UI/Bubble/ThinkingBubble.prefab";

        // Fonts
        private const string FontBoldPath = "Assets/TextMesh Pro/NANUM/DynamicFonts/NanumSquareRoundB Dynamic SDF.asset";
        private const string FontRegularPath = "Assets/TextMesh Pro/NANUM/DynamicFonts/NanumSquareRoundR Dynamic SDF.asset";

        // Layer Lab Sprites
        private const string PopupDarkFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Popup/Popup_Demo_Dark/Popup00_Frame1.png";
        private const string InnerFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/ListFrame00.png";
        private const string PassFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/PassFrame01_n.png";
        private const string ChatFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/ChatFrmae01.png";

        // Buttons
        private const string TabActiveBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_MenuButton_Rectangle00_s.png";
        private const string TabInactiveBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_MenuButton_Rectangle00_n.png";
        private const string PurpleBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Common/Btn_Rectangle00_n_Purple.png";
        private const string MintBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Common/Btn_Rectangle00_n_Mint.png";
        private const string RedRoundBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Common/Btn_IconButton_Circle_90_red.png";
        private const string DarkRoundBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_IconButton_Circle_90.png";

        // Slider
        private const string SliderFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Slider/Slider_Demo_Dark/Slider07_Frame.png";
        private const string SliderFillPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Slider/Slider_Demo_Dark/Slider07_Fill_Purple.png";

        // Picto Icons
        private const string IconMicPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Mic.Png";
        private const string IconSendPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Send.Png";
        private const string IconXPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Symbol_X.Png";
        private const string IconSoundOffPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Sound_Off.Png";

        [MenuItem("Tools/MySecondWorld/Setup NPC Dialogue Canvas Layout")]
        public static void SetupDialogueCanvas()
        {
            Debug.Log("[NPCDialogueCanvasSetup] === Layer Lab 에셋 & 나눔스퀘어라운드 폰트 정밀 스타일링 시작 ===");

            var fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
            var fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRegularPath);

            var popupFrame = AssetDatabase.LoadAssetAtPath<Sprite>(PopupDarkFramePath);
            var innerFrame = AssetDatabase.LoadAssetAtPath<Sprite>(InnerFramePath);
            var passFrame = AssetDatabase.LoadAssetAtPath<Sprite>(PassFramePath);
            var chatFrame = AssetDatabase.LoadAssetAtPath<Sprite>(ChatFramePath);

            var tabActiveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TabActiveBtnPath);
            var tabInactiveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TabInactiveBtnPath);
            var purpleBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PurpleBtnPath);
            var mintBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MintBtnPath);
            var redRoundBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RedRoundBtnPath);
            var darkRoundBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DarkRoundBtnPath);

            var sliderFrame = AssetDatabase.LoadAssetAtPath<Sprite>(SliderFramePath);
            var sliderFill = AssetDatabase.LoadAssetAtPath<Sprite>(SliderFillPath);

            var iconMic = AssetDatabase.LoadAssetAtPath<Sprite>(IconMicPath);
            var iconSend = AssetDatabase.LoadAssetAtPath<Sprite>(IconSendPath);
            var iconX = AssetDatabase.LoadAssetAtPath<Sprite>(IconXPath);
            var iconSoundOff = AssetDatabase.LoadAssetAtPath<Sprite>(IconSoundOffPath);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError($"[NPCDialogueCanvasSetup] 프리팹 로드 실패: {PrefabPath}");
                return;
            }

            try
            {
                var dialogueUI = root.GetComponent<NPCDialogueUI>();
                if (dialogueUI == null)
                {
                    Debug.LogError("[NPCDialogueCanvasSetup] NPCDialogueUI 컴포넌트를 찾을 수 없습니다.");
                    return;
                }

                var dialoguePanelTrans = root.transform.Find("DialoguePanel") as RectTransform;
                if (dialoguePanelTrans == null)
                {
                    Debug.LogError("[NPCDialogueCanvasSetup] DialoguePanel을 찾을 수 없습니다.");
                    return;
                }

                // 1. DialoguePanel 하단 도킹 (Bottom 42% of screen)
                dialoguePanelTrans.anchorMin = new Vector2(0f, 0f);
                dialoguePanelTrans.anchorMax = new Vector2(1f, 0.42f);
                dialoguePanelTrans.offsetMin = new Vector2(25f, 15f);
                dialoguePanelTrans.offsetMax = new Vector2(-25f, 0f);
                dialoguePanelTrans.pivot = new Vector2(0.5f, 0f);

                var panelImg = dialoguePanelTrans.GetComponent<Image>();
                if (panelImg != null)
                {
                    panelImg.color = Color.white; // 본래 Layer Lab 다크 프레임 텍스처 온전히 표현
                    if (popupFrame != null)
                    {
                        panelImg.sprite = popupFrame;
                        panelImg.type = Image.Type.Sliced;
                    }
                }

                // 2. 상단 헤더: NPCName 배지, TTSStopBtn, CloseBtn
                var nameBadgeGo = GetOrCreateChild(dialoguePanelTrans, "NameBadge");
                var nameBadgeRect = nameBadgeGo.GetComponent<RectTransform>();
                nameBadgeRect.anchorMin = new Vector2(0f, 1f);
                nameBadgeRect.anchorMax = new Vector2(0f, 1f);
                nameBadgeRect.pivot = new Vector2(0f, 1f);
                nameBadgeRect.anchoredPosition = new Vector2(25f, -8f);
                nameBadgeRect.sizeDelta = new Vector2(230f, 40f);

                var badgeImg = nameBadgeGo.GetComponent<Image>() ?? nameBadgeGo.AddComponent<Image>();
                if (passFrame != null)
                {
                    badgeImg.sprite = passFrame;
                    badgeImg.type = Image.Type.Sliced;
                    badgeImg.color = Color.white;
                }

                var nameTrans = dialoguePanelTrans.Find("NPCName") as RectTransform;
                if (nameTrans == null)
                    nameTrans = nameBadgeGo.transform.Find("NPCName") as RectTransform;

                if (nameTrans != null)
                {
                    nameTrans.SetParent(nameBadgeRect, false);
                    nameTrans.anchorMin = Vector2.zero;
                    nameTrans.anchorMax = Vector2.one;
                    nameTrans.offsetMin = new Vector2(14f, 0f);
                    nameTrans.offsetMax = new Vector2(-14f, 0f);

                    var tmp = nameTrans.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = "NPC";
                        if (fontBold != null) tmp.font = fontBold;
                        tmp.fontSize = 18;
                        tmp.fontStyle = FontStyles.Bold;
                        tmp.color = new Color(1f, 0.9f, 0.5f, 1f); // 따뜻한 골드 옐로우
                        tmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                // TTSStopBtn (원형 아이콘 버튼)
                var ttsStopTrans = dialoguePanelTrans.Find("TTSStopBtn") as RectTransform;
                if (ttsStopTrans != null)
                {
                    ttsStopTrans.anchorMin = new Vector2(1f, 1f);
                    ttsStopTrans.anchorMax = new Vector2(1f, 1f);
                    ttsStopTrans.pivot = new Vector2(1f, 1f);
                    ttsStopTrans.anchoredPosition = new Vector2(-68f, -12f);
                    ttsStopTrans.sizeDelta = new Vector2(36f, 36f);

                    var btnImg = ttsStopTrans.GetComponent<Image>();
                    if (btnImg != null && darkRoundBtnSprite != null)
                    {
                        btnImg.sprite = darkRoundBtnSprite;
                        btnImg.color = Color.white;
                    }

                    var oldTmp = ttsStopTrans.GetComponentInChildren<TextMeshProUGUI>();
                    if (oldTmp != null) oldTmp.gameObject.SetActive(false);

                    var iconGo = GetOrCreateChild(ttsStopTrans, "Icon");
                    var iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(18f, 18f);
                    iconRect.anchoredPosition = Vector2.zero;

                    var iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
                    if (iconSoundOff != null)
                    {
                        iconImg.sprite = iconSoundOff;
                        iconImg.color = Color.white;
                    }
                }

                // CloseBtn (빨간 원형 X 버튼)
                var closeTrans = dialoguePanelTrans.Find("CloseBtn") as RectTransform;
                if (closeTrans != null)
                {
                    closeTrans.anchorMin = new Vector2(1f, 1f);
                    closeTrans.anchorMax = new Vector2(1f, 1f);
                    closeTrans.pivot = new Vector2(1f, 1f);
                    closeTrans.anchoredPosition = new Vector2(-22f, -12f);
                    closeTrans.sizeDelta = new Vector2(36f, 36f);

                    var btnImg = closeTrans.GetComponent<Image>();
                    if (btnImg != null && redRoundBtnSprite != null)
                    {
                        btnImg.sprite = redRoundBtnSprite;
                        btnImg.color = Color.white;
                    }

                    var oldTmp = closeTrans.GetComponentInChildren<TextMeshProUGUI>();
                    if (oldTmp != null) oldTmp.gameObject.SetActive(false);

                    var iconGo = GetOrCreateChild(closeTrans, "Icon");
                    var iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(16f, 16f);
                    iconRect.anchoredPosition = Vector2.zero;

                    var iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
                    if (iconX != null)
                    {
                        iconImg.sprite = iconX;
                        iconImg.color = Color.white;
                    }
                }

                // 3. 좌측 섹션 (LeftSection) - 기억 & 히스토리
                var leftSectionGo = GetOrCreateChild(dialoguePanelTrans, "LeftSection");
                var leftRect = leftSectionGo.GetComponent<RectTransform>();
                leftRect.anchorMin = new Vector2(0f, 0f);
                leftRect.anchorMax = new Vector2(0.33f, 0.85f);
                leftRect.offsetMin = new Vector2(25f, 15f);
                leftRect.offsetMax = new Vector2(-5f, -5f);
                leftRect.pivot = new Vector2(0f, 0f);

                var leftImg = leftSectionGo.GetComponent<Image>() ?? leftSectionGo.AddComponent<Image>();
                if (innerFrame != null)
                {
                    leftImg.sprite = innerFrame;
                    leftImg.type = Image.Type.Sliced;
                    leftImg.color = Color.white; // 프레임 고유 스타일 표현
                }

                // 탭 바 (TabBar)
                var tabBar = GetOrCreateChild(leftRect, "TabBar");
                var tabRect = tabBar.GetComponent<RectTransform>();
                tabRect.anchorMin = new Vector2(0f, 1f);
                tabRect.anchorMax = new Vector2(1f, 1f);
                tabRect.pivot = new Vector2(0.5f, 1f);
                tabRect.anchoredPosition = new Vector2(0f, -8f);
                tabRect.sizeDelta = new Vector2(-16f, 36f);

                // TabMemoryBtn
                var tabMemBtnGo = GetOrCreateChild(tabRect, "TabMemoryBtn");
                var tabMemRect = tabMemBtnGo.GetComponent<RectTransform>();
                tabMemRect.anchorMin = new Vector2(0f, 0f);
                tabMemRect.anchorMax = new Vector2(0.5f, 1f);
                tabMemRect.offsetMin = new Vector2(2f, 1f);
                tabMemRect.offsetMax = new Vector2(-2f, -1f);
                var tabMemBtn = tabMemBtnGo.GetComponent<Button>() ?? tabMemBtnGo.AddComponent<Button>();
                var tabMemImg = tabMemBtnGo.GetComponent<Image>() ?? tabMemBtnGo.AddComponent<Image>();
                if (tabActiveSprite != null)
                {
                    tabMemImg.sprite = tabActiveSprite;
                    tabMemImg.type = Image.Type.Sliced;
                    tabMemImg.color = Color.white;
                }
                var tabMemTmp = GetOrCreateCenterTMP(tabMemBtnGo.transform, "Label", "기억", 15, fontBold);
                tabMemTmp.color = Color.white;

                // TabHistoryBtn
                var tabHisBtnGo = GetOrCreateChild(tabRect, "TabHistoryBtn");
                var tabHisRect = tabHisBtnGo.GetComponent<RectTransform>();
                tabHisRect.anchorMin = new Vector2(0.5f, 0f);
                tabHisRect.anchorMax = new Vector2(1f, 1f);
                tabHisRect.offsetMin = new Vector2(2f, 1f);
                tabHisRect.offsetMax = new Vector2(-2f, -1f);
                var tabHisBtn = tabHisBtnGo.GetComponent<Button>() ?? tabHisBtnGo.AddComponent<Button>();
                var tabHisImg = tabHisBtnGo.GetComponent<Image>() ?? tabHisBtnGo.AddComponent<Image>();
                if (tabInactiveSprite != null)
                {
                    tabHisImg.sprite = tabInactiveSprite;
                    tabHisImg.type = Image.Type.Sliced;
                    tabHisImg.color = Color.white;
                }
                var tabHisTmp = GetOrCreateCenterTMP(tabHisBtnGo.transform, "Label", "대화 기록", 15, fontBold);
                tabHisTmp.color = new Color(0.75f, 0.8f, 0.9f, 0.85f);

                // 기억 뷰 (MemoryView)
                var memViewGo = GetOrCreateChild(leftRect, "MemoryView");
                var memViewRect = memViewGo.GetComponent<RectTransform>();
                memViewRect.anchorMin = new Vector2(0f, 0f);
                memViewRect.anchorMax = new Vector2(1f, 1f);
                memViewRect.offsetMin = new Vector2(12f, 10f);
                memViewRect.offsetMax = new Vector2(-12f, -50f);

                // 호감도 슬라이더
                var sliderGo = GetOrCreateChild(memViewRect, "RelationSlider");
                var sliderRect = sliderGo.GetComponent<RectTransform>();
                sliderRect.anchorMin = new Vector2(0f, 1f);
                sliderRect.anchorMax = new Vector2(1f, 1f);
                sliderRect.pivot = new Vector2(0.5f, 1f);
                sliderRect.anchoredPosition = new Vector2(0f, -6f);
                sliderRect.sizeDelta = new Vector2(0f, 20f);

                var slider = sliderGo.GetComponent<Slider>() ?? sliderGo.AddComponent<Slider>();
                slider.minValue = -100f;
                slider.maxValue = 100f;
                slider.value = 38f;

                var sliderBg = sliderGo.GetComponent<Image>() ?? sliderGo.AddComponent<Image>();
                if (sliderFrame != null)
                {
                    sliderBg.sprite = sliderFrame;
                    sliderBg.type = Image.Type.Sliced;
                    sliderBg.color = Color.white;
                }

                var fillAreaGo = GetOrCreateChild(sliderRect, "Fill Area");
                var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
                fillAreaRect.anchorMin = new Vector2(0f, 0f);
                fillAreaRect.anchorMax = new Vector2(1f, 1f);
                fillAreaRect.offsetMin = new Vector2(4f, 3f);
                fillAreaRect.offsetMax = new Vector2(-4f, -3f);

                var fillGo = GetOrCreateChild(fillAreaRect, "Fill");
                var fillRect = fillGo.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                var fillImg = fillGo.GetComponent<Image>() ?? fillGo.AddComponent<Image>();
                if (sliderFill != null)
                {
                    fillImg.sprite = sliderFill;
                    fillImg.type = Image.Type.Sliced;
                    fillImg.color = Color.white;
                }
                slider.fillRect = fillRect;

                var relText = GetOrCreateTMP(memViewRect, "RelationText", "호감도: +38 (호감)", 14, fontBold);
                var relTextRect = relText.rectTransform;
                relTextRect.anchorMin = new Vector2(0f, 1f);
                relTextRect.anchorMax = new Vector2(1f, 1f);
                relTextRect.pivot = new Vector2(0f, 1f);
                relTextRect.anchoredPosition = new Vector2(2f, -30f);
                relTextRect.sizeDelta = new Vector2(0f, 20f);
                relText.color = new Color(0.92f, 0.94f, 1f, 1f);

                var firstMetText = GetOrCreateTMP(memViewRect, "FirstMetText", "첫 만남: 마을 광장 (최근)", 13, fontRegular);
                var firstMetRect = firstMetText.rectTransform;
                firstMetRect.anchorMin = new Vector2(0f, 1f);
                firstMetRect.anchorMax = new Vector2(1f, 1f);
                firstMetRect.pivot = new Vector2(0f, 1f);
                firstMetRect.anchoredPosition = new Vector2(2f, -54f);
                firstMetRect.sizeDelta = new Vector2(0f, 18f);
                firstMetText.color = new Color(0.75f, 0.8f, 0.9f, 0.85f);

                var convCountText = GetOrCreateTMP(memViewRect, "ConvCountText", "14회 대화함", 13, fontRegular);
                var convRect = convCountText.rectTransform;
                convRect.anchorMin = new Vector2(0f, 1f);
                convRect.anchorMax = new Vector2(1f, 1f);
                convRect.pivot = new Vector2(0f, 1f);
                convRect.anchoredPosition = new Vector2(2f, -76f);
                convRect.sizeDelta = new Vector2(0f, 18f);
                convCountText.color = new Color(0.75f, 0.8f, 0.9f, 0.85f);

                var factsTitle = GetOrCreateTMP(memViewRect, "FactsTitle", "알고 있는 사실들:", 14, fontBold);
                var factsTitleRect = factsTitle.rectTransform;
                factsTitleRect.anchorMin = new Vector2(0f, 1f);
                factsTitleRect.anchorMax = new Vector2(1f, 1f);
                factsTitleRect.pivot = new Vector2(0f, 1f);
                factsTitleRect.anchoredPosition = new Vector2(2f, -100f);
                factsTitleRect.sizeDelta = new Vector2(0f, 20f);
                factsTitle.color = new Color(0.6f, 0.78f, 1f, 1f);

                var factsContentGo = GetOrCreateChild(memViewRect, "KnownFactsContent");
                var factsContentRect = factsContentGo.GetComponent<RectTransform>();
                factsContentRect.anchorMin = new Vector2(0f, 0f);
                factsContentRect.anchorMax = new Vector2(1f, 1f);
                factsContentRect.offsetMin = new Vector2(2f, 2f);
                factsContentRect.offsetMax = new Vector2(-2f, -126f);
                var factsVlg = factsContentGo.GetComponent<VerticalLayoutGroup>() ?? factsContentGo.AddComponent<VerticalLayoutGroup>();
                factsVlg.childControlWidth = true;
                factsVlg.childControlHeight = false;
                factsVlg.spacing = 5f;

                // 히스토리 뷰 (HistoryView)
                var hisViewGo = GetOrCreateChild(leftRect, "HistoryView");
                var hisViewRect = hisViewGo.GetComponent<RectTransform>();
                hisViewRect.anchorMin = new Vector2(0f, 0f);
                hisViewRect.anchorMax = new Vector2(1f, 1f);
                hisViewRect.offsetMin = new Vector2(12f, 10f);
                hisViewRect.offsetMax = new Vector2(-12f, -46f);
                hisViewGo.SetActive(false);

                var hisContentGo = GetOrCreateChild(hisViewRect, "HistoryContent");
                var hisContentRect = hisContentGo.GetComponent<RectTransform>();
                hisContentRect.anchorMin = new Vector2(0f, 0f);
                hisContentRect.anchorMax = new Vector2(1f, 1f);
                hisContentRect.offsetMin = Vector2.zero;
                hisContentRect.offsetMax = Vector2.zero;
                var hisVlg = hisContentGo.GetComponent<VerticalLayoutGroup>() ?? hisContentGo.AddComponent<VerticalLayoutGroup>();
                hisVlg.childControlWidth = true;
                hisVlg.childControlHeight = false;
                hisVlg.spacing = 6f;

                // 4. 우측 대화창 & 하단 입력바 (RightSection: 65% Width)
                var scrollTrans = dialoguePanelTrans.Find("Scroll View") as RectTransform;
                if (scrollTrans != null)
                {
                    scrollTrans.anchorMin = new Vector2(0.35f, 0.22f);
                    scrollTrans.anchorMax = new Vector2(1f, 0.88f);
                    scrollTrans.offsetMin = new Vector2(10f, 5f);
                    scrollTrans.offsetMax = new Vector2(-25f, -5f);

                    var scrollImg = scrollTrans.GetComponent<Image>();
                    if (scrollImg != null)
                    {
                        if (chatFrame != null)
                        {
                            scrollImg.sprite = chatFrame;
                            scrollImg.type = Image.Type.Sliced;
                            scrollImg.color = new Color(1f, 1f, 1f, 0.35f);
                        }
                    }

                    var hScrollbar = scrollTrans.Find("Scrollbar Horizontal");
                    if (hScrollbar != null) hScrollbar.gameObject.SetActive(false);

                    var scrollRect = scrollTrans.GetComponent<ScrollRect>();
                    if (scrollRect != null)
                    {
                        scrollRect.horizontal = false;
                        scrollRect.horizontalScrollbar = null;
                    }
                }

                // 하단 입력바: PlayerInputField (PassFrame) + STTBtn (Mint) + SubmitBtn (Purple)
                var inputTrans = dialoguePanelTrans.Find("PlayerInputField") as RectTransform;
                if (inputTrans != null)
                {
                    inputTrans.anchorMin = new Vector2(0.35f, 0f);
                    inputTrans.anchorMax = new Vector2(1f, 0f);
                    inputTrans.pivot = new Vector2(0f, 0f);
                    inputTrans.anchoredPosition = new Vector2(10f, 12f);
                    inputTrans.sizeDelta = new Vector2(-195f, 44f);

                    var inputImg = inputTrans.GetComponent<Image>();
                    if (inputImg != null && passFrame != null)
                    {
                        inputImg.sprite = passFrame;
                        inputImg.type = Image.Type.Sliced;
                        inputImg.color = Color.white;
                    }

                    var tmpInput = inputTrans.GetComponent<TMP_InputField>();
                    if (tmpInput != null)
                    {
                        if (tmpInput.placeholder is TextMeshProUGUI phTmp)
                        {
                            phTmp.text = "NPC에게 메시지 입력... (Enter로 전송)";
                            if (fontRegular != null) phTmp.font = fontRegular;
                            phTmp.fontSize = 15;
                            phTmp.fontStyle = FontStyles.Normal;
                            phTmp.color = new Color(0.6f, 0.65f, 0.75f, 0.6f);
                        }
                        if (tmpInput.textComponent != null)
                        {
                            if (fontRegular != null) tmpInput.textComponent.font = fontRegular;
                            tmpInput.textComponent.fontSize = 15;
                            tmpInput.textComponent.color = Color.white;
                        }
                    }
                }

                // STT 버튼 (민트 라운드 버튼 + 마이크 아이콘)
                var sttTrans = dialoguePanelTrans.Find("STTBtn") as RectTransform;
                if (sttTrans != null)
                {
                    sttTrans.anchorMin = new Vector2(1f, 0f);
                    sttTrans.anchorMax = new Vector2(1f, 0f);
                    sttTrans.pivot = new Vector2(1f, 0f);
                    sttTrans.anchoredPosition = new Vector2(-115f, 12f);
                    sttTrans.sizeDelta = new Vector2(46f, 44f);

                    var btnImg = sttTrans.GetComponent<Image>();
                    if (btnImg != null && mintBtnSprite != null)
                    {
                        btnImg.sprite = mintBtnSprite;
                        btnImg.type = Image.Type.Sliced;
                        btnImg.color = Color.white;
                    }

                    var oldTmp = sttTrans.GetComponentInChildren<TextMeshProUGUI>();
                    if (oldTmp != null) oldTmp.gameObject.SetActive(false);

                    var iconGo = GetOrCreateChild(sttTrans, "Icon");
                    var iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(22f, 22f);
                    iconRect.anchoredPosition = Vector2.zero;

                    var iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
                    if (iconMic != null)
                    {
                        iconImg.sprite = iconMic;
                        iconImg.color = Color.white;
                    }
                }

                // Submit 버튼 (보라색 라운드 버튼 + 전송 텍스트)
                var submitTrans = dialoguePanelTrans.Find("SubmitBtn") as RectTransform;
                if (submitTrans != null)
                {
                    submitTrans.anchorMin = new Vector2(1f, 0f);
                    submitTrans.anchorMax = new Vector2(1f, 0f);
                    submitTrans.pivot = new Vector2(1f, 0f);
                    submitTrans.anchoredPosition = new Vector2(-25f, 12f);
                    submitTrans.sizeDelta = new Vector2(82f, 44f);

                    var btnImg = submitTrans.GetComponent<Image>();
                    if (btnImg != null && purpleBtnSprite != null)
                    {
                        btnImg.sprite = purpleBtnSprite;
                        btnImg.type = Image.Type.Sliced;
                        btnImg.color = Color.white;
                    }

                    var tmp = GetOrCreateCenterTMP(submitTrans, "Text (TMP)", "전송", 15, fontBold);
                    tmp.gameObject.SetActive(true);
                    tmp.color = Color.white;
                }

                // 5. 직렬화 필드 완벽 바인딩
                var so = new SerializedObject(dialogueUI);
                BindField(so, "_canvas", dialoguePanelTrans.gameObject);
                BindField(so, "_npcNameText", nameTrans?.GetComponent<TextMeshProUGUI>());
                BindField(so, "_chatContent", dialoguePanelTrans.Find("Scroll View/Viewport/Content")?.GetComponent<RectTransform>());
                BindField(so, "_playerInputField", inputTrans?.GetComponent<TMP_InputField>());
                BindField(so, "_submitButton", submitTrans?.GetComponent<Button>());
                BindField(so, "_closeButton", closeTrans?.GetComponent<Button>());
                BindField(so, "_backdropButton", root.transform.Find("Dialogue_Backdrop")?.GetComponent<Button>());
                BindField(so, "_scrollRect", dialoguePanelTrans.Find("Scroll View")?.GetComponent<ScrollRect>());
                BindField(so, "_sttButton", sttTrans?.GetComponent<Button>());
                BindField(so, "_ttsStopButton", ttsStopTrans?.GetComponent<Button>());

                BindField(so, "_leftPanel", leftRect.gameObject);
                BindField(so, "_tabMemoryBtn", tabMemBtn);
                BindField(so, "_tabHistoryBtn", tabHisBtn);
                BindField(so, "_memoryView", memViewGo);
                BindField(so, "_historyView", hisViewGo);
                BindField(so, "_relationSlider", slider);
                BindField(so, "_relationText", relText);
                BindField(so, "_firstMetText", firstMetText);
                BindField(so, "_conversationCountText", convCountText);
                BindField(so, "_knownFactsContent", factsContentGo.GetComponent<RectTransform>());
                BindField(so, "_historyContent", hisContentGo.GetComponent<RectTransform>());

                var npcBubble = AssetDatabase.LoadAssetAtPath<GameObject>(NpcBubblePrefabPath);
                var playerBubble = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBubblePrefabPath);
                var thinkingBubble = AssetDatabase.LoadAssetAtPath<GameObject>(ThinkingBubblePrefabPath);
                if (npcBubble != null) BindField(so, "_npcBubblePrefab", npcBubble);
                if (playerBubble != null) BindField(so, "_playerBubblePrefab", playerBubble);
                if (thinkingBubble != null) BindField(so, "_thinkingBubblePrefab", thinkingBubble);

                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[NPCDialogueCanvasSetup] === Layer Lab 에셋 & 폰트 정밀 스타일링 적용 완료! ===");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 6. 버블 프리팹 폰트 & 스타일 업데이트
            UpdateBubblePrefabs(fontRegular, fontBold, chatFrame);
        }

        private static void UpdateBubblePrefabs(TMP_FontAsset fontRegular, TMP_FontAsset fontBold, Sprite chatFrame)
        {
            UpdateSingleBubble(NpcBubblePrefabPath, fontRegular, new Color(0.14f, 0.18f, 0.28f, 0.95f), chatFrame);
            UpdateSingleBubble(PlayerBubblePrefabPath, fontRegular, new Color(0.32f, 0.25f, 0.62f, 0.96f), chatFrame);
            UpdateSingleBubble(ThinkingBubblePrefabPath, fontBold, new Color(0.18f, 0.22f, 0.32f, 0.9f), chatFrame);
        }

        private static void UpdateSingleBubble(string path, TMP_FontAsset font, Color bgColor, Sprite frameSprite)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            try
            {
                var img = root.GetComponent<Image>();
                if (img != null)
                {
                    img.color = bgColor;
                    if (frameSprite != null)
                    {
                        img.sprite = frameSprite;
                        img.type = Image.Type.Sliced;
                    }
                }
                var tmp = root.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 16;
                    tmp.color = Color.white;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI GetOrCreateCenterTMP(Transform parent, string name, string defaultText, float fontSize, TMP_FontAsset font = null)
        {
            var child = parent.Find(name);
            TextMeshProUGUI tmp;
            if (child != null)
            {
                tmp = child.GetComponent<TextMeshProUGUI>() ?? child.gameObject.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                tmp = go.AddComponent<TextMeshProUGUI>();
            }

            var rect = tmp.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            if (font != null) tmp.font = font;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private static TextMeshProUGUI GetOrCreateTMP(Transform parent, string name, string defaultText, float fontSize, TMP_FontAsset font = null)
        {
            var child = parent.Find(name);
            TextMeshProUGUI tmp;
            if (child != null)
            {
                tmp = child.GetComponent<TextMeshProUGUI>() ?? child.gameObject.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                tmp = go.AddComponent<TextMeshProUGUI>();
            }

            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            if (font != null) tmp.font = font;
            tmp.color = Color.white;
            return tmp;
        }

        private static void BindField(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning($"[NPCDialogueCanvasSetup] 직렬화 프로퍼티를 찾을 수 없음: {propName}");
            }
        }
    }
}
#endif
