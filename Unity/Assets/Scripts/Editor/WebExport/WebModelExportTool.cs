using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast.Export;
using UnityEditor;
using UnityEngine;

// Tools > Ghost > Export Models For The Website
// Exports the props of the game (EM unit, Faraday cage, MEP, injector, scanner, Phantom Gun) as .glb files for the gallery of the website,
// and adds them to D:\LORDS_WEB\models\models.json (credit: May Thinzar Khin). Models you select in the Project window (prefabs or
// models) are exported too. Then run  python D:\deploy_tools\deploy_web.py  to put them on the website. The files are shown only
// through the protected viewer: they are never served as downloads.
// (The whole house is very big for a web page: export it only if you really want it: select GCC_house in the Project window.)
public static class WebModelExportTool
{
    const string OutputFolder = @"D:\LORDS_WEB\models";
    const string Author = "May Thinzar Khin";

    class Item
    {
        public string path, title, description;
    }

    static readonly Item[] Defaults =
    {
        new Item { path = "Assets/ASSET/EM.prefab", title = "EM Tethering Unit", description = "The generator. Walk an orb into it and it makes a MEP capsule." },
        new Item { path = "Assets/ASSET/MEP.prefab", title = "MEP Capsule", description = "The Molecular Encapsulation Phial: a captured soul, sealed in glass." },
        new Item { path = "Assets/ASSET/Faraday_Cage.prefab", title = "Faraday Cage", description = "The cage that carries a MEP to the Drop Table." },
        new Item { path = "Assets/ASSET/Injector Final.prefab", title = "Injector", description = "Heals you when the night gets close." },
        new Item { path = "Assets/ASSET/scanner.prefab", title = "EMF Scanner", description = "Its lights climb as a ghost gets closer." },
        new Item { path = "Assets/ASSET/protonEmitterGun.prefab", title = "Phantom Gun", description = "Pulls the orbs out of the dark. Watch its heat." },
    };

    [Serializable]
    class Entry
    {
        public string id, title, description, file, author;
        public string[] tags;
        public int year;
    }

    [Serializable]
    class Wrapper
    {
        public Entry[] items;
    }

    [MenuItem("Tools/Ghost/Export Models For The Website")]
    public static async void Run()
    {
        if (!Directory.Exists(OutputFolder))
        {
            EditorUtility.DisplayDialog("Export models", "The folder " + OutputFolder + " is missing.", "OK");
            return;
        }

        var list = new List<Item>(Defaults);
        foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path) || list.Exists(i => i.path == path)) continue;
            list.Add(new Item { path = path, title = go.name.Replace('_', ' '), description = "" });
        }

        var done = new List<string>();
        var failed = new List<string>();
        var entries = LoadEntries();

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            EditorUtility.DisplayProgressBar("Export models", item.title, (float)i / list.Count);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(item.path);
            if (asset == null) { failed.Add(item.path + " (not found)"); continue; }

            string id = Slug(item.title);
            string file = id + ".glb";
            string target = Path.Combine(OutputFolder, file);

            bool ok = false;
            var temp = UnityEngine.Object.Instantiate(asset);
            try
            {
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var settings = new ExportSettings { Format = GltfFormat.Binary, FileConflictResolution = FileConflictResolution.Overwrite };
                var export = new GameObjectExport(settings);
                export.AddScene(new[] { temp }, item.title);
                ok = await export.SaveToFileAndDispose(target);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Export] " + item.title + ": " + e.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }

            if (!ok) { failed.Add(item.title); continue; }

            long size = new FileInfo(target).Length;
            done.Add(item.title + "  " + (size / 1024) + " KB");

            var existing = entries.Find(e => e.id == id);
            if (existing == null)
            {
                entries.Add(new Entry
                {
                    id = id,
                    title = item.title,
                    description = item.description,
                    file = file,
                    tags = new[] { "Prop", "Game ready" },
                    author = Author,
                    year = DateTime.Now.Year,
                });
            }
        }
        EditorUtility.ClearProgressBar();

        SaveEntries(entries);

        string text = "Exported " + done.Count + " model(s) to " + OutputFolder + ":\n  " + string.Join("\n  ", done) +
                      (failed.Count > 0 ? "\n\nFailed:\n  " + string.Join("\n  ", failed) : "") +
                      "\n\nNow run:  python D:\\deploy_tools\\deploy_web.py";
        Debug.Log("[Export] " + text);
        EditorUtility.DisplayDialog("Export models", text, "OK");
    }

    static string Slug(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        string slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "model" : slug;
    }

    static List<Entry> LoadEntries()
    {
        string path = Path.Combine(OutputFolder, "models.json");
        try
        {
            if (File.Exists(path))
            {
                var w = JsonUtility.FromJson<Wrapper>("{\"items\":" + File.ReadAllText(path) + "}");
                if (w != null && w.items != null) return new List<Entry>(w.items);
            }
        }
        catch (Exception) { }
        return new List<Entry>();
    }

    static void SaveEntries(List<Entry> entries)
    {
        string json = JsonUtility.ToJson(new Wrapper { items = entries.ToArray() }, true);
        int a = json.IndexOf('[');
        int b = json.LastIndexOf(']');
        File.WriteAllText(Path.Combine(OutputFolder, "models.json"), json.Substring(a, b - a + 1));
    }
}
