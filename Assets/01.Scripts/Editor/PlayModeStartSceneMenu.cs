using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlayModeStartSceneMenu
{
    private const string ScenePath = "Assets/00.Scenes/OpenWorldRobby.unity";

    [MenuItem("Tools/Play Mode Start Scene/Lock to OpenWorldRobby")]
    private static void Lock()
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
    }

    [MenuItem("Tools/Play Mode Start Scene/Unlock (Use Current Scene)")]
    private static void Unlock()
    {
        EditorSceneManager.playModeStartScene = null;
    }
}