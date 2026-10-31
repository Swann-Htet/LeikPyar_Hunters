using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Use Butterfly Orb, MirageGhost And Muzzle Orb
// One click for the new look:
//   1. The Orb (Lv 1 soul) becomes the animated butterfly: Assets/ASSET/Orb/Butterfly Orb.prefab is made from ASSET/animated_butterfly.glb
//      (the "Flying" animation in a loop, a little light blue emission, a soft blue light) and put in GhostSpawner > Orb Prefab.
//   2. The old orb (ASSET/Orb/magical_orb.glb) is not an Orb any more: it goes to GhostGun > Muzzle Orb. It is shown small in the gap
//      between the gun and the electric effect while the gun fires, with a blue light, as the light effect of the muzzle.
//   3. The Mirage (Lv 2) is your MirageGhost model (see MirageModelTool): same functions, new model, its animation in a loop.
// The .glb files need the glTFast package. Run it in SampleScene (and in TutorialScene if it has the ghosts too), then save the scene.
public static class ButterflyOrbTool
{
    const string ButterflyModel = "Assets/ASSET/animated_butterfly.glb";
    const string ButterflyPrefab = "Assets/ASSET/Orb/Butterfly Orb.prefab";
    const string OldOrbModel = "Assets/ASSET/Orb/magical_orb.glb";

    [MenuItem("Tools/Ghost/Use Butterfly Orb, MirageGhost And Muzzle Orb")]
    public static void Run()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Butterfly orb", "No GhostSpawner in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ButterflyModel);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Butterfly orb",
                "Unity cannot use " + ButterflyModel + " yet.\n\nThe .glb files need the glTFast package. Wait until Unity has finished importing " +
                "(the spinner at the bottom right), right-click the file > Reimport, and run this again.", "OK");
            return;
        }

        // 1. the butterfly prefab
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Butterfly Orb";

        var clip = MirageModelTool.FindClip(ButterflyModel, "fly");
        string animation;
        if (clip != null)
        {
            var player = instance.AddComponent<AnimatedModel>();
            player.clip = clip;
            player.speed = 1f;
            animation = "'" + clip.name + "' plays in a loop";
        }
        else
        {
            animation = "the model has no animation";
        }

        instance.AddComponent<ButterflyOrb>();
        var butterfly = PrefabUtility.SaveAsPrefabAsset(instance, ButterflyPrefab);
        Object.DestroyImmediate(instance);

        Undo.RecordObject(spawner, "Butterfly orb");
        spawner.orbPrefab = butterfly;
        EditorUtility.SetDirty(spawner);

        // 2. the old orb at the muzzle of the gun
        var gun = Object.FindFirstObjectByType<GhostGun>();
        var oldOrb = AssetDatabase.LoadAssetAtPath<GameObject>(OldOrbModel);
        string muzzle;
        if (gun != null && oldOrb != null)
        {
            Undo.RecordObject(gun, "Muzzle orb");
            gun.muzzleOrb = oldOrb;
            gun.fillMuzzleGap = true;
            EditorUtility.SetDirty(gun);
            muzzle = "The old orb is small at the gun's muzzle while it fires (GhostGun > Muzzle Orb).";
        }
        else
        {
            muzzle = "The gun or " + OldOrbModel + " was not found: the muzzle only has the light.";
        }

        // 3. the Mirage
        string mirage = MirageModelTool.Build(spawner);

        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        AssetDatabase.SaveAssets();

        string text = "Done.\n\n" +
                      "ORB (Lv 1): the animated butterfly (" + animation + ", a little light blue emission). Prefab: " + ButterflyPrefab +
                      ". Its size is GhostSpawner > Orb Width (" + spawner.orbWidth + " m wide), its height Orb Lift.\n\n" +
                      "MUZZLE: " + muzzle + "\n\n" +
                      "MIRAGE (Lv 2): " + mirage + "\n\nSave the scene (Ctrl+S).";
        Debug.Log("[ButterflyOrbTool] " + text);
        EditorUtility.DisplayDialog("Butterfly orb", text, "OK");
    }
}
