using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Setup Scanner
// Sets the ghost scanner up in the open scene:
//   - GhostDevice is on the player, with the 'scanner' model as its Device Object (child of 'Device Hand')
//   - keys: R takes it out, O switches it on
//   - the camera, the GhostSpawner and the woman's collider are found for it
//   - checks that the model has the parts ON, OFF, DETECT, LEVEL > ONE - FIVE and that the LED materials exist
//   - the model is shown with all its lights dark (the game lights them)
// There is no screen any more: the lights tell everything.
public static class ScannerSetupTool
{
    static readonly string[] Parts = { "ON", "OFF", "DETECT", "ONE", "TWO", "THREE", "FOUR", "FIVE" };

    [MenuItem("Tools/Ghost/Setup Scanner")]
    public static void Run()
    {
        var report = new StringBuilder();

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Scanner", "No player (PlayerController) in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        var device = player.GetComponent<GhostDevice>();
        if (device == null)
        {
            device = Undo.AddComponent<GhostDevice>(player.gameObject);
            report.AppendLine("  GhostDevice added to " + player.name);
        }

        // A second GhostDevice (for example one added on the scanner model itself) would answer the same keys twice: only the
        // one on the player stays
        foreach (var other in Object.FindObjectsByType<GhostDevice>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == device || !other.gameObject.scene.IsValid()) continue;
            report.AppendLine("  Extra GhostDevice removed from '" + other.gameObject.name + "'");
            Undo.DestroyObjectImmediate(other);
        }

        Undo.RecordObject(device, "Setup scanner");

        // the model: 'scanner' under 'Device Hand' (or the Device Hand itself)
        Transform hand = FindDeep(player.transform, "Device Hand");
        Transform model = hand != null ? FindDeep(hand, "scanner") : null;
        if (hand == null)
        {
            report.AppendLine("  !! 'Device Hand' not found under " + player.name);
        }
        else
        {
            device.deviceObject = model != null ? model.gameObject : hand.gameObject;
            report.AppendLine("  Device Object: " + device.deviceObject.name);
        }

        device.playerCamera = player.GetComponentInChildren<Camera>(true) != null ? player.GetComponentInChildren<Camera>(true) : Camera.main;
        device.spawner = Object.FindFirstObjectByType<GhostSpawner>();
        device.toggleKey = KeyCode.R;
        device.powerKey = KeyCode.O;
        device.startHidden = true;
        device.startPowered = false;

        var woman = device.spawner != null ? device.spawner.ghost : null;
        device.ghostRange = woman != null ? woman.GetComponent<BoxCollider>() : null;
        report.AppendLine("  Solid ghost (the woman): " + (woman != null ? woman.name : "none") +
                          ", Ghost Range: " + (device.ghostRange != null ? "her " + device.ghostRange.GetType().Name : "none (she has no BoxCollider)"));
        report.AppendLine("  Camera: " + (device.playerCamera != null ? device.playerCamera.name : "none") +
                          ", GhostSpawner: " + (device.spawner != null ? "found" : "none") +
                          ", keys: R = out / away, O = on / off");

        // the lights of the model
        if (device.deviceObject != null)
        {
            var missing = new StringBuilder();
            foreach (var part in Parts)
            {
                Transform t = FindDeep(device.deviceObject.transform, part);
                if (t == null || (t.GetComponent<Renderer>() == null && t.GetComponentInChildren<Renderer>(true) == null))
                    missing.Append(part + " ");
            }
            report.AppendLine(missing.Length == 0
                ? "  Lights ON, OFF, DETECT, ONE-FIVE: all found"
                : "  !! Lights missing (name or mesh renderer): " + missing);
        }

        // the LED materials (Assets/Resources/Scanner)
        foreach (var name in new[] { "LED_Green", "LED_Yellow", "LED_Red", "LED_Dark" })
            if (Resources.Load<Material>("Scanner/" + name) == null)
                report.AppendLine("  !! Material Resources/Scanner/" + name + " is missing");

        // all the lights dark, as the game starts
        var dark = Resources.Load<Material>("Scanner/LED_Dark");
        if (dark != null && device.deviceObject != null)
        {
            foreach (var part in Parts)
            {
                Transform t = FindDeep(device.deviceObject.transform, part);
                var r = t != null ? (t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>() : t.GetComponentInChildren<Renderer>(true)) : null;
                if (r == null) continue;

                Undo.RecordObject(r, "Scanner light dark");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats.Length == 1 || (mats[i] != null && (mats[i].name == "ON" || mats[i].name.StartsWith("LED_")))) mats[i] = dark;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }

        EditorUtility.SetDirty(device);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = device;

        Debug.Log("ScannerSetupTool:\n" + report + "Save the scene. Play: R takes the scanner out, O switches it on.");
        EditorUtility.DisplayDialog("Setup Scanner", "Done. The details are in the Console. Save the scene.", "OK");
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
