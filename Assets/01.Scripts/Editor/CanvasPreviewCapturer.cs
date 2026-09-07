#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class CanvasPreviewCapturer
{
    public static string Capture()
    {
        string prefabPath = "Assets/03.Prefab/UI/Canvas/NPCDialogueCanvas.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return "Prefab not found";

        var instance = Object.Instantiate(prefab);
        instance.name = "Temp_NPCDialogueCanvas_Preview";

        // Active DialoguePanel
        var panel = instance.transform.Find("DialoguePanel");
        if (panel != null) panel.gameObject.SetActive(true);

        // Create temporary Camera
        var camGo = new GameObject("Temp_PreviewCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f); // Dark studio background

        var canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 10f;

        int width = 1280;
        int height = 720;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // Force UI update
        Canvas.ForceUpdateCanvases();

        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;

        byte[] bytes = tex.EncodeToPNG();
        string outPath = "C:/Users/BaeSoohyun/.gemini/antigravity/brain/9a36c6ce-6665-4e26-9b4e-147f389d3754/npc_dialogue_styled_preview.png";
        File.WriteAllBytes(outPath, bytes);

        // Cleanup
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(instance);

        return "Captured: " + outPath;
    }
}
#endif
