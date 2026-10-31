using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

public static class GhostSetupTool
{
    private const string BeamMaterialPath = "Assets/Prefab/Gun/textures/ElectricBeam.mat";

    [MenuItem("Tools/Ghost/Setup Baby Tag And Generator")]
    public static void Setup()
    {
        // 1) The "bghost" tag
        if (Array.IndexOf(InternalEditorUtility.tags, BabyGhostFinder.Tag) < 0)
            InternalEditorUtility.AddTag(BabyGhostFinder.Tag);
        AddTypeTags();

        // 2) Tag every baby ghost (objects named "Bby Ghost..." / "Baby Ghost..." that have a model)
        int tagged = 0;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(t.name.StartsWith("Bby Ghost") || t.name.StartsWith("Baby Ghost"))) continue;
            if (t.GetComponent<BabyGhostSpawner>() != null) continue;
            if (t.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
            if (t.parent != null && t.parent.name.StartsWith("Bby Ghost")) continue;
            if (t.gameObject.tag == BabyGhostFinder.Tag) continue;

            Undo.RecordObject(t.gameObject, "Tag baby ghost");
            t.gameObject.tag = BabyGhostFinder.Tag;
            tagged++;
        }

        // 3) The generator component
        string generatorResult = "generator 'sci-fi_generator' not found";
        var generator = GameObject.Find("sci-fi_generator");
        if (generator != null)
        {
            if (generator.GetComponent<BabyGhostGenerator>() == null)
                Undo.AddComponent<BabyGhostGenerator>(generator);
            generatorResult = "BabyGhostGenerator is on 'sci-fi_generator'";
        }

        // 4) The electric beam material, assigned to the gun
        string beamResult = "no GhostGun found";
        var gun = UnityEngine.Object.FindFirstObjectByType<GhostGun>();
        if (gun != null)
        {
            Material beam = GetOrCreateBeamMaterial();
            if (beam != null && gun.beamMaterial == null)
            {
                Undo.RecordObject(gun, "Assign beam material");
                gun.beamMaterial = beam;
                EditorUtility.SetDirty(gun);
            }
            beamResult = beam != null ? "beam material assigned" : "beam shader not found";
        }

        // 5) The ghost scanner on the player (its lights are set up by Tools > Ghost > Setup Scanner)
        string deviceResult = "no player found";
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var device = player.GetComponent<GhostDevice>();
            if (device == null) device = Undo.AddComponent<GhostDevice>(player.gameObject);
            EditorUtility.SetDirty(device);
            deviceResult = "GhostDevice is on the player (run Tools > Ghost > Setup Scanner for its lights)";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"GhostSetupTool: tag '{BabyGhostFinder.Tag}' ready, {tagged} baby ghost(s) tagged, {generatorResult}, {beamResult}, {deviceResult}.");
    }

    [MenuItem("Tools/Ghost/Add Ghost Type Tags (Orb, Mirage, Solid)")]
    public static void AddTypeTags()
    {
        foreach (var tag in BabyGhostFinder.TypeTags)
        {
            if (Array.IndexOf(InternalEditorUtility.tags, tag) < 0)
                InternalEditorUtility.AddTag(tag);
        }
        Debug.Log("GhostSetupTool: tags Orb, Mirage and Solid are ready. Set one on each ghost (Inspector > Tag).");
    }

    // Gives the gun the Cartoon FX electric particles as its pull effect (the lightning line is then hidden)
    [MenuItem("Tools/Ghost/Use Electric VFX For The Gun Beam")]
    public static void UseElectricVfxForBeam()
    {
        var gun = UnityEngine.Object.FindFirstObjectByType<GhostGun>();
        if (gun == null)
        {
            Debug.LogWarning("GhostSetupTool: no GhostGun in the scene.");
            return;
        }

        Undo.RecordObject(gun, "Electric gun VFX");
        gun.beamVfx = LoadPrefab("CFXR Electrified 3");
        if (gun.hookVfx == null) gun.hookVfx = LoadPrefab("CFXR3 Hit Electric C (Air)");
        if (gun.overheatVfx == null) gun.overheatVfx = LoadPrefab("CFXR2 Sparks Rain");
        gun.showLineBeam = false;
        EditorUtility.SetDirty(gun);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("GhostSetupTool: pull effect = " + (gun.beamVfx != null ? gun.beamVfx.name : "NOT FOUND") +
                  ". The lightning line is hidden. Change any VFX in the GhostGun Inspector > Gun VFX.");
    }

    static GameObject LoadPrefab(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    // A transparent copy of the capsule's material, assigned to the generator. It makes sure the
    // transparent shader is included in the WebGL build, so the capsule can fade in there too.
    [MenuItem("Tools/Ghost/Create Capsule Fade Material")]
    public static void CreateCapsuleFadeMaterial()
    {
        int made = 0;
        foreach (var generator in UnityEngine.Object.FindObjectsByType<BabyGhostGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (generator.capsulePrefab == null) continue;

            var renderer = generator.capsulePrefab.GetComponentInChildren<Renderer>(true);
            if (renderer == null || renderer.sharedMaterial == null) continue;

            Material source = renderer.sharedMaterial;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string folder = string.IsNullOrEmpty(sourcePath) ? "Assets" : System.IO.Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + source.name + "_Fade.mat");

            var copy = new Material(source);
            BabyGhostGenerator.MakeTransparent(copy);
            AssetDatabase.CreateAsset(copy, path);

            Undo.RecordObject(generator, "Assign capsule fade material");
            generator.fadeMaterial = copy;
            EditorUtility.SetDirty(generator);
            made++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("GhostSetupTool: " + made + " capsule fade material(s) created and assigned to the generator(s).");
    }

    static Material GetOrCreateBeamMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        material = new Material(shader);
        material.color = new Color(0.4f, 0.85f, 1f, 1f);
        AssetDatabase.CreateAsset(material, BeamMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
