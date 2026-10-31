using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Use Magical Orb For The Orbs
// Puts ASSET/Orb/magical_orb.glb on the GhostSpawner ("Orb Prefab"): every Orb (baby ghost) then shows the magical orb
// instead of its old model while the game runs. The .glb needs the glTFast package (it is in the project's package list).
public static class OrbSetupTool
{
    const string Path = "Assets/ASSET/Orb/magical_orb.glb";

    [MenuItem("Tools/Ghost/Use Magical Orb For The Orbs")]
    public static void Run()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Magical orb", "No GhostSpawner in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Magical orb",
                "Unity cannot use " + Path + " yet.\n\n" +
                "The .glb files need the glTFast package. Wait until Unity has finished installing it (the spinner at the bottom right), " +
                "then right-click the file > Reimport, and run this again.", "OK");
            return;
        }

        Undo.RecordObject(spawner, "Magical orb");
        spawner.orbPrefab = model;
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("OrbSetupTool: the Orbs use " + Path + " (GhostSpawner > Orb Prefab). Orb Width " + spawner.orbWidth +
                  " m, Orb Lift " + spawner.orbLift + " m. Save the scene.");
        EditorUtility.DisplayDialog("Magical orb", "Done. The Orbs now use the magical orb. Save the scene.", "OK");
    }
}
