using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NightFogSetupTool
{
    [MenuItem("Tools/Ghost/Setup Night Fog, Torch And Power Outages")]
    public static void Setup()
    {
        // 1) The night object: fog + dark ambient, and the power outages
        var fog = Object.FindFirstObjectByType<NightFog>();
        GameObject nightObject;
        if (fog == null)
        {
            nightObject = new GameObject("NightFog");
            Undo.RegisterCreatedObjectUndo(nightObject, "Night");
            fog = nightObject.AddComponent<NightFog>();
        }
        else
        {
            nightObject = fog.gameObject;
        }

        if (nightObject.GetComponent<PowerOutage>() == null)
            Undo.AddComponent<PowerOutage>(nightObject);

        // 2) The torch (T) on the player
        string torchResult = "no player found";
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            if (player.GetComponent<Torch>() == null)
                Undo.AddComponent<Torch>(player.gameObject);
            torchResult = "Torch is on '" + player.name + "'";
        }

        // 3) Fog must be ON in the scene's lighting, or the WebGL build removes the fog shader.
        // The density is tiny here, so in the editor you see no fog until the game runs.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fog.dayDensity;
        RenderSettings.fogColor = fog.dayFogColor;

        EditorUtility.SetDirty(fog);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = nightObject;

        Debug.Log("NightFogSetupTool: 'NightFog' (fog + PowerOutage) is in the scene, " + torchResult + ". Save the scene. " +
                  "In Play mode: T = torch. Tick 'Force Night' (NightFog) or 'Start Outage Now' (PowerOutage) to test.");
    }
}
