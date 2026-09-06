#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MySecondWorld.UI;

namespace MySecondWorld.EditorTools
{
    public static class UIResponsiveBackdropSetup
    {
        private const string InventoryPrefabPath = "Assets/03.Prefab/UI/Canvas/Inventory Canvas.prefab";
        private const string ShopPrefabPath = "Assets/03.Prefab/UI/Canvas/ShopCanvas.prefab";
        private const string NPCDialoguePrefabPath = "Assets/03.Prefab/UI/Canvas/NPCDialogueCanvas.prefab";
        private const string MenuPrefabPath = "Assets/03.Prefab/UI/Canvas/MenuCanvas.prefab";
        private const string ProjectScopePrefabPath = "Assets/03.Prefab/LifetimeScope/ProjectLifetimeScope.prefab";
        private const string LayerLabDarkPopupSprite = "Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Popup/Popup_Demo_Dark/Popup00_Frame1.png";

        [MenuItem("Tools/MySecondWorld/Setup All UI Backdrops")]
        public static void SetupAllBackdrops()
        {
            Debug.Log("[UIBackdropSetup] === 전역 UI Backdrop 설정 시작 ===");

            SetupInventoryCanvas();
            SetupShopCanvas();
            SetupNPCDialogueCanvas();
            SetupMenuCanvas();
            SetupProjectLifetimeScopeNPCMenu();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UIBackdropSetup] === 전역 UI Backdrop 설정 완료 및 저장됨 ===");
        }

        [MenuItem("Tools/MySecondWorld/Setup Inventory Canvas Only")]
        public static void SetupInventoryCanvas()
        {
            PolishInventoryUI();
        }

        [MenuItem("Tools/MySecondWorld/Polish Inventory UI (Frames, Icons, Buttons)")]
        public static void PolishInventoryUI()
        {
            // 1. Inventory Canvas Prefab 세팅
            using (var scope = new PrefabEditScope(InventoryPrefabPath))
            {
                if (scope.Root != null)
                {
                    var inventoryUI = scope.Root.GetComponent<InventoryUI>();

                    // Inventory Panel 찾기
                    var panelTrans = scope.Root.transform.Find("Inventory Panel") ?? scope.Root.transform;

                    // Backdrop 생성 또는 획득
                    var backdropBtn = GetOrCreateBackdrop(panelTrans, "Backdrop", new Color(0f, 0f, 0f, 0.35f), 0);

                    // InventoryUI._backdropButton 연결
                    if (inventoryUI != null)
                    {
                        var so = new SerializedObject(inventoryUI);
                        var prop = so.FindProperty("_backdropButton");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = backdropBtn;
                            so.ApplyModifiedProperties();
                        }
                    }

                    // Inventory Items 메인 창 윈도우 스타일링
                    var itemsTrans = panelTrans.Find("Inventory Items") as RectTransform;
                    if (itemsTrans != null)
                    {
                        itemsTrans.anchorMin = new Vector2(0.5f, 0.5f);
                        itemsTrans.anchorMax = new Vector2(0.5f, 0.5f);
                        itemsTrans.pivot = new Vector2(0.5f, 0.5f);
                        itemsTrans.anchoredPosition = new Vector2(350f, 0f);
                        itemsTrans.sizeDelta = new Vector2(460f, 620f);

                        if (itemsTrans.TryGetComponent<Image>(out var itemsImg))
                        {
                            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerLabDarkPopupSprite);
                            if (frameSprite != null)
                            {
                                itemsImg.sprite = frameSprite;
                                itemsImg.type = Image.Type.Sliced;
                                itemsImg.color = Color.white;
                            }
                        }

                        // Header 영역 정돈
                        var headerTrans = itemsTrans.Find("Header") as RectTransform;
                        if (headerTrans != null)
                        {
                            headerTrans.anchorMin = new Vector2(0f, 1f);
                            headerTrans.anchorMax = new Vector2(1f, 1f);
                            headerTrans.pivot = new Vector2(0.5f, 1f);
                            headerTrans.anchoredPosition = new Vector2(0f, -12f);
                            headerTrans.sizeDelta = new Vector2(-20f, 48f);

                            if (headerTrans.TryGetComponent<Image>(out var headerImg))
                                headerImg.color = Color.clear; // 불투명 시커먼 박스 제거

                            var labelTxt = headerTrans.GetComponentInChildren<TMPro.TMP_Text>(true);
                            if (labelTxt != null)
                            {
                                labelTxt.text = "INVENTORY";
                                labelTxt.fontSize = 22f;
                                labelTxt.fontStyle = TMPro.FontStyles.Bold;
                                labelTxt.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                                labelTxt.alignment = TMPro.TextAlignmentOptions.Center;
                            }

                            // 닫기 버튼 모바일 터치 친화적 38px 스타일링
                            var closeTrans = headerTrans.Find("CloseButton") as RectTransform;
                            if (closeTrans != null)
                            {
                                closeTrans.anchorMin = new Vector2(1f, 0.5f);
                                closeTrans.anchorMax = new Vector2(1f, 0.5f);
                                closeTrans.pivot = new Vector2(1f, 0.5f);
                                closeTrans.anchoredPosition = new Vector2(-15f, 0f);
                                closeTrans.sizeDelta = new Vector2(38f, 38f);

                                if (closeTrans.TryGetComponent<Image>(out var closeImg))
                                {
                                    var btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Common/Btn_IconButton_Circle_90_red.png");
                                    if (btnSprite != null)
                                    {
                                        closeImg.sprite = btnSprite;
                                        closeImg.type = Image.Type.Simple;
                                        closeImg.color = Color.white;
                                    }
                                }

                                // 닫기 아이콘 X 내부 배치
                                var iconCloseChild = closeTrans.Find("Icon_Close") as RectTransform;
                                if (iconCloseChild == null)
                                {
                                    var go = new GameObject("Icon_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                                    iconCloseChild = go.GetComponent<RectTransform>();
                                    iconCloseChild.SetParent(closeTrans, false);
                                }
                                iconCloseChild.anchorMin = new Vector2(0.5f, 0.5f);
                                iconCloseChild.anchorMax = new Vector2(0.5f, 0.5f);
                                iconCloseChild.pivot = new Vector2(0.5f, 0.5f);
                                iconCloseChild.anchoredPosition = Vector2.zero;
                                iconCloseChild.sizeDelta = new Vector2(16f, 16f);
                                var iconCloseImg = iconCloseChild.GetComponent<Image>();
                                iconCloseImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Demo/Demo_Icon/Icon_WhiteIcon_Close.png");
                                iconCloseImg.color = Color.white;
                                iconCloseImg.raycastTarget = false;
                            }
                        }

                        // Inv Viewport 시커먼 박스 제거 및 여백 조정
                        var viewTrans = itemsTrans.Find("Inv Viewport") as RectTransform;
                        if (viewTrans != null)
                        {
                            viewTrans.anchorMin = Vector2.zero;
                            viewTrans.anchorMax = Vector2.one;
                            viewTrans.pivot = new Vector2(0.5f, 0.5f);
                            viewTrans.anchoredPosition = new Vector2(0f, -28f);
                            viewTrans.sizeDelta = new Vector2(-30f, -80f);

                            if (viewTrans.TryGetComponent<Image>(out var viewImg))
                                viewImg.color = Color.clear;

                            var scrollTrans = viewTrans.Find("Scroll View") as RectTransform;
                            if (scrollTrans != null && scrollTrans.TryGetComponent<Image>(out var scrollImg))
                                scrollImg.color = Color.clear;

                            var grid = viewTrans.GetComponentInChildren<GridLayoutGroup>(true);
                            if (grid != null)
                            {
                                grid.cellSize = new Vector2(98f, 98f);
                                grid.spacing = new Vector2(8f, 8f);
                                grid.padding = new RectOffset(8, 8, 8, 8);
                            }
                        }
                    }

                    EditorUtility.SetDirty(scope.Root);
                }
            }

            // 2. InventorySlotButton Prefab 세팅
            using (var slotScope = new PrefabEditScope("Assets/03.Prefab/UI/Buttons/InventorySlotButton.prefab"))
            {
                if (slotScope.Root != null)
                {
                    var rootRt = slotScope.Root.GetComponent<RectTransform>();
                    if (rootRt != null) rootRt.sizeDelta = new Vector2(98f, 98f);

                    // Slot Frame
                    var frameTrans = slotScope.Root.transform.Find("Slot Frame") as RectTransform;
                    if (frameTrans != null)
                    {
                        frameTrans.sizeDelta = new Vector2(98f, 98f);
                        if (frameTrans.TryGetComponent<Image>(out var fImg))
                        {
                            var slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/CardFrame02,03_BackFrame_d.png");
                            if (slotSprite != null)
                            {
                                fImg.sprite = slotSprite;
                                fImg.type = Image.Type.Sliced;
                                fImg.color = Color.white;
                            }
                        }
                    }

                    // Icon (너무 크지도 작지도 않은 56x56 밸런스 크기)
                    var iconTrans = slotScope.Root.transform.Find("icon") as RectTransform;
                    if (iconTrans != null)
                    {
                        iconTrans.anchorMin = new Vector2(0.5f, 0.5f);
                        iconTrans.anchorMax = new Vector2(0.5f, 0.5f);
                        iconTrans.pivot = new Vector2(0.5f, 0.5f);
                        iconTrans.anchoredPosition = new Vector2(0f, 6f);
                        iconTrans.sizeDelta = new Vector2(56f, 56f);

                        if (iconTrans.TryGetComponent<Image>(out var iconImg))
                        {
                            iconImg.preserveAspect = true;
                        }
                    }

                    // index (개수 텍스트)
                    var indexTrans = slotScope.Root.transform.Find("index") as RectTransform;
                    if (indexTrans != null)
                    {
                        indexTrans.anchorMin = new Vector2(0f, 1f);
                        indexTrans.anchorMax = new Vector2(0f, 1f);
                        indexTrans.pivot = new Vector2(0f, 1f);
                        indexTrans.anchoredPosition = new Vector2(6f, -6f);
                        indexTrans.sizeDelta = new Vector2(40f, 20f);
                        if (indexTrans.TryGetComponent<TMPro.TMP_Text>(out var indexTxt))
                        {
                            indexTxt.fontSize = 14f;
                            indexTxt.fontStyle = TMPro.FontStyles.Bold;
                            indexTxt.color = Color.white;
                        }
                    }

                    // discard (버리기 버튼)
                    var discardTrans = slotScope.Root.transform.Find("discard") as RectTransform;
                    if (discardTrans != null)
                    {
                        discardTrans.anchorMin = new Vector2(1f, 1f);
                        discardTrans.anchorMax = new Vector2(1f, 1f);
                        discardTrans.pivot = new Vector2(1f, 1f);
                        discardTrans.anchoredPosition = new Vector2(-6f, -6f);
                        discardTrans.sizeDelta = new Vector2(24f, 24f);

                        if (discardTrans.TryGetComponent<Image>(out var dImg))
                        {
                            var discBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Dark/Btn_IconButton_Circle_90.png");
                            if (discBg != null)
                            {
                                dImg.sprite = discBg;
                                dImg.color = Color.white;
                            }
                        }

                        // 휴지통 아이콘 자식 생성/조정
                        var trashChild = discardTrans.Find("Icon_Trash") as RectTransform;
                        if (trashChild == null)
                        {
                            var go = new GameObject("Icon_Trash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                            trashChild = go.GetComponent<RectTransform>();
                            trashChild.SetParent(discardTrans, false);
                        }
                        trashChild.anchorMin = new Vector2(0.5f, 0.5f);
                        trashChild.anchorMax = new Vector2(0.5f, 0.5f);
                        trashChild.pivot = new Vector2(0.5f, 0.5f);
                        trashChild.anchoredPosition = Vector2.zero;
                        trashChild.sizeDelta = new Vector2(13f, 13f);
                        var trashImg = trashChild.GetComponent<Image>();
                        trashImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/256/PictoIcon_Delete_1.Png");
                        trashImg.color = new Color(0.95f, 0.45f, 0.45f, 1f);
                        trashImg.raycastTarget = false;
                    }

                    // action ([장착] / [사용] 버튼)
                    var actionTrans = slotScope.Root.transform.Find("action") as RectTransform;
                    if (actionTrans != null)
                    {
                        actionTrans.anchorMin = new Vector2(0.5f, 0f);
                        actionTrans.anchorMax = new Vector2(0.5f, 0f);
                        actionTrans.pivot = new Vector2(0.5f, 0f);
                        actionTrans.anchoredPosition = new Vector2(0f, 6f);
                        actionTrans.sizeDelta = new Vector2(68f, 24f);

                        if (actionTrans.TryGetComponent<Image>(out var actImg))
                        {
                            var actSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/External/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Button/Button_Demo_Common/Btn_Rectangle00_n_Blue.png");
                            if (actSprite != null)
                            {
                                actImg.sprite = actSprite;
                                actImg.type = Image.Type.Sliced;
                                actImg.color = Color.white;
                            }
                        }

                        var actTxt = actionTrans.GetComponentInChildren<TMPro.TMP_Text>(true);
                        if (actTxt != null)
                        {
                            actTxt.fontSize = 13f;
                            actTxt.fontStyle = TMPro.FontStyles.Bold;
                            actTxt.color = Color.white;
                            actTxt.alignment = TMPro.TextAlignmentOptions.Center;
                        }
                    }

                    EditorUtility.SetDirty(slotScope.Root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UIBackdropSetup] Inventory Canvas & InventorySlotButton Polish 완료!");
        }

        [MenuItem("Tools/MySecondWorld/Setup Shop Canvas Only")]
        public static void SetupShopCanvas()
        {
            using var scope = new PrefabEditScope(ShopPrefabPath);
            if (scope.Root == null) return;

            var shopUI = scope.Root.GetComponent<ShopUI>();
            if (shopUI == null)
            {
                Debug.LogError($"[UIBackdropSetup] ShopUI를 찾을 수 없음: {ShopPrefabPath}");
                return;
            }

            // ShopCanvas 메인 백드롭 생성
            var mainBackdrop = GetOrCreateBackdrop(scope.Root.transform, "Shop_Backdrop", new Color(0f, 0f, 0f, 0.35f), 0);

            var so = new SerializedObject(shopUI);
            var propBackdrop = so.FindProperty("_backdropButton");
            if (propBackdrop != null)
            {
                propBackdrop.objectReferenceValue = mainBackdrop;
                Debug.Log("[UIBackdropSetup] ShopUI._backdropButton 연결 완료");
            }

            // ConfirmPanel 백드롭 생성
            var propConfirmPanel = so.FindProperty("_confirmPanel");
            if (propConfirmPanel != null && propConfirmPanel.objectReferenceValue is GameObject confirmPanelGo)
            {
                var confirmBackdrop = GetOrCreateBackdrop(confirmPanelGo.transform, "Confirm_Backdrop", new Color(0f, 0f, 0f, 0.45f), 0);
                var propConfirmBackdrop = so.FindProperty("_confirmBackdropButton");
                if (propConfirmBackdrop != null)
                {
                    propConfirmBackdrop.objectReferenceValue = confirmBackdrop;
                    Debug.Log("[UIBackdropSetup] ShopUI._confirmBackdropButton 연결 완료");
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(scope.Root);
        }

        [MenuItem("Tools/MySecondWorld/Setup NPC Dialogue Canvas Only")]
        public static void SetupNPCDialogueCanvas()
        {
            using var scope = new PrefabEditScope(NPCDialoguePrefabPath);
            if (scope.Root == null) return;

            var dialogueUI = scope.Root.GetComponent<NPCDialogueUI>();
            if (dialogueUI == null)
            {
                Debug.LogError($"[UIBackdropSetup] NPCDialogueUI를 찾을 수 없음: {NPCDialoguePrefabPath}");
                return;
            }

            var so = new SerializedObject(dialogueUI);
            var canvasProp = so.FindProperty("_canvas");
            Transform parentTransform = scope.Root.transform;

            if (canvasProp != null && canvasProp.objectReferenceValue is GameObject canvasGo)
            {
                parentTransform = canvasGo.transform;
            }

            var backdropBtn = GetOrCreateBackdrop(parentTransform, "Dialogue_Backdrop", new Color(0f, 0f, 0f, 0.25f), 0);

            var prop = so.FindProperty("_backdropButton");
            if (prop != null)
            {
                prop.objectReferenceValue = backdropBtn;
                so.ApplyModifiedProperties();
                Debug.Log("[UIBackdropSetup] NPCDialogueUI._backdropButton 연결 완료");
            }

            EditorUtility.SetDirty(scope.Root);
        }

        [MenuItem("Tools/MySecondWorld/Setup Menu Canvas Only")]
        public static void SetupMenuCanvas()
        {
            using var scope = new PrefabEditScope(MenuPrefabPath);
            if (scope.Root == null) return;

            var menuUI = scope.Root.GetComponent<MenuUI>();
            if (menuUI == null)
            {
                Debug.LogError($"[UIBackdropSetup] MenuUI를 찾을 수 없음: {MenuPrefabPath}");
                return;
            }

            var so = new SerializedObject(menuUI);
            var settingsPanelProp = so.FindProperty("_settingsPanel");

            if (settingsPanelProp != null && settingsPanelProp.objectReferenceValue is GameObject settingsPanelGo)
            {
                var settingsBackdrop = GetOrCreateBackdrop(settingsPanelGo.transform, "Settings_Backdrop", new Color(0f, 0f, 0f, 0.35f), 0);
                var prop = so.FindProperty("_settingsBackdropButton");
                if (prop != null)
                {
                    prop.objectReferenceValue = settingsBackdrop;
                    so.ApplyModifiedProperties();
                    Debug.Log("[UIBackdropSetup] MenuUI._settingsBackdropButton 연결 완료");
                }
            }

            EditorUtility.SetDirty(scope.Root);
        }

        [MenuItem("Tools/MySecondWorld/Setup ProjectLifetimeScope NPCMenu Only")]
        public static void SetupProjectLifetimeScopeNPCMenu()
        {
            using var scope = new PrefabEditScope(ProjectScopePrefabPath);
            if (scope.Root == null) return;

            var npcMenuUI = scope.Root.GetComponentInChildren<NPCMenuUI>(true);
            if (npcMenuUI == null)
            {
                Debug.LogError($"[UIBackdropSetup] NPCMenuUI를 찾을 수 없음: {ProjectScopePrefabPath}");
                return;
            }

            var so = new SerializedObject(npcMenuUI);
            var canvasProp = so.FindProperty("_canvas");
            Transform parentTransform = npcMenuUI.transform;

            if (canvasProp != null && canvasProp.objectReferenceValue is Canvas canvasComp)
            {
                parentTransform = canvasComp.transform;
            }

            var backdropBtn = GetOrCreateBackdrop(parentTransform, "NPCMenu_Backdrop", new Color(0f, 0f, 0f, 0.35f), 0);

            var prop = so.FindProperty("_backdropButton");
            if (prop != null)
            {
                prop.objectReferenceValue = backdropBtn;
                so.ApplyModifiedProperties();
                Debug.Log("[UIBackdropSetup] NPCMenuUI._backdropButton 연결 완료");
            }

            EditorUtility.SetDirty(scope.Root);
        }

        private static Button GetOrCreateBackdrop(Transform parent, string name, Color dimColor, int siblingIndex)
        {
            var child = parent.Find(name);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = child.gameObject;
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = dimColor;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            go.transform.SetSiblingIndex(siblingIndex);
            return btn;
        }

        private class PrefabEditScope : System.IDisposable
        {
            private readonly string _assetPath;
            public GameObject Root { get; }

            public PrefabEditScope(string assetPath)
            {
                _assetPath = assetPath;
                Root = PrefabUtility.LoadPrefabContents(assetPath);
            }

            public void Dispose()
            {
                if (Root != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(Root, _assetPath);
                    PrefabUtility.UnloadPrefabContents(Root);
                }
            }
        }
    }
}
#endif
