using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Use My Mirage Model As Ghost
// Makes the Mirage (Lv 2) from your model Assets/ASSET/MirageGhost.glb and puts it in GhostSpawner > Mirage Template.
// Everything else is the same as before: the Mirage hides in the house, floats and sways, and runs from a weak gun (see MirageGhost).
//   - The prefab Assets/ASSET/Mirage/Mirage Ghost.prefab is made again (the same file, so nothing that points to it breaks).
//   - The model keeps its own look (the glb material is already a see-through ghost). Its own clip is a dance, so it gets a calm idle
//     pose by code instead (IdlePose).
//   - The model is a CHILD of an empty root: MirageGhost puts everything under the root into one pivot to sway it, and the animation of
//     a model must not lose its own bones when that happens.
//   - It gets the height of the Mirage model it replaces.
// Tools > Ghost > Use Butterfly Orb, MirageGhost And Muzzle Orb runs this too.
public static class MirageModelTool
{
    const string ModelPath = "Assets/ASSET/MirageGhost.glb";
    const string PrefabPath = "Assets/ASSET/Mirage/Mirage Ghost.prefab";

    [MenuItem("Tools/Ghost/Use My Mirage Model As Ghost")]
    public static void Run()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Mirage", "No GhostSpawner in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        string text = Build(spawner);
        Debug.Log("[Mirage] " + text);
        EditorUtility.DisplayDialog("Mirage", text, "OK");
    }

    // Makes the prefab and puts it in the spawner. Returns what to tell the user.
    public static string Build(GhostSpawner spawner)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
            return "Unity cannot use " + ModelPath + " yet. The .glb files need the glTFast package: wait until Unity has finished importing (the spinner at the bottom right), right-click the file > Reimport, and run this again.";

        // the height of the Mirage that is replaced
        float oldHeight = 0f;
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (old != null)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(old);
            oldHeight = HeightOf(temp);
            Object.DestroyImmediate(temp);
        }

        // an empty root, and the model under it
        var root = new GameObject("Mirage Ghost");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "MirageGhost model";
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        float newHeight = HeightOf(instance);
        string sizeNote = "";
        if (oldHeight > 0.3f && newHeight > 0.05f)
        {
            float factor = oldHeight / newHeight;
            if (factor < 0.9f || factor > 1.1f)
            {
                instance.transform.localScale = Vector3.one * factor;
                sizeNote = "It was scaled by " + factor.ToString("0.00") + " to be as high as the old Mirage (" + oldHeight.ToString("0.0") + " m).\n";
            }
        }

        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // the clip inside the file is a Mixamo DANCE, so it is not used: a calm idle pose is made by code (IdlePose)
        instance.AddComponent<IdlePose>();
        string animationNote = "It stands in an idle pose (arms down, breathing, the head turning slowly): IdlePose. The dance inside the file is not played.\n";

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Undo.RecordObject(spawner, "Mirage model");
        spawner.mirageTemplate = prefab;
        EditorUtility.SetDirty(spawner);

        // a copy of the old model in the scene is not needed any more: the spawner makes its own copies
        int hidden = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if ((t.name == "lowpoly_basemesh_male" || t.name == "MirageGhost") && t.parent == null && t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Hide scene model");
                t.gameObject.SetActive(false);
                hidden++;
            }
        }

        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        AssetDatabase.SaveAssets();

        return "The Mirage is your MirageGhost model now (prefab: " + PrefabPath + ").\n" + animationNote + sizeNote +
               (hidden > 0 ? "A copy of the model in the scene was switched off.\n" : "") +
               "It behaves like before: it hides, floats, and runs from a weak gun.";
    }

    // The animation clip inside a model file whose name contains the keyword (empty keyword = the first one)
    public static AnimationClip FindClip(string assetPath, string keyword)
    {
        AnimationClip first = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            var clip = o as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__")) continue;
            if (first == null) first = clip;
            if (!string.IsNullOrEmpty(keyword) && clip.name.ToLowerInvariant().Contains(keyword.ToLowerInvariant())) return clip;
        }
        return first;
    }

    public static float HeightOf(GameObject go)
    {
        bool any = false;
        Bounds box = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) { box = r.bounds; any = true; }
            else box.Encapsulate(r.bounds);
        }
        return any ? box.size.y : 0f;
    }
}
