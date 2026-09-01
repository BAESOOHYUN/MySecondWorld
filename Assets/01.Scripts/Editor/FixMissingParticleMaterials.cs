using UnityEditor;
using UnityEngine;
using System.Text;

public static class FixMissingParticleMaterials
{
    [MenuItem("Tools/Fix Missing Particle Materials In PolygonParticleFX")]
    public static void Fix()
    {
        var spritesDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Models/PolygonParticleFX" });
        int fixedSlots = 0;
        int prefabsChanged = 0;
        var log = new StringBuilder();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            bool prefabChanged = false;

            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool rendererChanged = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        mats[i] = spritesDefault;
                        rendererChanged = true;
                        fixedSlots++;
                    }
                }
                if (rendererChanged)
                {
                    r.sharedMaterials = mats;
                    prefabChanged = true;
                }
            }

            if (prefabChanged)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabsChanged++;
                log.AppendLine(path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixMissingParticleMaterials] fixedSlots={fixedSlots}, prefabsChanged={prefabsChanged}\n{log}");
    }
}