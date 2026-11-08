using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using MySecondWorld.UI;

namespace MySecondWorld.EditorTools
{
    public static class SetupPaperdollUI
    {
        [MenuItem("Tools/Setup Equipment Paperdoll UI")]
        public static void Execute()
        {
            SetupInventoryCanvas();
            SetupShopCanvas();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SetupPaperdollUI] 2탭 세부 페이퍼돌 및 하단 무기 슬롯 배치가 완료되었습니다!");
        }

        private static void SetupInventoryCanvas()
        {
            string prefabPath = "Assets/03.Prefab/UI/Canvas/Inventory Canvas.prefab";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            bool isStageOpen = stage != null && stage.assetPath == prefabPath;
            GameObject root = isStageOpen ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // 0. CanvasScaler 설정: Match Height (1.0)로 세로 720 고정 -> 어떤 화면비에서도 상하 50px 여백 균일
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1280f, 720f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1.0f;
                }

                // 1. Inventory Items 위치/크기 확인: Right-Center 앵커 (우측 20px 안쪽 고정)
                Transform itemsTransform = null;
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "Inventory Items")
                    {
                        itemsTransform = rt;
                        break;
                    }
                }

                if (itemsTransform != null)
                {
                    RectTransform rt = itemsTransform.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-20f, 0f);
                    rt.sizeDelta = new Vector2(440f, 620f);
                }

                // 1-1. Category_TabBar 탭 순서 및 명칭 동기화: [전체] -> [방어구] -> [악세사리] -> [무기] -> [소모품]
                var tabBar = root.transform.Find("Inventory Panel/Inventory Items/Category_TabBar");
                if (tabBar != null)
                {
                    var tabAll = tabBar.Find("Tab_All");
                    var tabEquip = tabBar.Find("Tab_Equipment");
                    var tabAcc = tabBar.Find("Tab_Accessory");
                    var tabWpn = tabBar.Find("Tab_Weapon");
                    var tabCons = tabBar.Find("Tab_Consumable");

                    if (tabAll != null) { tabAll.gameObject.SetActive(true); tabAll.GetComponentInChildren<TMP_Text>()?.SetText("전체"); tabAll.SetSiblingIndex(0); }
                    if (tabEquip != null) { tabEquip.GetComponentInChildren<TMP_Text>()?.SetText("방어구"); tabEquip.SetSiblingIndex(1); }
                    if (tabAcc != null) { tabAcc.GetComponentInChildren<TMP_Text>()?.SetText("악세사리"); tabAcc.SetSiblingIndex(2); }
                    if (tabWpn != null) { tabWpn.GetComponentInChildren<TMP_Text>()?.SetText("무기"); tabWpn.SetSiblingIndex(3); }
                    if (tabCons != null) { tabCons.GetComponentInChildren<TMP_Text>()?.SetText("소모품"); tabCons.SetSiblingIndex(4); }
                }

                // 2. Equipment Paperdoll 부모 컨테이너
                Transform parentContainer = itemsTransform != null ? itemsTransform.parent : root.transform;
                Transform existingPaperdoll = parentContainer.Find("Equipment Paperdoll");
                if (existingPaperdoll != null)
                {
                    Object.DestroyImmediate(existingPaperdoll.gameObject);
                }

                GameObject paperdollGO = new GameObject("Equipment Paperdoll", typeof(RectTransform));
                paperdollGO.transform.SetParent(parentContainer, false);
                if (itemsTransform != null)
                {
                    paperdollGO.transform.SetSiblingIndex(itemsTransform.GetSiblingIndex());
                }

                RectTransform paperdollRT = paperdollGO.GetComponent<RectTransform>();
                paperdollRT.anchorMin = new Vector2(0.5f, 0.5f);
                paperdollRT.anchorMax = new Vector2(0.5f, 0.5f);
                paperdollRT.pivot = new Vector2(0.5f, 0.5f);
                paperdollRT.anchoredPosition = Vector2.zero;
                paperdollRT.sizeDelta = new Vector2(360f, 620f);

                EquipmentPaperdollUI paperdollComp = paperdollGO.AddComponent<EquipmentPaperdollUI>();

                Sprite squareFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Square02_White1.png");
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("73a6edc2a049a87438dee81c70ca76e4"));

                var allSlotViews = new List<EquipmentPaperdollUI.PaperdollSlotView>();

                // --- 2-1. 단일 통합 슬롯 컨테이너 (Polygon Fantasy Hero 인체 비례 그리드) ---
                GameObject slotsGroupGO = new GameObject("Slots_Container", typeof(RectTransform));
                slotsGroupGO.transform.SetParent(paperdollGO.transform, false);
                RectTransform slotsGroupRT = slotsGroupGO.GetComponent<RectTransform>();
                slotsGroupRT.anchorMin = new Vector2(0.5f, 0.5f);
                slotsGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
                slotsGroupRT.pivot = new Vector2(0.5f, 0.5f);
                slotsGroupRT.anchoredPosition = Vector2.zero;
                slotsGroupRT.sizeDelta = new Vector2(360f, 620f);

                var slotDefs = new (EquipmentSlotType slotType, string name, Vector2 pos, string label, string iconPath, bool isWeapon)[]
                {
                    // Row 1 (y = 185): 머리, 투구장식, 주무기
                    (EquipmentSlotType.HeadCoverings,    "Slot_HeadCoverings",    new Vector2(-42f, 185f),  "HEAD",          "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Knight_Helmet.Png", false),
                    (EquipmentSlotType.HelmetAttachment, "Slot_HelmetAttachment", new Vector2(42f, 185f),   "HELMET ATTACH", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_HelmetAttach.png", false),
                    (EquipmentSlotType.Weapon,           "Slot_Weapon",           new Vector2(126f, 185f),  "WEAPON",        "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Dagger.Png", true),

                    // Row 2 (y = 95): 상완, 상체, 어깨, 보조무기
                    (EquipmentSlotType.ArmUpper,         "Slot_ArmUpper",         new Vector2(-126f, 95f),  "UPPER ARMS",    "Assets/07.Textures/UI/PaperdollIcons/Silhouette_ArmUpper.png", false),
                    (EquipmentSlotType.Torso,            "Slot_Torso",            new Vector2(-42f, 95f),   "TORSO",         "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Armor.Png", false),
                    (EquipmentSlotType.ShoulderAttach,   "Slot_ShoulderAttach",   new Vector2(42f, 95f),    "SHOULDER PADS", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Shoulder.png", false),
                    (EquipmentSlotType.Shield,           "Slot_Shield",           new Vector2(126f, 95f),   "SHIELD",        "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Shield.Png", true),

                    // Row 3 (y = 5): 하완, 하의, 팔꿈치, 등 장식
                    (EquipmentSlotType.ArmLower,         "Slot_ArmLower",         new Vector2(-126f, 5f),   "LOWER ARMS",    "Assets/07.Textures/UI/PaperdollIcons/Silhouette_ArmLower.png", false),
                    (EquipmentSlotType.Hips,             "Slot_Hips",             new Vector2(-42f, 5f),    "HIPS",          "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Pants.png", false),
                    (EquipmentSlotType.ElbowAttach,      "Slot_ElbowAttach",      new Vector2(42f, 5f),     "ELBOW PADS",    "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Elbow.png", false),
                    (EquipmentSlotType.BackAttachment,   "Slot_BackAttachment",   new Vector2(126f, 5f),    "BACK ATTACH",   "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Wing.Png", false),

                    // Row 4 (y = -85): 손, 다리/신발, 무릎 각반, 허리 장식
                    (EquipmentSlotType.Hands,            "Slot_Hands",            new Vector2(-126f, -85f), "HANDS",         "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Hand.Png", false),
                    (EquipmentSlotType.Legs,             "Slot_Legs",             new Vector2(-42f, -85f),  "LEGS",          "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Shoes.Png", false),
                    (EquipmentSlotType.Knees,            "Slot_Knees",            new Vector2(42f, -85f),   "KNEE PADS",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Greaves.png", false),
                    (EquipmentSlotType.HipsAttachment,   "Slot_HipsAttachment",   new Vector2(126f, -85f),  "HIP ATTACH",    "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Belt.png", false)
                };

                foreach (var def in slotDefs)
                {
                    allSlotViews.Add(CreateSlot(def, slotsGroupGO.transform, squareFrameSprite, fontAsset));
                }

                // Reflection 필드 바인딩
                var t = typeof(EquipmentPaperdollUI);
                t.GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(paperdollComp, allSlotViews);

                // InventoryUI에 _paperdollUI 바인딩
                InventoryUI invUI = root.GetComponent<InventoryUI>();
                if (invUI != null)
                {
                    typeof(InventoryUI).GetField("_paperdollUI", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invUI, paperdollComp);
                }

                if (isStageOpen)
                {
                    EditorUtility.SetDirty(root);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("[SetupPaperdollUI] Inventory Canvas 프리팹 갱신 완료");
            }
            finally
            {
                if (!isStageOpen)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }


        private static EquipmentPaperdollUI.PaperdollSlotView CreateSlot(
            (EquipmentSlotType slotType, string name, Vector2 pos, string label, string iconPath, bool isWeapon) def,
            Transform parent,
            Sprite squareFrameSprite,
            TMP_FontAsset fontAsset)
        {
            GameObject slotGO = new GameObject(def.name, typeof(RectTransform), typeof(Image), typeof(Button));
            slotGO.transform.SetParent(parent, false);

            RectTransform slotRT = slotGO.GetComponent<RectTransform>();
            slotRT.anchorMin = new Vector2(0.5f, 0.5f);
            slotRT.anchorMax = new Vector2(0.5f, 0.5f);
            slotRT.pivot = new Vector2(0.5f, 0.5f);
            slotRT.anchoredPosition = def.pos;
            slotRT.sizeDelta = def.isWeapon ? new Vector2(64f, 64f) : new Vector2(60f, 60f);

            // 1. 슬롯 배경 (은은하고 세련된 반투명 다크 틴트 - 참고 사진 스타일)
            Image bgImg = slotGO.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.07f, 0.12f, 0.38f);

            Button btn = slotGO.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            // 2. 평상시 얇은 와이어프레임 사각 테두리 (속이 100% 뚫린 순수 외곽선 4개 라인)
            Color normalLineColor = def.isWeapon 
                ? new Color(1f, 0.85f, 0.35f, 0.65f) 
                : new Color(0.85f, 0.92f, 1f, 0.45f);
            GameObject normalBorderGO = CreateWireframeLines("NormalBorder", slotGO.transform, normalLineColor, 1.5f, 0f);

            // 3. 선택 시 하이라이트 보더 (속이 100% 뚫린 밝은 네온 외곽선 4개 라인 - 장비를 절대 가리지 않음!)
            Color selectLineColor = def.isWeapon 
                ? new Color(1f, 0.88f, 0.2f, 1f) 
                : new Color(0.2f, 0.88f, 1f, 1f);
            GameObject borderGO = CreateWireframeLines("SelectedBorder", slotGO.transform, selectLineColor, 2.5f, 1.5f);
            borderGO.SetActive(false);

            Sprite placeholderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(def.iconPath);

            // 4. 미장착 실루엣 아이콘 (Placeholder)
            GameObject placeholderGO = new GameObject("PlaceholderIcon", typeof(RectTransform), typeof(Image));
            placeholderGO.transform.SetParent(slotGO.transform, false);
            RectTransform phRT = placeholderGO.GetComponent<RectTransform>();
            phRT.anchoredPosition = Vector2.zero;
            phRT.sizeDelta = new Vector2(36f, 36f);
            Image phImg = placeholderGO.GetComponent<Image>();
            phImg.sprite = placeholderSprite;
            phImg.preserveAspect = true;
            phImg.color = def.isWeapon 
                ? new Color(1f, 0.88f, 0.45f, 0.55f) 
                : new Color(1f, 1f, 1f, 0.38f);
            phImg.raycastTarget = false;

            // 5. 장착 아이템 아이콘 (IconImage - 테두리보다 위에서 최상단 렌더링되어 절대 가려지지 않음)
            GameObject iconGO = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(slotGO.transform, false);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(46f, 46f);
            Image iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = placeholderSprite;
            iconImg.preserveAspect = true;
            iconImg.color = new Color(1f, 1f, 1f, 0f);
            iconImg.raycastTarget = false;

            // 6. 상단 중앙 영문 볼드 라벨 (LabelText)
            GameObject labelGO = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(slotGO.transform, false);
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.5f, 1f);
            labelRT.anchorMax = new Vector2(0.5f, 1f);
            labelRT.pivot = new Vector2(0.5f, 0f);
            labelRT.anchoredPosition = new Vector2(0f, 2f); // 슬롯 윗변 바로 위
            labelRT.sizeDelta = new Vector2(76f, 14f);
            TextMeshProUGUI labelTMP = labelGO.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) labelTMP.font = fontAsset;
            labelTMP.text = def.label;
            labelTMP.fontSize = 8.5f;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.color = def.isWeapon 
                ? new Color(1f, 0.88f, 0.4f, 0.95f) 
                : new Color(0.88f, 0.94f, 1f, 0.9f);
            labelTMP.raycastTarget = false;

            return new EquipmentPaperdollUI.PaperdollSlotView
            {
                SlotType = def.slotType,
                Button = btn,
                BackgroundImage = bgImg,
                IconImage = iconImg,
                LabelText = labelTMP,
                SelectedBorder = borderGO,
                DefaultPlaceholderIcon = placeholderSprite
            };
        }

        private static GameObject CreateWireframeLines(string name, Transform parent, Color lineColor, float thickness, float offset)
        {
            GameObject container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-offset, -offset);
            rt.offsetMax = new Vector2(offset, offset);

            // Top Line
            CreateSingleLine("Line_T", container.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, thickness), lineColor);
            // Bottom Line
            CreateSingleLine("Line_B", container.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, thickness), lineColor);
            // Left Line
            CreateSingleLine("Line_L", container.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(thickness, 0f), lineColor);
            // Right Line
            CreateSingleLine("Line_R", container.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(thickness, 0f), lineColor);

            return container;
        }

        private static void CreateSingleLine(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject lineGO = new GameObject(name, typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(parent, false);
            RectTransform rt = lineGO.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = lineGO.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void SetupShopCanvas()
        {
            string prefabPath = "Assets/03.Prefab/UI/Canvas/ShopCanvas.prefab";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            bool isStageOpen = stage != null && stage.assetPath == prefabPath;
            GameObject root = isStageOpen ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // 0. CanvasScaler 설정: Match Height (1.0)로 세로 720 고정 -> 상하 여백 균일
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1280f, 720f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1.0f;
                }

                Transform buyBg = null;
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "Buy Background")
                    {
                        buyBg = rt;
                        break;
                    }
                }

                if (buyBg != null)
                {
                    RectTransform rt = buyBg.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(20f, 0f);
                    rt.sizeDelta = new Vector2(440f, 620f);
                }

                if (isStageOpen)
                {
                    EditorUtility.SetDirty(root);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (!isStageOpen)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
