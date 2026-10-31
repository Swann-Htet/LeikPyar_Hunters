using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Setup Sky
// Makes two sky materials from your pictures and puts a SkyCycle in the open scene:
//   Night   : deep-space-starry-environment_1K_...exr
//   Morning : syferfontein_6d_clear_puresky_1k.exr
// The sky changes with the game clock (night hours = the NightFog hours).
public static class SkySetupTool
{
    const string Folder = "Assets/Skyboxes";

    [MenuItem("Tools/Ghost/Setup Sky")]
    public static void Setup()
    {
        var nightTexture = FindTexture("deep-space-starry-environment");
        var morningTexture = FindTexture("syferfontein_6d_clear_puresky");

        if (nightTexture == null || morningTexture == null)
        {
            EditorUtility.DisplayDialog("Setup Sky",
                "Could not find the pictures:\n" +
                (nightTexture == null ? "  - deep-space-starry-environment...exr\n" : "") +
                (morningTexture == null ? "  - syferfontein_6d_clear_puresky_1k.exr\n" : "") +
                "Put them in the Assets folder and run this again.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Skyboxes");

        var night = MakeSky("Sky_Night", nightTexture, 1f);
        var morning = MakeSky("Sky_Morning", morningTexture, 1f);

        var cycle = Object.FindFirstObjectByType<SkyCycle>();
        if (cycle == null)
        {
            var go = new GameObject("Sky");
            Undo.RegisterCreatedObjectUndo(go, "Sky");
            cycle = go.AddComponent<SkyCycle>();
        }

        Undo.RecordObject(cycle, "Setup sky");
        cycle.nightSky = night;
        cycle.morningSky = morning;
        EditorUtility.SetDirty(cycle);

        // what you see in the editor: the sky of the moment (the game clock decides while it runs)
        RenderSettings.skybox = night;
        cycle.ApplyAmbient();   // room light from plain colours, not from the (almost black) night sky
        DynamicGI.UpdateEnvironment();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = cycle.gameObject;

        Debug.Log("SkySetupTool: Sky_Night and Sky_Morning are in " + Folder + ". The sky now changes with the game clock. Save the scene.");
    }

    static Texture FindTexture(string namePart)
    {
        foreach (var guid in AssetDatabase.FindAssets(namePart))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".exr") && !path.EndsWith(".hdr")) continue;

            var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture != null) return texture;
        }
        return null;
    }

    static Material MakeSky(string name, Texture texture, float exposure)
    {
        string path = Folder + "/" + name + ".mat";
        var shader = Shader.Find("Skybox/Panoramic");

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_MainTex", texture);
        material.SetFloat("_Mapping", 1f);       // Latitude Longitude Layout (a normal 360 degree picture)
        material.SetFloat("_ImageType", 0f);     // 360 degrees
        material.SetFloat("_Exposure", exposure);
        material.SetFloat("_Rotation", 0f);
        material.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        material.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
