#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class AutoRefresh : MonoBehaviour
{
    [MenuItem("Tools/Auto Refresh")]
    static void AutoRefreshToggle()
    {
        var status = EditorPrefs.GetInt("kAutoRefresh");
        if (status == 1)
            EditorPrefs.SetInt("kAutoRefresh", 0);
        else
            EditorPrefs.SetInt("kAutoRefresh", 1);
    }

    [MenuItem("Tools/Auto Refresh", true)]
    static bool AutoRefreshToggleValidation()
    {
        var status = EditorPrefs.GetInt("kAutoRefresh");
        if (status == 1)
            Menu.SetChecked("Tools/Auto Refresh", true);
        else
            Menu.SetChecked("Tools/Auto Refresh", false);
        return true;
    }
}
