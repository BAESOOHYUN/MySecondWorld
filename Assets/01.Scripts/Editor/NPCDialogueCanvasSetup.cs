#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MySecondWorld.UI;

namespace MySecondWorld.EditorTools
{
    /// <summary>
    /// 피그마 PC 우측 패널 시안(01 · PC) 및 추천 카드(02 · RECOMMEND CARD) 핸드오프 규격에 맞춰
    /// NPCDialogueCanvas.prefab의 레이아웃, 스타일, 직렬화 필드를 완벽하게 자동 구성하는 에디터 툴.
    /// </summary>
    public static class NPCDialogueCanvasSetup
    {
        private const string PrefabPath = "Assets/03.Prefab/UI/Canvas/NPCDialogueCanvas.prefab";
        private const string NpcBubblePrefabPath = "Assets/03.Prefab/UI/Bubble/NpcBubble.prefab";
        private const string PlayerBubblePrefabPath = "Assets/03.Prefab/UI/Bubble/PlayerBubble.prefab";
        private const string ThinkingBubblePrefabPath = "Assets/03.Prefab/UI/Bubble/ThinkingBubble.prefab";

        // Fonts
        private const string FontBoldPath = "Assets/TextMesh Pro/NANUM/DynamicFonts/NanumSquareRoundB Dynamic SDF.asset";
        private const string FontRegularPath = "Assets/TextMesh Pro/NANUM/DynamicFonts/NanumSquareRoundR Dynamic SDF.asset";

        // Layer Lab Sprites - Frames
        private const string PopupDarkFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Popup/Popup_Demo_Dark/Popup00_Frame1.png";
        private const string InnerFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/ListFrame00.png";
        private const string PassFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/PassFrame01_n.png";
        private const string ChatFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/ChatFrmae01.png";
        private const string ItemSlotFramePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/ItemFrame01_Frame_d1.png";

        // Buttons
        private const string TabActiveBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_MenuButton_Rectangle00_s.png";
        private const string TabInactiveBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_MenuButton_Rectangle00_n.png";
        private const string NavyRectBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_Rectangle04_Navy.png";
        private const string DarkSquareBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_IconButton_Square.png";
        private const string YellowBtnPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Common/Btn_Rectangle00_n_Yellow.png";

        // Picto Icons
        private const string IconMicPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Mic.Png";
        private const string IconXPath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Symbol_X.Png";
        private const string IconMutePath = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Sound_Off.Png";

        [MenuItem("Tools/MySecondWorld/Setup NPC Dialogue Canvas Layout")]
        public static void SetupDialogueCanvas()
        {
            Debug.Log("[NPCDialogueCanvasSetup] === 피그마 01 PC 대화 패널 & 02 추천 카드 정밀 스타일링 시작 ===");

            var fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
            var fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRegularPath);

            var popupFrame = AssetDatabase.LoadAssetAtPath<Sprite>(PopupDarkFramePath);
            var innerFrame = AssetDatabase.LoadAssetAtPath<Sprite>(InnerFramePath);
            var passFrame = AssetDatabase.LoadAssetAtPath<Sprite>(PassFramePath);
            var chatFrame = AssetDatabase.LoadAssetAtPath<Sprite>(ChatFramePath);
            var itemSlotFrame = AssetDatabase.LoadAssetAtPath<Sprite>(ItemSlotFramePath);

            var tabActiveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TabActiveBtnPath);
            var tabInactiveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TabInactiveBtnPath);
            var navyRectBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NavyRectBtnPath);
            var darkSquareBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DarkSquareBtnPath);
            var yellowBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(YellowBtnPath);

            var iconMic = AssetDatabase.LoadAssetAtPath<Sprite>(IconMicPath);
            var iconX = AssetDatabase.LoadAssetAtPath<Sprite>(IconXPath);
            var iconMute = AssetDatabase.LoadAssetAtPath<Sprite>(IconMutePath);

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

                // 구버전 잔재 정리
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child.name.Trim() == "RecommendCard" && child.name != "DialoguePanel")
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                var dialoguePanelTrans = root.transform.Find("DialoguePanel") as RectTransform;
                if (dialoguePanelTrans == null)
                {
                    Debug.LogError("[NPCDialogueCanvasSetup] DialoguePanel을 찾을 수 없습니다.");
                    return;
                }

                for (int i = dialoguePanelTrans.childCount - 1; i >= 0; i--)
                {
                    var child = dialoguePanelTrans.GetChild(i);
                    if (child.name == "RecommendCard ")
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                // =========================================================================
                // 1. DialoguePanel (Width: 440, Height: 전체의 92%)
                // =========================================================================
                dialoguePanelTrans.anchorMin = new Vector2(1f, 0.04f);
                dialoguePanelTrans.anchorMax = new Vector2(1f, 0.96f);
                dialoguePanelTrans.pivot = new Vector2(1f, 0.5f);
                dialoguePanelTrans.anchoredPosition = new Vector2(-28f, 0f);
                dialoguePanelTrans.sizeDelta = new Vector2(440f, 0f);

                var panelImg = dialoguePanelTrans.GetComponent<Image>();
                if (panelImg != null && popupFrame != null)
                {
                    panelImg.sprite = popupFrame;
                    panelImg.type = Image.Type.Sliced;
                    panelImg.color = new Color(0.06f, 0.10f, 0.14f, 0.98f);
                }

                // =========================================================================
                // 2. 상단 헤더: Subtitle, NPCName & CloseBtn (X)
                // =========================================================================
                var nameBadgeGo = GetOrCreateChild(dialoguePanelTrans, "NameBadge");
                var nameBadgeRect = nameBadgeGo.GetComponent<RectTransform>();
                nameBadgeRect.anchorMin = new Vector2(0f, 1f);
                nameBadgeRect.anchorMax = new Vector2(0f, 1f);
                nameBadgeRect.pivot = new Vector2(0f, 1f);
                nameBadgeRect.anchoredPosition = new Vector2(24f, -14f);
                nameBadgeRect.sizeDelta = new Vector2(300f, 64f);

                var badgeImg = nameBadgeGo.GetComponent<Image>() ?? nameBadgeGo.AddComponent<Image>();
                badgeImg.color = new Color(0, 0, 0, 0); // 투명 처리

                // Subtitle
                var subGo = GetOrCreateChild(nameBadgeRect, "Subtitle");
                var subTmp = subGo.GetComponent<TextMeshProUGUI>() ?? subGo.AddComponent<TextMeshProUGUI>();
                subTmp.text = "브램블 마을 · 대장장이";
                subTmp.fontSize = 14;
                if (fontRegular != null) subTmp.font = fontRegular;
                subTmp.color = new Color(0.92f, 0.76f, 0.48f, 1f); // 피그마 골드 텍스트
                var subRect = subGo.GetComponent<RectTransform>();
                subRect.anchorMin = new Vector2(0f, 1f);
                subRect.anchorMax = new Vector2(1f, 1f);
                subRect.pivot = new Vector2(0f, 1f);
                subRect.anchoredPosition = new Vector2(0f, 0f);
                subRect.sizeDelta = new Vector2(0f, 20f);

                // NPCName
                var nameTrans = dialoguePanelTrans.Find("NPCName") as RectTransform;
                if (nameTrans == null)
                    nameTrans = nameBadgeGo.transform.Find("NPCName") as RectTransform;

                if (nameTrans != null)
                {
                    nameTrans.SetParent(nameBadgeRect, false);
                    nameTrans.anchorMin = new Vector2(0f, 0f);
                    nameTrans.anchorMax = new Vector2(1f, 1f);
                    nameTrans.pivot = new Vector2(0f, 0f);
                    nameTrans.anchoredPosition = new Vector2(0f, 0f);
                    nameTrans.sizeDelta = new Vector2(0f, -22f);

                    var tmp = nameTrans.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = "로웬";
                        if (fontBold != null) tmp.font = fontBold;
                        tmp.fontSize = 28;
                        tmp.fontStyle = FontStyles.Bold;
                        tmp.color = new Color(0.96f, 0.95f, 0.91f, 1f); // 피그마 아이보리
                        tmp.alignment = TextAlignmentOptions.Left;
                    }
                }

                // CloseBtn -> 다크 스퀘어 버튼 (피그마 48x48 규격)
                var closeTrans = dialoguePanelTrans.Find("CloseBtn") as RectTransform;
                if (closeTrans != null)
                {
                    closeTrans.anchorMin = new Vector2(1f, 1f);
                    closeTrans.anchorMax = new Vector2(1f, 1f);
                    closeTrans.pivot = new Vector2(1f, 1f);
                    closeTrans.anchoredPosition = new Vector2(-24f, -16f);
                    closeTrans.sizeDelta = new Vector2(46f, 46f);

                    var btnImg = closeTrans.GetComponent<Image>();
                    if (btnImg != null && darkSquareBtnSprite != null)
                    {
                        btnImg.sprite = darkSquareBtnSprite;
                        btnImg.type = Image.Type.Sliced;
                        btnImg.color = Color.white;
                    }

                    var oldTmp = closeTrans.GetComponentInChildren<TextMeshProUGUI>();
                    if (oldTmp != null) oldTmp.gameObject.SetActive(false);

                    var iconGo = GetOrCreateChild(closeTrans, "Icon");
                    var iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(20f, 20f);
                    iconRect.anchoredPosition = Vector2.zero;

                    var iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
                    if (iconX != null)
                    {
                        iconImg.sprite = iconX;
                        iconImg.color = Color.white;
                    }
                }

                // =========================================================================
                // 3. 상단 탭바: [대화 기록] (왼쪽) & [기억] (오른쪽) 탭 버튼 2개
                // =========================================================================
                var tabBarGo = GetOrCreateChild(dialoguePanelTrans, "TabBar");
                var tabBarRect = tabBarGo.GetComponent<RectTransform>();
                tabBarRect.anchorMin = new Vector2(0f, 1f);
                tabBarRect.anchorMax = new Vector2(1f, 1f);
                tabBarRect.pivot = new Vector2(0.5f, 1f);
                tabBarRect.anchoredPosition = new Vector2(0f, -80f);
                tabBarRect.sizeDelta = new Vector2(-48f, 48f);

                // 왼쪽 탭: [대화 기록] (활성 탭: 골드 테두리 버튼)
                var tabHistoryGo = GetOrCreateChild(tabBarRect, "TabHistoryBtn");
                var tabHistoryRect = tabHistoryGo.GetComponent<RectTransform>();
                tabHistoryRect.anchorMin = new Vector2(0f, 0f);
                tabHistoryRect.anchorMax = new Vector2(0.5f, 1f);
                tabHistoryRect.offsetMin = Vector2.zero;
                tabHistoryRect.offsetMax = new Vector2(-4f, 0f);

                var tabHisImg = tabHistoryGo.GetComponent<Image>() ?? tabHistoryGo.AddComponent<Image>();
                tabHisImg.color = Color.white;
                if (tabActiveSprite != null)
                {
                    tabHisImg.sprite = tabActiveSprite;
                    tabHisImg.type = Image.Type.Sliced;
                }
                var tabHistoryBtn = tabHistoryGo.GetComponent<Button>() ?? tabHistoryGo.AddComponent<Button>();
                var tabHisText = GetOrCreateCenterTMP(tabHistoryRect, "Text (TMP)", "대화 기록", 17, fontBold);
                tabHisText.color = new Color(1f, 0.92f, 0.55f, 1f);

                // 오른쪽 탭: [기억] (비활성 탭: 다크 네이비 버튼)
                var tabMemoryGo = GetOrCreateChild(tabBarRect, "TabMemoryBtn");
                var tabMemoryRect = tabMemoryGo.GetComponent<RectTransform>();
                tabMemoryRect.anchorMin = new Vector2(0.5f, 0f);
                tabMemoryRect.anchorMax = new Vector2(1f, 1f);
                tabMemoryRect.offsetMin = new Vector2(4f, 0f);
                tabMemoryRect.offsetMax = Vector2.zero;

                var tabMemImg = tabMemoryGo.GetComponent<Image>() ?? tabMemoryGo.AddComponent<Image>();
                tabMemImg.color = Color.white;
                if (tabInactiveSprite != null)
                {
                    tabMemImg.sprite = tabInactiveSprite;
                    tabMemImg.type = Image.Type.Sliced;
                }
                var tabMemoryBtn = tabMemoryGo.GetComponent<Button>() ?? tabMemoryGo.AddComponent<Button>();
                var tabMemText = GetOrCreateCenterTMP(tabMemoryRect, "Text (TMP)", "기억", 17, fontBold);
                tabMemText.color = new Color(0.70f, 0.78f, 0.85f, 0.85f);

                // =========================================================================
                // 4. ChatSection (실시간 채팅창 + 하단 큼직한 입력창/버튼)
                // =========================================================================
                var chatSectionGo = GetOrCreateChild(dialoguePanelTrans, "ChatSection");
                var chatSectionRect = chatSectionGo.GetComponent<RectTransform>();
                chatSectionRect.anchorMin = Vector2.zero;
                chatSectionRect.anchorMax = Vector2.one;
                chatSectionRect.offsetMin = Vector2.zero;
                chatSectionRect.offsetMax = new Vector2(0f, -136f);

                // 4-A. 메인 스크롤뷰
                var scrollTrans = (dialoguePanelTrans.Find("Scroll View") ?? chatSectionRect.Find("Scroll View")) as RectTransform;
                if (scrollTrans != null)
                {
                    scrollTrans.SetParent(chatSectionRect, false);
                    scrollTrans.anchorMin = new Vector2(0f, 0f);
                    scrollTrans.anchorMax = new Vector2(1f, 1f);
                    scrollTrans.pivot = new Vector2(0.5f, 0.5f);
                    scrollTrans.offsetMin = new Vector2(24f, 166f);
                    scrollTrans.offsetMax = new Vector2(-24f, 0f);

                    var scrollImg = scrollTrans.GetComponent<Image>();
                    if (scrollImg != null && chatFrame != null)
                    {
                        scrollImg.sprite = chatFrame;
                        scrollImg.type = Image.Type.Sliced;
                        scrollImg.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);
                    }

                    var viewport = scrollTrans.Find("Viewport");
                    if (viewport != null)
                    {
                        var content = viewport.Find("Content");
                        if (content != null)
                        {
                            var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                            vlg.padding = new RectOffset(10, 10, 14, 14);
                            vlg.spacing = 12;
                            vlg.childAlignment = TextAnchor.UpperLeft;
                            vlg.childControlWidth = true;
                            vlg.childControlHeight = true;
                            vlg.childForceExpandWidth = true;
                            vlg.childForceExpandHeight = false;

                            var csf = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        }
                    }
                }

                // 4-B. 하단 입력 영역 (높이 60px 큼직한 버튼 3개 + 58px 입력창)
                var inputTrans = (dialoguePanelTrans.Find("PlayerInputField") ?? chatSectionRect.Find("PlayerInputField")) as RectTransform;
                var submitTrans = (dialoguePanelTrans.Find("SubmitBtn") ?? chatSectionRect.Find("SubmitBtn")) as RectTransform;
                var sttTrans = (dialoguePanelTrans.Find("STTBtn") ?? chatSectionRect.Find("STTBtn")) as RectTransform;
                var ttsStopTrans = (dialoguePanelTrans.Find("TTSStopBtn") ?? chatSectionRect.Find("TTSStopBtn")) as RectTransform;

                if (inputTrans != null)
                {
                    inputTrans.SetParent(chatSectionRect, false);
                    inputTrans.anchorMin = new Vector2(0f, 0f);
                    inputTrans.anchorMax = new Vector2(1f, 0f);
                    inputTrans.pivot = new Vector2(0.5f, 0f);
                    inputTrans.anchoredPosition = new Vector2(0f, 88f);
                    inputTrans.sizeDelta = new Vector2(-48f, 58f);

                    var inputImg = inputTrans.GetComponent<Image>();
                    if (inputImg != null && innerFrame != null)
                    {
                        inputImg.sprite = innerFrame;
                        inputImg.type = Image.Type.Sliced;
                        inputImg.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);
                    }

                    var textComponent = inputTrans.GetComponent<TMP_InputField>()?.textComponent;
                    if (textComponent != null) textComponent.fontSize = 19;
                    var placeholder = inputTrans.GetComponent<TMP_InputField>()?.placeholder as TextMeshProUGUI;
                    if (placeholder != null) placeholder.fontSize = 19;
                }

                // 아랫줄 1번: TTSStopBtn (읽기 중지 -> 스피커 뮤트 아이콘: 60 × 60px 왼쪽 배치)
                if (ttsStopTrans != null)
                {
                    ttsStopTrans.SetParent(chatSectionRect, false);
                    ttsStopTrans.anchorMin = new Vector2(0f, 0f);
                    ttsStopTrans.anchorMax = new Vector2(0f, 0f);
                    ttsStopTrans.pivot = new Vector2(0f, 0f);
                    ttsStopTrans.anchoredPosition = new Vector2(24f, 16f);
                    ttsStopTrans.sizeDelta = new Vector2(60f, 60f);

                    var btnImg = ttsStopTrans.GetComponent<Image>();
                    btnImg.color = Color.white;
                    if (navyRectBtnSprite != null)
                    {
                        btnImg.sprite = navyRectBtnSprite;
                        btnImg.type = Image.Type.Sliced;
                    }

                    var oldTmp = ttsStopTrans.GetComponentInChildren<TextMeshProUGUI>();
                    if (oldTmp != null) oldTmp.gameObject.SetActive(false);

                    var iconGo = GetOrCreateChild(ttsStopTrans, "Icon");
                    iconGo.SetActive(true);
                    var iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(30f, 30f);
                    iconRect.anchoredPosition = Vector2.zero;

                    var iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
                    if (iconMute != null)
                    {
                        iconImg.sprite = iconMute;
                        iconImg.color = new Color(0.95f, 0.85f, 0.85f, 1f);
                    }
                }

                // 아랫줄 2번: STTBtn (음성 마이크 -> 118 × 60px 대형 음성 입력 버튼으로 확대!)
                if (sttTrans != null)
                {
                    sttTrans.SetParent(chatSectionRect, false);
                    sttTrans.anchorMin = new Vector2(0f, 0f);
                    sttTrans.anchorMax = new Vector2(0f, 0f);
                    sttTrans.pivot = new Vector2(0f, 0f);
                    sttTrans.anchoredPosition = new Vector2(92f, 16f);
                    sttTrans.sizeDelta = new Vector2(118f, 60f);

                    var btnImg = sttTrans.GetComponent<Image>();
                    btnImg.color = Color.white;
                    if (navyRectBtnSprite != null)
                    {
                        btnImg.sprite = navyRectBtnSprite;
                        btnImg.type = Image.Type.Sliced;
                    }

                    var iconGo = GetOrCreateChild(sttTrans, "Icon");
                    iconGo.SetActive(true);
                    var iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(26f, 26f);
                    iconRect.anchoredPosition = new Vector2(-24f, 0f);

                    var iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
                    if (iconMic != null)
                    {
                        iconImg.sprite = iconMic;
                        iconImg.color = Color.white;
                    }

                    var txt = GetOrCreateCenterTMP(sttTrans, "Text (TMP)", "음성", 17, fontBold);
                    txt.gameObject.SetActive(true);
                    txt.color = new Color(0.96f, 0.95f, 0.91f, 1f);
                    var txtRect = txt.rectTransform;
                    txtRect.anchorMin = new Vector2(0.5f, 0.5f);
                    txtRect.anchorMax = new Vector2(0.5f, 0.5f);
                    txtRect.pivot = new Vector2(0.5f, 0.5f);
                    txtRect.anchoredPosition = new Vector2(18f, 0f);
                    txtRect.sizeDelta = new Vector2(56f, 32f);
                }

                // SubmitBtn (전송 ↗: 196 × 60px 피그마 골드 옐로우 버튼)
                if (submitTrans != null)
                {
                    submitTrans.SetParent(chatSectionRect, false);
                    submitTrans.anchorMin = new Vector2(1f, 0f);
                    submitTrans.anchorMax = new Vector2(1f, 0f);
                    submitTrans.pivot = new Vector2(1f, 0f);
                    submitTrans.anchoredPosition = new Vector2(-24f, 16f);
                    submitTrans.sizeDelta = new Vector2(196f, 60f);

                    var btnImg = submitTrans.GetComponent<Image>();
                    btnImg.color = Color.white;
                    if (yellowBtnSprite != null)
                    {
                        btnImg.sprite = yellowBtnSprite;
                        btnImg.type = Image.Type.Sliced;
                    }

                    var tmp = GetOrCreateCenterTMP(submitTrans, "Text (TMP)", "전송 ↗", 19, fontBold);
                    tmp.gameObject.SetActive(true);
                    tmp.color = new Color(0.14f, 0.11f, 0.06f, 1f);
                }

                // =========================================================================
                // 5. MemorySection (호감도 + 첫만남 + KnownFacts 스크롤뷰)
                // =========================================================================
                var memorySectionTrans = (dialoguePanelTrans.Find("MemorySection") ?? dialoguePanelTrans.Find("LeftSection")) as RectTransform;
                if (memorySectionTrans == null)
                    memorySectionTrans = GetOrCreateChild(dialoguePanelTrans, "MemorySection").GetComponent<RectTransform>();

                memorySectionTrans.name = "MemorySection";
                memorySectionTrans.anchorMin = Vector2.zero;
                memorySectionTrans.anchorMax = Vector2.one;
                memorySectionTrans.offsetMin = new Vector2(24f, 20f);
                memorySectionTrans.offsetMax = new Vector2(-24f, -136f);

                var memSectionImg = memorySectionTrans.GetComponent<Image>() ?? memorySectionTrans.gameObject.AddComponent<Image>();
                if (innerFrame != null)
                {
                    memSectionImg.sprite = innerFrame;
                    memSectionImg.type = Image.Type.Sliced;
                    memSectionImg.color = new Color(0.08f, 0.12f, 0.18f, 0.96f);
                }

                for (int i = memorySectionTrans.childCount - 1; i >= 0; i--)
                {
                    var child = memorySectionTrans.GetChild(i);
                    if (child.name != "SummaryCard" && child.name != "KnownFactsScrollView")
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                // 5-A. 상단 요약 카드 (호감도 & 첫만남)
                var summaryCardGo = GetOrCreateChild(memorySectionTrans, "SummaryCard");
                var summaryRect = summaryCardGo.GetComponent<RectTransform>();
                summaryRect.anchorMin = new Vector2(0f, 1f);
                summaryRect.anchorMax = new Vector2(1f, 1f);
                summaryRect.pivot = new Vector2(0.5f, 1f);
                summaryRect.anchoredPosition = new Vector2(0f, -12f);
                summaryRect.sizeDelta = new Vector2(-24f, 96f);

                var summaryImg = summaryCardGo.GetComponent<Image>() ?? summaryCardGo.AddComponent<Image>();
                if (passFrame != null)
                {
                    summaryImg.sprite = passFrame;
                    summaryImg.type = Image.Type.Sliced;
                    summaryImg.color = new Color(0.12f, 0.18f, 0.25f, 0.98f);
                }

                var relText = GetOrCreateTMP(summaryRect, "RelationLabel", "호감도 : +38 (호감)", 18, fontBold);
                relText.color = new Color(0.95f, 0.85f, 0.45f, 1f);
                var relTextRect = relText.rectTransform;
                relTextRect.anchorMin = new Vector2(0f, 1f);
                relTextRect.anchorMax = new Vector2(1f, 1f);
                relTextRect.pivot = new Vector2(0f, 1f);
                relTextRect.anchoredPosition = new Vector2(16f, -14f);
                relTextRect.sizeDelta = new Vector2(-32f, 28f);

                var firstMetText = GetOrCreateTMP(summaryRect, "FirstMetText", "첫 만남 : 마을 광장 · 누적 대화 14회", 15, fontRegular);
                firstMetText.color = new Color(0.75f, 0.82f, 0.90f, 1f);
                var firstMetRect = firstMetText.rectTransform;
                firstMetRect.anchorMin = new Vector2(0f, 0f);
                firstMetRect.anchorMax = new Vector2(1f, 0f);
                firstMetRect.pivot = new Vector2(0f, 0f);
                firstMetRect.anchoredPosition = new Vector2(16f, 14f);
                firstMetRect.sizeDelta = new Vector2(-32f, 26f);

                // 5-B. 하단 KnownFacts 전용 스크롤뷰
                var kfScrollGo = GetOrCreateChild(memorySectionTrans, "KnownFactsScrollView");
                var kfScrollRect = kfScrollGo.GetComponent<RectTransform>();
                kfScrollRect.anchorMin = Vector2.zero;
                kfScrollRect.anchorMax = Vector2.one;
                kfScrollRect.offsetMin = new Vector2(12f, 12f);
                kfScrollRect.offsetMax = new Vector2(-12f, -120f);

                var kfScrollImg = kfScrollGo.GetComponent<Image>() ?? kfScrollGo.AddComponent<Image>();
                if (chatFrame != null)
                {
                    kfScrollImg.sprite = chatFrame;
                    kfScrollImg.type = Image.Type.Sliced;
                    kfScrollImg.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);
                }

                var kfScroll = kfScrollGo.GetComponent<ScrollRect>() ?? kfScrollGo.AddComponent<ScrollRect>();
                kfScroll.horizontal = false;
                kfScroll.vertical = true;

                var kfViewportGo = GetOrCreateChild(kfScrollRect, "Viewport");
                var kfViewportRect = kfViewportGo.GetComponent<RectTransform>();
                kfViewportRect.anchorMin = Vector2.zero;
                kfViewportRect.anchorMax = Vector2.one;
                kfViewportRect.offsetMin = new Vector2(10f, 10f);
                kfViewportRect.offsetMax = new Vector2(-10f, -10f);
                var mask = kfViewportGo.GetComponent<Mask>() ?? kfViewportGo.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var maskImg = kfViewportGo.GetComponent<Image>() ?? kfViewportGo.AddComponent<Image>();

                var kfContentGo = GetOrCreateChild(kfViewportRect, "Content");
                var kfContentRect = kfContentGo.GetComponent<RectTransform>();
                kfContentRect.anchorMin = new Vector2(0f, 1f);
                kfContentRect.anchorMax = new Vector2(1f, 1f);
                kfContentRect.pivot = new Vector2(0.5f, 1f);
                kfContentRect.anchoredPosition = Vector2.zero;
                kfContentRect.sizeDelta = new Vector2(0f, 100f);

                var kfVlg = kfContentGo.GetComponent<VerticalLayoutGroup>() ?? kfContentGo.AddComponent<VerticalLayoutGroup>();
                kfVlg.padding = new RectOffset(6, 6, 6, 6);
                kfVlg.spacing = 8;
                kfVlg.childAlignment = TextAnchor.UpperLeft;
                kfVlg.childControlWidth = true;
                kfVlg.childControlHeight = true;
                kfVlg.childForceExpandWidth = true;
                kfVlg.childForceExpandHeight = false;

                var kfCsf = kfContentGo.GetComponent<ContentSizeFitter>() ?? kfContentGo.AddComponent<ContentSizeFitter>();
                kfCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                kfScroll.viewport = kfViewportRect;
                kfScroll.content = kfContentRect;

                var knownFactsTmp = GetOrCreateTMP(kfContentRect, "KnownFactsText", "기억하고 있는 사실 :\n• 북쪽 숲으로 향하는 여행자", 16, fontRegular);
                knownFactsTmp.color = new Color(0.85f, 0.90f, 0.95f, 1f);
                knownFactsTmp.lineSpacing = 18f;

                memorySectionTrans.gameObject.SetActive(false);

                // =========================================================================
                // 6. 피그마 02 규격 RecommendCard (360 × 165px)
                // =========================================================================
                var recommendCardGo = GetOrCreateChild(dialoguePanelTrans, "RecommendCard");
                var cardRect = recommendCardGo.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0f, 0.5f);
                cardRect.anchorMax = new Vector2(0f, 0.5f);
                cardRect.pivot = new Vector2(1f, 0.5f);
                cardRect.anchoredPosition = new Vector2(-16f, 30f);
                cardRect.sizeDelta = new Vector2(360f, 165f);

                var cardBg = recommendCardGo.GetComponent<Image>() ?? recommendCardGo.AddComponent<Image>();
                cardBg.color = new Color(0.06f, 0.11f, 0.16f, 0.98f);
                if (popupFrame != null)
                {
                    cardBg.sprite = popupFrame;
                    cardBg.type = Image.Type.Sliced;
                }

                // 상단 태그 라벨
                var cardTag = GetOrCreateTMP(cardRect, "TagLabel", "★ 권장 장비 추천", 14, fontBold);
                cardTag.color = new Color(0.92f, 0.76f, 0.48f, 1f);
                var tagRect = cardTag.rectTransform;
                tagRect.anchorMin = new Vector2(0f, 1f);
                tagRect.anchorMax = new Vector2(0f, 1f);
                tagRect.pivot = new Vector2(0f, 1f);
                tagRect.anchoredPosition = new Vector2(18f, -14f);
                tagRect.sizeDelta = new Vector2(180f, 22f);

                // 닫기 버튼 (34x34 다크 스퀘어 버튼)
                var closeBtnRecommendGo = GetOrCreateChild(cardRect, "CloseButton");
                var closeBtnRecommendRect = closeBtnRecommendGo.GetComponent<RectTransform>();
                closeBtnRecommendRect.anchorMin = new Vector2(1f, 1f);
                closeBtnRecommendRect.anchorMax = new Vector2(1f, 1f);
                closeBtnRecommendRect.pivot = new Vector2(1f, 1f);
                closeBtnRecommendRect.anchoredPosition = new Vector2(-12f, -12f);
                closeBtnRecommendRect.sizeDelta = new Vector2(34f, 34f);

                var closeBtnRecommendImg = closeBtnRecommendGo.GetComponent<Image>() ?? closeBtnRecommendGo.AddComponent<Image>();
                closeBtnRecommendImg.color = Color.white;
                if (darkSquareBtnSprite != null)
                {
                    closeBtnRecommendImg.sprite = darkSquareBtnSprite;
                    closeBtnRecommendImg.type = Image.Type.Sliced;
                }
                var recommendCloseBtn = closeBtnRecommendGo.GetComponent<Button>() ?? closeBtnRecommendGo.AddComponent<Button>();

                var closeIconGo = GetOrCreateChild(closeBtnRecommendRect, "Icon");
                var closeIconRect = closeIconGo.GetComponent<RectTransform>();
                closeIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                closeIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                closeIconRect.sizeDelta = new Vector2(16f, 16f);
                closeIconRect.anchoredPosition = Vector2.zero;

                var closeIconImg = closeIconGo.GetComponent<Image>() ?? closeIconGo.AddComponent<Image>();
                if (iconX != null)
                {
                    closeIconImg.sprite = iconX;
                    closeIconImg.color = Color.white;
                }

                // 좌측 아이템 슬롯 (76 × 76px ItemFrame01_Frame_d1)
                var iconFrameGo = GetOrCreateChild(cardRect, "IconFrame");
                var iconFrameRect = iconFrameGo.GetComponent<RectTransform>();
                iconFrameRect.anchorMin = new Vector2(0f, 0f);
                iconFrameRect.anchorMax = new Vector2(0f, 0f);
                iconFrameRect.pivot = new Vector2(0f, 0f);
                iconFrameRect.anchoredPosition = new Vector2(18f, 18f);
                iconFrameRect.sizeDelta = new Vector2(76f, 76f);

                var iconFrameImg = iconFrameGo.GetComponent<Image>() ?? iconFrameGo.AddComponent<Image>();
                iconFrameImg.color = Color.white;
                if (itemSlotFrame != null)
                {
                    iconFrameImg.sprite = itemSlotFrame;
                    iconFrameImg.type = Image.Type.Sliced;
                }

                var iconGoRecommend = GetOrCreateChild(iconFrameRect, "Icon");
                var iconRectRecommend = iconGoRecommend.GetComponent<RectTransform>();
                iconRectRecommend.anchorMin = Vector2.zero;
                iconRectRecommend.anchorMax = Vector2.one;
                iconRectRecommend.offsetMin = new Vector2(6f, 6f);
                iconRectRecommend.offsetMax = new Vector2(-6f, -6f);
                var recommendIconImg = iconGoRecommend.GetComponent<Image>() ?? iconGoRecommend.AddComponent<Image>();
                recommendIconImg.preserveAspect = true;

                // 아이템명 (카드 내부 위치: x: 106, y: 62)
                var recommendNameTmp = GetOrCreateTMP(cardRect, "NameText", "아이템 이름", 19, fontBold);
                recommendNameTmp.color = Color.white;
                var nameRectRecommend = recommendNameTmp.rectTransform;
                nameRectRecommend.anchorMin = new Vector2(0f, 0f);
                nameRectRecommend.anchorMax = new Vector2(1f, 0f);
                nameRectRecommend.pivot = new Vector2(0f, 0f);
                nameRectRecommend.anchoredPosition = new Vector2(106f, 62f);
                nameRectRecommend.sizeDelta = new Vector2(-120f, 32f);

                // 구매 버튼 (190 × 44px 피그마 골드 버튼)
                var buyBtnGo = GetOrCreateChild(cardRect, "BuyButton");
                var buyBtnRect = buyBtnGo.GetComponent<RectTransform>();
                buyBtnRect.anchorMin = new Vector2(0f, 0f);
                buyBtnRect.anchorMax = new Vector2(0f, 0f);
                buyBtnRect.pivot = new Vector2(0f, 0f);
                buyBtnRect.anchoredPosition = new Vector2(106f, 18f);
                buyBtnRect.sizeDelta = new Vector2(190f, 44f);

                var buyBtnImg = buyBtnGo.GetComponent<Image>() ?? buyBtnGo.AddComponent<Image>();
                buyBtnImg.color = Color.white;
                if (yellowBtnSprite != null)
                {
                    buyBtnImg.sprite = yellowBtnSprite;
                    buyBtnImg.type = Image.Type.Sliced;
                }
                var recommendBuyBtn = buyBtnGo.GetComponent<Button>() ?? buyBtnGo.AddComponent<Button>();
                var buyText = GetOrCreateCenterTMP(buyBtnRect, "Text (TMP)", "구매", 17, fontBold);
                buyText.color = new Color(0.14f, 0.11f, 0.06f, 1f);

                recommendCardGo.SetActive(false);

                // =========================================================================
                // 7. 직렬화 바인딩
                // =========================================================================
                var so = new SerializedObject(dialogueUI);
                BindField(so, "_canvas", dialoguePanelTrans.gameObject);
                BindField(so, "_npcNameText", nameTrans?.GetComponent<TextMeshProUGUI>());
                BindField(so, "_chatContent", chatSectionRect.Find("Scroll View/Viewport/Content")?.GetComponent<RectTransform>());
                BindField(so, "_playerInputField", inputTrans?.GetComponent<TMP_InputField>());
                BindField(so, "_submitButton", submitTrans?.GetComponent<Button>());
                BindField(so, "_closeButton", closeTrans?.GetComponent<Button>());
                BindField(so, "_backdropButton", root.transform.Find("Dialogue_Backdrop")?.GetComponent<Button>());
                BindField(so, "_scrollRect", chatSectionRect.Find("Scroll View")?.GetComponent<ScrollRect>());
                BindField(so, "_sttButton", sttTrans?.GetComponent<Button>());
                BindField(so, "_ttsStopButton", ttsStopTrans?.GetComponent<Button>());

                BindField(so, "_tabHistoryBtn", tabHistoryBtn);
                BindField(so, "_tabMemoryBtn", tabMemoryBtn);
                BindField(so, "_chatSection", chatSectionGo);
                BindField(so, "_memorySection", memorySectionTrans.gameObject);

                BindField(so, "_relationText", relText);
                BindField(so, "_firstMetText", firstMetText);
                BindField(so, "_knownFactsText", knownFactsTmp);

                BindField(so, "_recommendCard", recommendCardGo);
                BindField(so, "_recommendIcon", recommendIconImg);
                BindField(so, "_recommendNameText", recommendNameTmp);
                BindField(so, "_recommendBuyButton", recommendBuyBtn);
                BindField(so, "_recommendCloseButton", recommendCloseBtn);

                var npcBubble = AssetDatabase.LoadAssetAtPath<GameObject>(NpcBubblePrefabPath);
                var playerBubble = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBubblePrefabPath);
                var thinkingBubble = AssetDatabase.LoadAssetAtPath<GameObject>(ThinkingBubblePrefabPath);
                if (npcBubble != null) BindField(so, "_npcBubblePrefab", npcBubble);
                if (playerBubble != null) BindField(so, "_playerBubblePrefab", playerBubble);
                if (thinkingBubble != null) BindField(so, "_thinkingBubblePrefab", thinkingBubble);

                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[NPCDialogueCanvasSetup] === 피그마 정품 에셋 완벽 스타일링 적용 완료! ===");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 8. 버블 프리팹 폰트 & 스타일 업데이트
            UpdateBubblePrefabs(fontRegular, fontBold, chatFrame);
        }

        private static void UpdateBubblePrefabs(TMP_FontAsset fontRegular, TMP_FontAsset fontBold, Sprite chatFrame)
        {
            UpdateSingleBubble(NpcBubblePrefabPath, fontRegular, new Color(0.12f, 0.17f, 0.24f, 0.96f), chatFrame, 18);
            UpdateSingleBubble(PlayerBubblePrefabPath, fontRegular, new Color(0.24f, 0.35f, 0.42f, 0.96f), chatFrame, 18);
            UpdateSingleBubble(ThinkingBubblePrefabPath, fontBold, new Color(0.14f, 0.18f, 0.26f, 0.92f), chatFrame, 18);
        }

        private static void UpdateSingleBubble(string path, TMP_FontAsset font, Color bgColor, Sprite frameSprite, float fontSize)
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
                    tmp.fontSize = fontSize;
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
