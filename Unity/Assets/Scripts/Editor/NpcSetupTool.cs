using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Tools > Ghost > Setup NPC Worker
// Finds the object named "NPC" and the point named "NPC Stand Check", then adds the NavMeshAgent and the NpcWorker
// and connects the four animation clips from Assets/NPC/ANIM.
public static class NpcSetupTool
{
    const string AnimFolder = "Assets/NPC/ANIM/";

    [MenuItem("Tools/Ghost/Setup NPC Worker")]
    public static void Setup()
    {
        Transform npc = null, stand = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.name == "NPC") npc = t;
            else if (t.name == "NPC Stand Check") stand = t;
        }

        if (npc == null)
        {
            EditorUtility.DisplayDialog("Setup NPC Worker", "No object named 'NPC' in the open scene.", "OK");
            return;
        }

        var animator = npc.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(npc.gameObject);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var agent = npc.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = Undo.AddComponent<NavMeshAgent>(npc.gameObject);
            agent.radius = 0.3f;
            agent.height = 1.7f;
            agent.stoppingDistance = 0.3f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        var worker = npc.GetComponent<NpcWorker>();
        if (worker == null) worker = Undo.AddComponent<NpcWorker>(npc.gameObject);

        // The NPC must not be a child of the stand point (the point stays, the NPC walks)
        if (stand != null && npc.IsChildOf(stand))
            Undo.SetTransformParent(npc, null, "Free the NPC");

        worker.useHome = true;
        worker.homePosition = new Vector3(162.29f, 0.4098f, -18.43f);
        worker.homeEuler = new Vector3(0f, 238.72f, 0f);
        worker.wanderRadius = 40f;
        worker.maxHeightDelta = 0.3f;

        worker.standPoint = stand;
        worker.standLocalEuler = new Vector3(0f, 180f, 0f);
        worker.standLocalPosition = Vector3.zero;

        worker.idleClip = Load("Neutral Idle");
        worker.lookAroundClip = Load("Nervously Look Around");
        worker.walkClip = Load("Walking");
        worker.pushButtonClip = Load("Button Pushing");

        // The Drop Table gives the EMPTY cage back after the check
        InvItemData emptyCage = null;
        foreach (var guid in AssetDatabase.FindAssets("t:InvItemData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<InvItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.combineResult != null && data.combineResult.submitPoints > 0) { emptyCage = data; break; }
        }
        foreach (var table in Object.FindObjectsByType<DropTable>(FindObjectsSortMode.None))
        {
            table.returnItem = emptyCage;
            EditorUtility.SetDirty(table);
        }

        EditorUtility.SetDirty(worker);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = npc.gameObject;

        string missing = "";
        if (stand == null) missing += " 'NPC Stand Check' not found (assign Stand Point yourself).";
        if (worker.idleClip == null || worker.lookAroundClip == null || worker.walkClip == null || worker.pushButtonClip == null)
            missing += " Some clips were not found in " + AnimFolder + ".";

        Debug.Log("NpcSetupTool: NPC is ready (NavMeshAgent + NpcWorker + 4 clips)." + missing +
                  " It appears at 7:00 AM, goes away at 4:00 PM. Save the scene.");
    }

    static AnimationClip Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + name + ".anim");
    }

    // ================================================================ the real NPC (the man in the white suit)

    const string RealFolder = "Assets/ASSET/NPC/";
    const string IdleFbx = RealFolder + "idle02.fbx";
    const string ScanFbx = RealFolder + "scan02_anim.fbx";
    const string SuitMaterial = RealFolder + "hazat_MAT.mat";
    const string SuitTexture = RealFolder + "hazat_MAT_BaseColor.1001.jpg";
    const float BodyScale = 15f;       // the size of the body in the house

    // Tools > Ghost > Use The Real NPC
    // Takes the NPC of the open scene (the object named "NPC") and gives it the new body:
    //   - the white protective suit (idle02.fbx), its picture put on the suit's material
    //   - the idle animation (idle02) and the scan animation (scan02_anim): the scan plays while a cage is checked
    //   - the NPC stands at its place and does not walk (Stays In Place)
    // Run it in SampleScene and in TutorialScene (each has its own NPC), then save the scene.
    [MenuItem("Tools/Ghost/Use The Real NPC")]
    public static void UseRealNpc()
    {
        string text = ApplyToOpenScene(out bool ok);
        if (ok) Debug.Log("[NPC] " + text);
        EditorUtility.DisplayDialog("Use The Real NPC", text, "OK");
    }

    // The same for SampleScene (the game) and TutorialScene (the training): each has its own NPC. Both scenes are saved.
    [MenuItem("Tools/Ghost/Use The Real NPC In Game And Training")]
    public static void UseRealNpcEverywhere()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string back = EditorSceneManager.GetActiveScene().path;
        string report = "";
        foreach (var path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/TutorialScene.unity" })
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { report += path + ": the scene was not found.\n\n"; continue; }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string text = ApplyToOpenScene(out bool ok);
            if (ok) EditorSceneManager.SaveOpenScenes();
            report += System.IO.Path.GetFileNameWithoutExtension(path) + ": " + (ok ? "done and saved." : text) + "\n\n";
        }

        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        Debug.Log("[NPC] " + report);
        EditorUtility.DisplayDialog("Use The Real NPC", report + "He stands at the old home of the NPC. In SampleScene move the 'NPC' object near the door and use NpcWorker > Use This Place As Home.", "OK");
    }

    // Puts the real NPC into the open scene. Returns the text for the player; ok is false when nothing was done
    static string ApplyToOpenScene(out bool ok)
    {
        ok = false;
        Transform npc = null, stand = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.name == "NPC" && t.GetComponent<NpcWorker>() != null) npc = t;
            else if (t.name == "NPC Stand Check") stand = t;
        }
        if (npc == null) return "No object named 'NPC' with the NpcWorker script in this scene.";

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbx);
        if (model == null) return "The file " + IdleFbx + " was not found.";

        // 1) the two animation files, one skeleton
        string rigNote = SetupRig();

        // 2) the suit's picture
        var material = AssetDatabase.LoadAssetAtPath<Material>(SuitMaterial);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SuitTexture);
        if (material != null && texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
        }

        var avatar = LoadAvatar(IdleFbx);
        var idle = LoadClip(IdleFbx);
        var scan = LoadClip(ScanFbx);

        // a copy of the suit man that was dragged into the scene by hand (not part of the NPC) is a second man: it goes
        foreach (var root in npc.gameObject.scene.GetRootGameObjects())
        {
            if (root.transform == npc || !root.name.StartsWith("idle02")) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (source != null && AssetDatabase.GetAssetPath(source) == IdleFbx) Undo.DestroyObjectImmediate(root);
        }

        // 3) the old body goes: its parts, and the Animator on the NPC itself (the new one sits on the new body)
        var worker = npc.GetComponent<NpcWorker>();
        for (int i = npc.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(npc.GetChild(i).gameObject);
        var oldAnimator = npc.GetComponent<Animator>();
        if (oldAnimator != null) Undo.DestroyObjectImmediate(oldAnimator);

        Undo.RecordObject(npc, "Real NPC");
        npc.localScale = Vector3.one;

        // 4) the new body
        var body = (GameObject)PrefabUtility.InstantiatePrefab(model, npc);
        Undo.RegisterCreatedObjectUndo(body, "Real NPC body");
        body.name = "NPC Body";
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        // its size: the file is small, the house is big: 15 times (change 'Scale' on NPC Body if you want him bigger or smaller)
        body.transform.localScale = Vector3.one * BodyScale;
        Bounds bounds = BodyBounds(body);
        if (bounds.size.y > 0.001f)
            body.transform.position += Vector3.up * (npc.position.y - bounds.min.y);     // the feet on the floor

        var animator = body.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(body);
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 5) the worker: it stands still, idles, and scans while a cage is checked
        Undo.RecordObject(worker, "Real NPC worker");
        worker.staysInPlace = true;
        worker.walkChance = 0f;
        worker.idleClip = idle;
        worker.pushButtonClip = scan;     // "push" is the work animation: here it is the scan
        worker.lookAroundClip = null;
        worker.walkClip = null;
        if (stand != null) worker.standPoint = stand;

        EditorUtility.SetDirty(worker);
        EditorUtility.SetDirty(npc);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = body;

        string missing = "";
        if (avatar == null) missing += "\n  - no Avatar was made (see the Rig tab of idle02.fbx).";
        if (idle == null) missing += "\n  - no idle animation found in idle02.fbx.";
        if (scan == null) missing += "\n  - no scan animation found in scan02_anim.fbx.";
        if (texture == null) missing += "\n  - the suit picture " + SuitTexture + " was not found.";

        ok = true;
        return "The real NPC is in place.\n" + rigNote + "\nIt stands at its place (move the 'NPC' object where you want it, near the door, then right-click NpcWorker > Use This Place As Home) and plays the scan animation while a cage is checked." +
               (missing.Length > 0 ? "\n\nCheck:" + missing : "") +
               "\n\nIf he looks the wrong way, turn 'NPC Body' by 180 degrees on Y. Save the scene (Ctrl+S).";
    }

    // Humanoid for both files (the animations of one file play on the body of the other). If the skeleton is not a human one, Generic.
    static string SetupRig()
    {
        var idle = (ModelImporter)AssetImporter.GetAtPath(IdleFbx);
        var scan = (ModelImporter)AssetImporter.GetAtPath(ScanFbx);
        if (idle == null || scan == null) return "The animation files were not found.";

        idle.animationType = ModelImporterAnimationType.Human;
        idle.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        idle.importAnimation = true;
        idle.SaveAndReimport();

        var avatar = LoadAvatar(IdleFbx);
        if (avatar != null && avatar.isValid && avatar.isHuman)
        {
            scan.animationType = ModelImporterAnimationType.Human;
            scan.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            scan.sourceAvatar = avatar;
            scan.importAnimation = true;
            scan.SaveAndReimport();
            return "Rig: Humanoid (the animations are shared between the two files).";
        }

        // not a human skeleton: Generic, both files with the same bone names
        idle.animationType = ModelImporterAnimationType.Generic;
        idle.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        idle.SaveAndReimport();
        scan.animationType = ModelImporterAnimationType.Generic;
        scan.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        scan.SaveAndReimport();
        return "Rig: Generic (the skeleton is not a human one). The two files must have the same bone names.";
    }

    static Avatar LoadAvatar(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Avatar avatar) return avatar;
        return null;
    }

    static AnimationClip LoadClip(string path)
    {
        AnimationClip best = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
            if (best == null || clip.length > best.length) best = clip;
        }
        return best;
    }

    static Bounds BodyBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
