using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Materials that come inside an FBX (like 'asset_MAT') are locked: their fields are grey.
// This tool extracts them into normal .mat files (in a "Materials" folder next to the model),
// so Surface Type, colors, textures and transparency can be edited.
public static class MaterialUnlockTool
{
    [MenuItem("Tools/Ghost/Unlock Materials - all models used in this scene")]
    static void UnlockScene()
    {
        var models = new HashSet<string>();

        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (var material in renderer.sharedMaterials)
                AddIfEmbedded(material, models);
        }

        Run(models);
    }

    [MenuItem("Tools/Ghost/Unlock Materials - selected models or folders")]
    static void UnlockSelected()
    {
        var models = new HashSet<string>();

        foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { path }))
                    AddModel(AssetDatabase.GUIDToAssetPath(guid), models);
            }
            else
            {
                AddModel(path, models);
            }
        }

        Run(models);
    }

    static void AddIfEmbedded(Material material, HashSet<string> models)
    {
        if (material == null) return;

        string path = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsSubAsset(material)) return;

        AddModel(path, models);
    }

    static void AddModel(string path, HashSet<string> models)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) return;
        if (AssetImporter.GetAtPath(path) is ModelImporter)
            models.Add(path);
    }

    static void Run(HashSet<string> models)
    {
        if (models.Count == 0)
        {
            EditorUtility.DisplayDialog("Unlock materials", "No locked (embedded) materials found.", "OK");
            return;
        }

        var list = new StringBuilder();
        int shown = 0;
        foreach (var path in models)
        {
            if (shown++ >= 12) { list.AppendLine("..."); break; }
            list.AppendLine("- " + path);
        }

        bool go = EditorUtility.DisplayDialog(
            "Unlock materials",
            models.Count + " model file(s) will get their materials extracted into a 'Materials' folder next to them:\n\n" +
            list + "\nSave your scene first. Continue?",
            "Unlock", "Cancel");
        if (!go) return;

        int total = 0;
        foreach (var path in models)
            total += ExtractMaterials(path);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MaterialUnlockTool: " + total + " material(s) extracted from " + models.Count + " model(s). They are now normal, editable .mat files.");
    }

    static int ExtractMaterials(string modelPath)
    {
        string modelFolder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string folder = modelFolder + "/Materials";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(modelFolder, "Materials");

        int count = 0;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            var material = asset as Material;
            if (material == null) continue;

            string file = SafeName(material.name) + ".mat";
            string target = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + file);

            string error = AssetDatabase.ExtractAsset(material, target);
            if (string.IsNullOrEmpty(error))
                count++;
            else
                Debug.LogWarning("MaterialUnlockTool: could not extract '" + material.name + "' from " + modelPath + ": " + error);
        }

        AssetDatabase.WriteImportSettingsIfDirty(modelPath);
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        return count;
    }

    static string SafeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
