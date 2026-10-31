using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// After "Unlock Materials", prefabs and scene objects that pointed at a model's OLD embedded
// material show pink (missing material). This tool gives every empty material slot the material
// the model itself now uses (found through the mesh), so nothing is pink any more.
public static class MaterialRepairTool
{
    [MenuItem("Tools/Ghost/Repair Missing (pink) Materials - prefabs and scene")]
    static void Repair()
    {
        var lookup = new Dictionary<Mesh, Material[]>();
        var loadedModels = new HashSet<string>();
        int slots = 0;
        int prefabsFixed = 0;

        // Models whose materials were extracted
        var unlocked = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && importer.GetExternalObjectMap().Count > 0)
                unlocked.Add(path);
        }

        // 1) Prefab assets that use those models (fixed first, so scene instances follow)
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            bool usesModel = false;
            foreach (var dependency in AssetDatabase.GetDependencies(path, false))
            {
                if (unlocked.Contains(dependency)) { usesModel = true; break; }
            }
            if (!usesModel) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int fixedHere = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                fixedHere += RepairRenderer(renderer, lookup, loadedModels, false);

            if (fixedHere > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabsFixed++;
                slots += fixedHere;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2) Whatever is still empty in the open scene
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            slots += RepairRenderer(renderer, lookup, loadedModels, true);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("MaterialRepairTool: " + slots + " empty material slot(s) repaired (" + prefabsFixed + " prefab(s) changed). Save the scene.");
    }

    static int RepairRenderer(Renderer renderer, Dictionary<Mesh, Material[]> lookup, HashSet<string> loadedModels, bool recordUndo)
    {
        Material[] materials = renderer.sharedMaterials;

        bool anyEmpty = false;
        foreach (var m in materials)
        {
            if (m == null) { anyEmpty = true; break; }
        }
        if (!anyEmpty) return 0;

        Mesh mesh = GetMesh(renderer);
        if (mesh == null) return 0;

        string modelPath = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(modelPath) || !(AssetImporter.GetAtPath(modelPath) is ModelImporter)) return 0;

        LoadModel(modelPath, lookup, loadedModels);

        Material[] source;
        if (!lookup.TryGetValue(mesh, out source)) return 0;

        int count = 0;
        for (int i = 0; i < materials.Length && i < source.Length; i++)
        {
            if (materials[i] == null && source[i] != null)
            {
                materials[i] = source[i];
                count++;
            }
        }

        if (count > 0)
        {
            if (recordUndo) Undo.RecordObject(renderer, "Repair materials");
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
        return count;
    }

    // Remembers which materials each mesh of the model uses
    static void LoadModel(string modelPath, Dictionary<Mesh, Material[]> lookup, HashSet<string> loadedModels)
    {
        if (!loadedModels.Add(modelPath)) return;

        var root = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (root == null) return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh != null && !lookup.ContainsKey(mesh))
                lookup[mesh] = renderer.sharedMaterials;
        }
    }

    static Mesh GetMesh(Renderer renderer)
    {
        var skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;

        var filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }
}
