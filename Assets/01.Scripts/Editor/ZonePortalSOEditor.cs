using UnityEditor;
using UnityEngine;
using MySecondWorld.Network;
using MySecondWorld.EditorTools;

[CustomEditor(typeof(ZonePortalSO))]
public class ZonePortalSOEditor : Editor
{
    private void OnEnable() => SceneView.duringSceneGui += OnSceneViewGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneViewGUI;

    private void OnSceneViewGUI(SceneView sceneView)
    {
        var portal = target as ZonePortalSO;
        if (portal == null) return;

        BoundsGizmoDrawer.Draw(portal.volume, portal.name, Color.magenta);
    }
}