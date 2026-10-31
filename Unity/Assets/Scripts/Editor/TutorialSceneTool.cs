using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Create Tutorial Scene
// Makes the scene "TutorialScene" (the training) as a copy of SampleScene and puts it in the Build Settings, after SampleScene.
// The training itself is made by TutorialMode when the scene starts: nothing else needs to be set up in it.
// Run it again after you changed the house in SampleScene, to get the same house in the training (the old copy is replaced).
// The TUTORIAL button of the menu and the START TRAINING button of the guide open this scene
// (without it they open SampleScene in training mode, which works too).
public static class TutorialSceneTool
{
    const string GamePath = "Assets/Scenes/SampleScene.unity";
    const string TutorialPath = "Assets/Scenes/TutorialScene.unity";

    [MenuItem("Tools/Ghost/Create Tutorial Scene")]
    public static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GamePath) == null)
        {
            EditorUtility.DisplayDialog("Tutorial scene", "Could not find " + GamePath + ".", "OK");
            return;
        }

        // the copy is made from the file: unsaved changes of the open scene must be saved first
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialPath) != null)
        {
            if (!EditorUtility.DisplayDialog("Tutorial scene", "TutorialScene already exists. Replace it with a new copy of SampleScene?", "Replace", "Cancel")) return;
            AssetDatabase.DeleteAsset(TutorialPath);
        }

        if (!AssetDatabase.CopyAsset(GamePath, TutorialPath))
        {
            EditorUtility.DisplayDialog("Tutorial scene", "The copy failed. Look at the Console.", "OK");
            return;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // the Build Settings: after SampleScene
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool inBuild = false;
        foreach (var s in scenes)
            if (s.path == TutorialPath) { s.enabled = true; inBuild = true; }
        if (!inBuild) scenes.Add(new EditorBuildSettingsScene(TutorialPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log("TutorialSceneTool: " + TutorialPath + " was made and is in the Build Settings. Press Play on it to try the training. " +
                  "Run this tool again after you change the house in SampleScene.");
        EditorUtility.DisplayDialog("Tutorial scene",
            "Done: Assets/Scenes/TutorialScene.unity is a copy of SampleScene and is in the Build Settings.\n\n" +
            "Open it and press Play to try the training. The TUTORIAL button of the menu opens it.", "OK");
    }
}
