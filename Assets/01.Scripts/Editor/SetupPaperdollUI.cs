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

                Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("b6280e8fcec29455a896dab7a64e16cf"));
                Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("272b11fbfff364c65afa2d764c799e0b"));
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("73a6edc2a049a87438dee81c70ca76e4"));

                var allSlotViews = new List<EquipmentPaperdollUI.PaperdollSlotView>();

                // --- 2-1. 상단 탭 스위처 (Top-Center 앵커링) ---
                GameObject headerGO = new GameObject("Header_Tabs", typeof(RectTransform));
                headerGO.transform.SetParent(paperdollGO.transform, false);
                RectTransform headerRT = headerGO.GetComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0.5f, 1f);
                headerRT.anchorMax = new Vector2(0.5f, 1f);
                headerRT.pivot = new Vector2(0.5f, 1f);
                headerRT.anchoredPosition = new Vector2(0f, -20f);
                headerRT.sizeDelta = new Vector2(250f, 34f);

                // Btn_Armor
                GameObject btnArmorGO = CreateTabButton("Btn_Armor", headerGO.transform, new Vector2(-60f, 0f), "방어구", circleSprite, fontAsset, out Button btnArmor);
                // Btn_Accessory
                GameObject btnAccGO = CreateTabButton("Btn_Accessory", headerGO.transform, new Vector2(60f, 0f), "악세사리", circleSprite, fontAsset, out Button btnAcc);

                // --- 2-2. 컨테이너 1: 방어구 파츠 8슬롯 ---
                GameObject armorGroupGO = new GameObject("Armor_Slots", typeof(RectTransform));
                armorGroupGO.transform.SetParent(paperdollGO.transform, false);
                RectTransform armorGroupRT = armorGroupGO.GetComponent<RectTransform>();
                armorGroupRT.anchorMin = new Vector2(0.5f, 0.5f);
                armorGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
                armorGroupRT.pivot = new Vector2(0.5f, 0.5f);
                armorGroupRT.anchoredPosition = new Vector2(0f, 15f);
                armorGroupRT.sizeDelta = new Vector2(360f, 440f);

                // 좌측 4슬롯: Left 앵커(x=65), 우측 4슬롯: Right 앵커(x=-65)
                var armorDefs = new (EquipmentSlotType slotType, string name, Vector2 pos, string label, string iconPath, Vector2 anchor)[]
                {
                    (EquipmentSlotType.HeadCoverings, "Slot_HeadCoverings", new Vector2(65f, 155f),  "투구", "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Knight_Helmet.Png", new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.Torso,         "Slot_Torso",         new Vector2(65f, 55f),   "상체", "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Armor.Png",         new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.ArmUpper,      "Slot_ArmUpper",      new Vector2(65f, -45f),  "상완", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_ArmUpper.png",                                                                    new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.ArmLower,      "Slot_ArmLower",      new Vector2(65f, -145f), "하완", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_ArmLower.png",                                                                    new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.Hands,         "Slot_Hands",         new Vector2(-65f, 155f), "장갑", "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Hand.Png",          new Vector2(1f, 0.5f)),
                    (EquipmentSlotType.Hips,          "Slot_Hips",          new Vector2(-65f, 55f),  "하의", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Pants.png",                                                                       new Vector2(1f, 0.5f)),
                    (EquipmentSlotType.Knees,         "Slot_Feet",          new Vector2(-65f, -145f),"무릎 각반", "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Shoes.Png",    new Vector2(1f, 0.5f)),
                    (EquipmentSlotType.Legs,          "Slot_Legs",          new Vector2(-65f, -45f), "신발", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Greaves.png",                                                                     new Vector2(1f, 0.5f)),
                };

                foreach (var def in armorDefs)
                {
                    allSlotViews.Add(CreateSlot(def, armorGroupGO.transform, frameSprite, circleSprite, fontAsset, false));
                }

                // --- 2-3. 컨테이너 2: 악세사리 파츠 8슬롯 ---
                GameObject accGroupGO = new GameObject("Accessory_Slots", typeof(RectTransform));
                accGroupGO.transform.SetParent(paperdollGO.transform, false);
                RectTransform accGroupRT = accGroupGO.GetComponent<RectTransform>();
                accGroupRT.anchorMin = new Vector2(0.5f, 0.5f);
                accGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
                accGroupRT.pivot = new Vector2(0.5f, 0.5f);
                accGroupRT.anchoredPosition = new Vector2(0f, 15f);
                accGroupRT.sizeDelta = new Vector2(360f, 440f);

                var accDefs = new (EquipmentSlotType slotType, string name, Vector2 pos, string label, string iconPath, Vector2 anchor)[]
                {
                    (EquipmentSlotType.HelmetAttachment, "Slot_HelmetAttachment", new Vector2(65f, 155f),  "투구장식", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_HelmetAttach.png",                                        new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.ShoulderAttach,   "Slot_ShoulderAttach",   new Vector2(65f, 55f),   "어깨",     "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Shoulder.png",                                            new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.ElbowAttach,      "Slot_ElbowAttach",      new Vector2(65f, -45f),  "팔꿈치",   "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Elbow.png",                                               new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.BackAttachment,   "Slot_BackAttachment",   new Vector2(65f, -145f), "등장식",   "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Wing.Png", new Vector2(0f, 0.5f)),
                    (EquipmentSlotType.HipsAttachment,   "Slot_HipsAttachment",   new Vector2(-65f, 155f), "허리장식", "Assets/07.Textures/UI/PaperdollIcons/Silhouette_Belt.png",                                               new Vector2(1f, 0.5f)),
                    (EquipmentSlotType.Ear,              "Slot_Ear",              new Vector2(-65f, 55f),  "이어링",   "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Earing.Png",       new Vector2(1f, 0.5f)),
                    (EquipmentSlotType.Ring,             "Slot_Ring",             new Vector2(-65f, -45f), "반지",     "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Ring.Png",         new Vector2(1f, 0.5f)),
                    (EquipmentSlotType.Necklace,         "Slot_Necklace",         new Vector2(-65f, -145f),"목걸이",   "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Necklace_1.Png",   new Vector2(1f, 0.5f))
                };

                foreach (var def in accDefs)
                {
                    allSlotViews.Add(CreateSlot(def, accGroupGO.transform, frameSprite, circleSprite, fontAsset, false));
                }
                accGroupGO.SetActive(false); // 기본은 방어구 탭 표시

                // --- 2-4. 컨테이너 3: 하단 발밑 상시 고정 무기 2슬롯 (Bottom-Center 앵커링) ---
                GameObject wepGroupGO = new GameObject("Weapon_Slots", typeof(RectTransform));
                wepGroupGO.transform.SetParent(paperdollGO.transform, false);
                RectTransform wepGroupRT = wepGroupGO.GetComponent<RectTransform>();
                wepGroupRT.anchorMin = new Vector2(0.5f, 0f);
                wepGroupRT.anchorMax = new Vector2(0.5f, 0f);
                wepGroupRT.pivot = new Vector2(0.5f, 0f);
                wepGroupRT.anchoredPosition = new Vector2(0f, 25f);
                wepGroupRT.sizeDelta = new Vector2(240f, 80f);

                var wepDefs = new (EquipmentSlotType slotType, string name, Vector2 pos, string label, string iconPath, Vector2 anchor)[]
                {
                    (EquipmentSlotType.Weapon, "Slot_Weapon", new Vector2(-65f, 0f), "주무기", "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Dagger.Png", new Vector2(0.5f, 0.5f)),
                    (EquipmentSlotType.Shield, "Slot_Shield", new Vector2(65f, 0f),  "보조무기", "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Shield.Png", new Vector2(0.5f, 0.5f))
                };

                foreach (var def in wepDefs)
                {
                    allSlotViews.Add(CreateSlot(def, wepGroupGO.transform, frameSprite, circleSprite, fontAsset, true));
                }

                // Reflection 필드 바인딩
                var t = typeof(EquipmentPaperdollUI);
                t.GetField("_armorSlotsContainer", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(paperdollComp, armorGroupGO);
                t.GetField("_accessorySlotsContainer", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(paperdollComp, accGroupGO);
                t.GetField("_weaponSlotsContainer", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(paperdollComp, wepGroupGO);
                t.GetField("_tabArmorButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(paperdollComp, btnArmor);
                t.GetField("_tabAccessoryButton", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(paperdollComp, btnAcc);
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

        private static GameObject CreateTabButton(
            string name, 
            Transform parent, 
            Vector2 pos, 
            string text, 
            Sprite circleSprite, 
            TMP_FontAsset fontAsset, 
            out Button button)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(110f, 30f);

            Image img = go.GetComponent<Image>();
            img.sprite = circleSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.12f, 0.18f, 0.28f, 0.85f);

            button = go.GetComponent<Button>();
            button.targetGraphic = img;

            GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.text = text;
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.85f, 0.92f, 1f, 0.9f);
            tmp.raycastTarget = false;

            return go;
        }

        private static EquipmentPaperdollUI.PaperdollSlotView CreateSlot(
            (EquipmentSlotType slotType, string name, Vector2 pos, string label, string iconPath, Vector2 anchor) def,
            Transform parent,
            Sprite frameSprite,
            Sprite circleSprite,
            TMP_FontAsset fontAsset,
            bool isGoldWeapon)
        {
            GameObject slotGO = new GameObject(def.name, typeof(RectTransform), typeof(Image), typeof(Button));
            slotGO.transform.SetParent(parent, false);

            RectTransform slotRT = slotGO.GetComponent<RectTransform>();
            slotRT.anchorMin = def.anchor;
            slotRT.anchorMax = def.anchor;
            slotRT.pivot = new Vector2(0.5f, 0.5f);
            slotRT.anchoredPosition = def.pos;
            slotRT.sizeDelta = isGoldWeapon ? new Vector2(74f, 74f) : new Vector2(68f, 68f);

            Image bgImg = slotGO.GetComponent<Image>();
            bgImg.sprite = frameSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = isGoldWeapon ? new Color(1f, 0.95f, 0.85f, 1f) : new Color(0.9f, 0.95f, 1f, 0.9f);

            Button btn = slotGO.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.7f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = colors;

            Sprite placeholderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(def.iconPath);

            // Placeholder
            GameObject placeholderGO = new GameObject("PlaceholderIcon", typeof(RectTransform), typeof(Image));
            placeholderGO.transform.SetParent(slotGO.transform, false);
            RectTransform phRT = placeholderGO.GetComponent<RectTransform>();
            phRT.anchoredPosition = new Vector2(0f, 4f);
            phRT.sizeDelta = new Vector2(40f, 40f);
            Image phImg = placeholderGO.GetComponent<Image>();
            phImg.sprite = placeholderSprite;
            phImg.preserveAspect = true;
            phImg.color = isGoldWeapon ? new Color(1f, 0.88f, 0.45f, 0.65f) : new Color(1f, 1f, 1f, 0.55f);
            phImg.raycastTarget = false;

            // IconImage
            GameObject iconGO = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(slotGO.transform, false);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchoredPosition = new Vector2(0f, 4f);
            iconRT.sizeDelta = new Vector2(48f, 48f);
            Image iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = placeholderSprite;
            iconImg.preserveAspect = true;
            iconImg.color = new Color(1f, 1f, 1f, 0f);
            iconImg.raycastTarget = false;

            // Label
            GameObject labelGO = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(slotGO.transform, false);
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.5f, 0f);
            labelRT.anchorMax = new Vector2(0.5f, 0f);
            labelRT.pivot = new Vector2(0.5f, 0f);
            labelRT.anchoredPosition = new Vector2(0f, 4f);
            labelRT.sizeDelta = new Vector2(64f, 15f);
            TextMeshProUGUI labelTMP = labelGO.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) labelTMP.font = fontAsset;
            labelTMP.text = def.label;
            labelTMP.fontSize = 11;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.color = isGoldWeapon ? new Color(1f, 0.88f, 0.4f, 0.95f) : new Color(0.85f, 0.92f, 1f, 0.85f);
            labelTMP.raycastTarget = false;

            // SelectedBorder
            GameObject borderGO = new GameObject("SelectedBorder", typeof(RectTransform), typeof(Image));
            borderGO.transform.SetParent(slotGO.transform, false);
            RectTransform borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-3f, -3f);
            borderRT.offsetMax = new Vector2(3f, 3f);
            Image borderImg = borderGO.GetComponent<Image>();
            borderImg.sprite = circleSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = isGoldWeapon ? new Color(1f, 0.8f, 0.2f, 0.9f) : new Color(0.2f, 0.8f, 1f, 0.85f);
            borderImg.raycastTarget = false;
            borderGO.SetActive(false);

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
