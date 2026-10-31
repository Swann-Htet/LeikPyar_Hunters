using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Use Morning And Night Skies
// The sky of the day is Assets/Skybox/SKY.mat, the sky of the night is Assets/Skybox/NIGHT NEW.mat (the picture "je_gray_03_1k (1)").
// Both go into the Sky Cycle of the open scene, which switches them by the game clock.
public static class SkiesTool
{
    const string Morning = "Assets/Skybox/SKY.mat";
    const string Night = "Assets/Skybox/NIGHT NEW.mat";

    [MenuItem("Tools/Ghost/Use Morning And Night Skies")]
    public static void Run()
    {
        var cycle = Object.FindFirstObjectByType<SkyCycle>();
        if (cycle == null)
        {
            EditorUtility.DisplayDialog("Skies", "No Sky Cycle in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        var morning = AssetDatabase.LoadAssetAtPath<Material>(Morning);
        var night = AssetDatabase.LoadAssetAtPath<Material>(Night);
        if (morning == null || night == null)
        {
            EditorUtility.DisplayDialog("Skies", "Missing: " + (morning == null ? Morning + "  " : "") + (night == null ? Night : ""), "OK");
            return;
        }

        Undo.RecordObject(cycle, "Skies");
        cycle.morningSky = morning;
        cycle.nightSky = night;
        EditorUtility.SetDirty(cycle);
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);

        Debug.Log("[Sky] Morning = " + Morning + ", Night = " + Night + ". Save the scene (Ctrl+S).");
        EditorUtility.DisplayDialog("Skies", "Morning: SKY\nNight: NIGHT NEW (je_gray_03_1k (1))\n\nSave the scene (Ctrl+S). Run Create Tutorial Scene again to copy it there.", "OK");
    }
}
