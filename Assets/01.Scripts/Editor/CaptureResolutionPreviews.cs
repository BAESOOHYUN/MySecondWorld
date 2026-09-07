using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace MySecondWorld.EditorTools
{
    public static class CaptureResolutionPreviews
    {
        [MenuItem("Tools/Capture Resolution Previews")]
        public static void CaptureAll()
        {
            // 3-Zone Previews (Shop + Paperdoll + Inventory)
            CaptureResolution(1280, 720, "responsive_16_9_preview.png", true);
            CaptureResolution(1600, 720, "responsive_20_9_preview.png", true);
            CaptureResolution(960, 720, "responsive_4_3_preview.png", true);

            // Inventory + Paperdoll Only Previews
            CaptureResolution(1280, 720, "inv_only_16_9_preview.png", false);
            CaptureResolution(1600, 720, "inv_only_20_9_preview.png", false);
            CaptureResolution(960, 720, "inv_only_4_3_preview.png", false);

            Debug.Log("[CaptureResolutionPreviews] All previews captured successfully!");
        }

        private static void CaptureResolution(int width, int height, string filename, bool includeShop)
        {
            string invPath = "Assets/03.Prefab/UI/Canvas/Inventory Canvas.prefab";
            string shopPath = "Assets/03.Prefab/UI/Canvas/ShopCanvas.prefab";

            GameObject invRoot = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(invPath)) as GameObject;
            GameObject shopRoot = includeShop ? PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(shopPath)) as GameObject : null;

            GameObject camGO = new GameObject("TempUICam", typeof(Camera));
            Camera cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.16f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            try
            {
                // Setup Canvas 1: Inventory
                Canvas invCanvas = invRoot.GetComponent<Canvas>();
                invCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                invCanvas.worldCamera = cam;
                invCanvas.planeDistance = 10f;
                invCanvas.sortingOrder = 10;
                invRoot.SetActive(true);

                var invPanel = invRoot.transform.Find("Inventory Panel");
                if (invPanel != null) invPanel.gameObject.SetActive(true);

                // Setup Canvas 2: Shop (optional)
                if (shopRoot != null)
                {
                    Canvas shopCanvas = shopRoot.GetComponent<Canvas>();
                    shopCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    shopCanvas.worldCamera = cam;
                    shopCanvas.planeDistance = 10f;
                    shopCanvas.sortingOrder = 5;
                    shopRoot.SetActive(true);

                    var shopUI = shopRoot.transform.Find("ShopUI");
                    if (shopUI != null) shopUI.gameObject.SetActive(true);
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
                Debug.Log($"[CaptureResolutionPreviews] Saved: {outPath} ({width}x{height})");
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
                Object.DestroyImmediate(invRoot);
                if (shopRoot != null) Object.DestroyImmediate(shopRoot);
            }
        }
    }
}
