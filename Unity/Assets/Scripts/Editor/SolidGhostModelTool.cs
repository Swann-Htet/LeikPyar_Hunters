using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Use ghost_woman For The Solid Ghost
// The woman (Solid) gets the new model Assets/ASSET/ghost_woman.fbx and keeps her old animations:
//   - the old body (Body, Hair, Tops, Eyes, Eyelashes and the mixamorig skeleton under "Solid") is removed,
//   - ghost_woman is put under "Solid" and gets the same Animator Controller as before (Walk / Run / Sad Idle ... the animations did not change),
//     the same bones (mixamorig:...) make the old animation clips fit the new body,
//   - her TEXTURES: ghost_woman.fbx has no textures of its own (it only points to Womann_Body_diffuse.png and Womann_Hair_diffuse.png on another
//     computer), so Body and Hair get the materials Assets/Prefab/Ghost/textures/Body.mat and Hair.mat: the same full size pictures as before,
//     nothing is made smaller (the UV layout of the new Body and Hair is the same as the old one),
//   - the new file has only Body and Hair: the clothes (Tops), Eyes and Eyelashes are taken from the old model file and put on the new skeleton,
//     with Top.mat for the clothes,
//   - the voices, the songs, the spawn point and the collider on "Solid" stay as they are,
//   - the size is matched to the old body.
// Run it in SampleScene (and again in TutorialScene if the woman is there), then save the scene. Undo (Ctrl+Z) brings the old body back.
public static class SolidGhostModelTool
{
    const string ModelPath = "Assets/ASSET/ghost_woman.fbx";
    const string MaterialFolder = "Assets/ASSET/Materials";
    const string OldModelPath = "Assets/Prefab/Ghost/Womann@Sad Idle.fbx";
    const string TexturesFolder = "Assets/Prefab/Ghost/textures/";

    static readonly string[] Keep = { "Chase", "Jump Scare", "Ghost Sing", "Spwan", "Spawn" };

    [MenuItem("Tools/Ghost/Use ghost_woman For The Solid Ghost")]
    public static void Run()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null || spawner.ghost == null)
        {
            EditorUtility.DisplayDialog("ghost_woman", "No GhostSpawner with a Ghost in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            EditorUtility.DisplayDialog("ghost_woman", "Missing: " + ModelPath, "OK");
            return;
        }

        Transform solid = spawner.ghost;
        var oldAnimator = solid.GetComponent<Animator>();
        if (oldAnimator == null) oldAnimator = solid.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController controller = oldAnimator != null ? oldAnimator.runtimeAnimatorController : null;
        if (controller == null)
        {
            EditorUtility.DisplayDialog("ghost_woman", "The Solid ghost has no Animator Controller, so there are no animations to keep.", "OK");
            return;
        }

        // the old body: its height, and its materials (by the name of the part)
        var oldMaterials = new Dictionary<string, Material[]>();
        Bounds oldBounds = default;
        bool haveBounds = false;
        foreach (var r in solid.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            oldMaterials[r.name] = r.sharedMaterials;
            if (!haveBounds) { oldBounds = r.bounds; haveBounds = true; }
            else oldBounds.Encapsulate(r.bounds);
        }

        // 1. the old body goes (everything under Solid except the voices, the songs and the spawn point)
        var remove = new List<GameObject>();
        foreach (Transform child in solid)
        {
            if (System.Array.IndexOf(Keep, child.name) >= 0) continue;
            if (child.name.StartsWith("ghost_woman")) { remove.Add(child.gameObject); continue; }     // run again: the previous try goes too
            if (child.GetComponent<AudioSource>() != null && child.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
            if (child.name.StartsWith("mixamorig") || child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null || child.GetComponent<SkinnedMeshRenderer>() != null)
                remove.Add(child.gameObject);
        }
        foreach (var go in remove) Undo.DestroyObjectImmediate(go);

        // 2. the new body
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, solid);
        Undo.RegisterCreatedObjectUndo(instance, "ghost_woman");
        instance.name = "ghost_woman";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // the same size as the old body
        Bounds newBounds = default;
        bool haveNew = false;
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (!haveNew) { newBounds = r.bounds; haveNew = true; }
            else newBounds.Encapsulate(r.bounds);
        }
        if (haveBounds && haveNew && newBounds.size.y > 0.05f && oldBounds.size.y > 0.05f)
        {
            float factor = oldBounds.size.y / newBounds.size.y;
            if (factor < 0.85f || factor > 1.18f)
            {
                instance.transform.localScale = Vector3.one * factor;
                Debug.Log("[ghost_woman] The new body was " + newBounds.size.y.ToString("0.00") + " m high, the old one " + oldBounds.size.y.ToString("0.00") +
                          " m: scaled by " + factor.ToString("0.00"));
            }
        }

        // 3. her animations: the same controller, on the new body (the bone names are the same, so the old clips fit)
        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(instance);
        animator.runtimeAnimatorController = controller;
        animator.avatar = null;
        animator.applyRootMotion = false;                     // she is moved by GhostSpawner, not by the clips
        animator.cullingMode = oldAnimator != null ? oldAnimator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = oldAnimator != null ? oldAnimator.updateMode : AnimatorUpdateMode.Normal;

        if (oldAnimator != null && oldAnimator.gameObject == solid.gameObject)
            Undo.DestroyObjectImmediate(oldAnimator);          // the Animator is on the body now

        // 4. the parts the new file does not have (clothes, eyes, eyelashes): from the old model, on the new skeleton
        int added = AddMissingParts(instance);

        // 5. materials: the old ones, with the full size textures
        int fixedMaterials = 0;
        foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material m = MaterialFor(r.name);
            if (m == null) continue;

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            fixedMaterials++;
        }

        // the spawner finds the renderers and the Animator again from the Solid object
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = instance;
        AssetDatabase.SaveAssets();

        Debug.Log("[ghost_woman] Done: '" + solid.name + "' now has the ghost_woman body (" + remove.Count + " old parts removed, " + added +
                  " parts added from the old model, " + fixedMaterials + " materials set). Her animations come from the controller " + controller.name + ". Save the scene.");
        Debug.Log(Describe(instance));
        EditorUtility.DisplayDialog("ghost_woman",
            "Done. The woman (Solid) has the ghost_woman body and keeps her old animations.\n\n" +
            "If she faces the wrong way, turn ghost_woman (the child of Solid) on Y, or change GhostSpawner > Rotation Offset Y.\n" +
            "Save the scene (Ctrl+S).", "OK");
    }

    // The material of each part: the old project materials (the textures are the full size pictures of Prefab/Ghost/textures)
    static Material MaterialFor(string part)
    {
        switch (part)
        {
            case "Body": return AssetDatabase.LoadAssetAtPath<Material>(TexturesFolder + "Body.mat");
            case "Hair": return AssetDatabase.LoadAssetAtPath<Material>(TexturesFolder + "Hair.mat");
            case "Tops": return AssetDatabase.LoadAssetAtPath<Material>(TexturesFolder + "Top.mat");
            case "Eyes": return SimpleMaterial("ghost_woman_Eyes", new Color(0.82f, 0.82f, 0.8f, 1f), 0.75f);
            case "Eyelashes": return SimpleMaterial("ghost_woman_Eyelashes", new Color(0.02f, 0.02f, 0.02f, 1f), 0.2f);
        }
        return null;
    }

    static Material SimpleMaterial(string name, Color color, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return null;
        System.IO.Directory.CreateDirectory(MaterialFolder);
        m = new Material(lit) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ghost_woman.fbx has only Body and Hair. The clothes, the eyes and the eyelashes of the old model are made again on the new skeleton:
    // the same mesh, and every bone found by its name (both files have the same mixamorig skeleton).
    static int AddMissingParts(GameObject instance)
    {
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(OldModelPath);
        if (old == null)
        {
            Debug.LogWarning("[ghost_woman] " + OldModelPath + " was not found: the clothes and the eyes are not added.");
            return 0;
        }

        var bones = new Dictionary<string, Transform>();
        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones[t.name] = t;

        int count = 0;
        foreach (var src in old.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (src.name == "Body" || src.name == "Hair" || src.sharedMesh == null) continue;
            // the new file may have an empty node with that name (Tops): the mesh goes on it. A node that already has a mesh is left alone.
            Transform existing = instance.transform.Find(src.name);
            if (existing != null && existing.GetComponent<SkinnedMeshRenderer>() != null) continue;

            var from = src.bones;
            var to = new Transform[from.Length];
            int missing = 0;
            for (int i = 0; i < from.Length; i++)
            {
                if (from[i] == null || !bones.TryGetValue(from[i].name, out to[i])) missing++;
            }
            if (missing > 0)
            {
                Debug.LogWarning("[ghost_woman] '" + src.name + "' was not added: " + missing + " of its bones are not in the new skeleton.");
                continue;
            }

            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                Undo.RecordObject(go.transform, "ghost_woman part");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
            else
            {
                go = new GameObject(src.name);
                Undo.RegisterCreatedObjectUndo(go, "ghost_woman part");
                go.transform.SetParent(instance.transform, false);
            }

            var renderer = Undo.AddComponent<SkinnedMeshRenderer>(go);
            renderer.sharedMesh = src.sharedMesh;
            renderer.bones = to;
            Transform root;
            renderer.rootBone = src.rootBone != null && bones.TryGetValue(src.rootBone.name, out root) ? root :
                                (bones.TryGetValue("mixamorig:Hips", out root) ? root : null);
            renderer.localBounds = src.localBounds;
            renderer.quality = src.quality;
            renderer.updateWhenOffscreen = src.updateWhenOffscreen;
            count++;
        }
        return count;
    }

    // What every part of the woman looks like: the material, the shader, the picture on it. Send these lines if she still looks wrong.
    static string Describe(GameObject root)
    {
        var text = new System.Text.StringBuilder("[ghost_woman] The parts of '" + root.name + "':\n");
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            text.Append("  ").Append(r.name).Append(": ")
                .Append(r.enabled && r.gameObject.activeInHierarchy ? "shown" : "HIDDEN")
                .Append(", mesh ").Append(r.sharedMesh != null ? r.sharedMesh.name + " (" + r.sharedMesh.triangles.Length / 3 + " triangles)" : "NONE")
                .Append(", bones ").Append(r.bones != null ? r.bones.Length : 0)
                .Append(", root bone ").Append(r.rootBone != null ? r.rootBone.name : "NONE");
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) { text.Append(" | material NONE"); continue; }
                Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                text.Append(" | ").Append(m.name).Append(" [").Append(m.shader != null ? m.shader.name : "no shader").Append("] picture ")
                    .Append(t != null ? t.name : "NONE");
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    [MenuItem("Tools/Ghost/Log The Solid Ghost Look")]
    public static void LogLook()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null || spawner.ghost == null)
        {
            EditorUtility.DisplayDialog("ghost_woman", "No GhostSpawner with a Ghost in the open scene.", "OK");
            return;
        }
        Debug.Log(Describe(spawner.ghost.gameObject));
    }
}
