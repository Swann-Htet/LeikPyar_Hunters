using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Setup Hotbar
// Checks the bottom bar (1 gun, 2 scanner, 3 injector) and puts the new icons in the open scene:
//   - the three slot pictures and the injector model are in Resources/Hotbar
//   - the Upgrade Panel icons (Gun, EM, MEP, Radio) come from Resources/UpgradeIcons (also in the scene, so you see them in the Inspector)
//   - the old G / R keys and phone buttons are not used any more
// The bar itself makes itself when the game starts: no object is needed in the scene.
public static class HotbarSetupTool
{
    static readonly string[] Needed =
    {
        "UpgradeIcons/Gun", "UpgradeIcons/Radio", "UpgradeIcons/Injector",
    };

    [MenuItem("Tools/Ghost/Setup Hotbar")]
    public static void Run()
    {
        var report = new StringBuilder();

        foreach (var path in Needed)
        {
            if (Resources.Load<Sprite>(path) == null)
                report.AppendLine("  MISSING sprite: Resources/" + path + ".png (it must be imported as a Sprite)");
        }
        if (Resources.Load<GameObject>("Hotbar/InjectorHeld") == null)
            report.AppendLine("  MISSING: Resources/Hotbar/InjectorHeld.prefab");

        var system = Object.FindFirstObjectByType<UpgradeSystem>();
        if (system == null)
        {
            report.AppendLine("  No UpgradeSystem in the open scene (open SampleScene to put the icons in it)");
        }
        else
        {
            Undo.RecordObject(system, "Upgrade icons");
            foreach (var u in system.upgrades)
            {
                if (u == null) continue;
                var sprite = Resources.Load<Sprite>("UpgradeIcons/" + u.id);
                if (sprite == null) { report.AppendLine("  no icon for upgrade '" + u.id + "'"); continue; }
                u.icon = sprite;
                report.AppendLine("  upgrade icon set: " + u.id);
            }
            EditorUtility.SetDirty(system);
            EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
        }

        foreach (var icon in new[] { "Bag", "Torch", "Heat", "GunBattery", "Time", "Setting", "BeanPower" })
            if (Resources.Load<Sprite>("UIIcons/" + icon) == null) report.AppendLine("  MISSING HUD icon: Resources/UIIcons/" + icon + ".png");

        if (Object.FindFirstObjectByType<PlayerController>() == null)
            report.AppendLine("  (no player in this scene: the hotbar appears wherever a player is)");

        string text = report.Length == 0 ? "Everything is in place." : report.ToString();
        Debug.Log("[Hotbar] " + text);
        EditorUtility.DisplayDialog("Setup Hotbar",
            text + "\n\nPlay: 1 = gun, 2 = scanner, 3 = injector (left click heals). Save the scene (Ctrl+S).", "OK");
    }
}
