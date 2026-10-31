using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Add Lightning VFX To MEP Glass
// Puts the electric arcs (MepLightning) in the glass tube of the MEP. The glass is found by its material name ("glass").
// The old Thunder_FX copies under the MEP (they did not fit the tube) are taken away.
public static class MepThunderTool
{
    [MenuItem("Tools/Ghost/Add Lightning VFX To MEP Glass")]
    public static void Run()
    {
        GameObject mep = null;
        foreach (var top in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (top.name == "MEP") { mep = top; break; }

        if (mep == null)
        {
            EditorUtility.DisplayDialog("Lightning in the MEP", "No object called 'MEP' at the top of the Hierarchy.", "OK");
            return;
        }

        // the glass tube: the biggest part of the MEP whose material has "glass" in its name
        Renderer glass = null;
        float biggest = -1f;
        foreach (var r in mep.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;

            bool isGlass = false;
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.ToLowerInvariant().Contains("glass")) { isGlass = true; break; }
            if (!isGlass) continue;

            float volume = r.bounds.size.x * r.bounds.size.y * r.bounds.size.z;
            if (volume > biggest) { biggest = volume; glass = r; }
        }

        if (glass == null)
        {
            EditorUtility.DisplayDialog("Lightning in the MEP", "No part of the MEP has a material with 'glass' in its name.\nOpen the MEP and check its materials.", "OK");
            return;
        }

        // the old effect copies go
        int removed = 0;
        foreach (var child in mep.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == mep.transform) continue;
            if (child.name == "MEP Thunder" || child.name == "Thunder_FX" || child.name == "MEP Lightning")
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                removed++;
            }
        }

        var lightning = glass.GetComponent<MepLightning>();
        if (lightning == null) lightning = Undo.AddComponent<MepLightning>(glass.gameObject);
        lightning.glass = glass;
        EditorUtility.SetDirty(lightning);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = glass.gameObject;
        Debug.Log("MepThunderTool: MepLightning is on '" + glass.name + "' (the glass of the MEP), " + removed + " old effect(s) removed. " +
                  "It shows in play mode while the MEP is out. Save the scene.");
        EditorUtility.DisplayDialog("Lightning in the MEP", "Done: the lightning is in the glass '" + glass.name + "'.\nPress Play and take the MEP out to see it. Save the scene.", "OK");
    }
}
