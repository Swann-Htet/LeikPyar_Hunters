using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Set Ghost Types (Orb = babies, Solid = the woman)
// Gives every ghost in the open scene its type tag:
//   - every baby ghost (tag "bghost") becomes "Orb"
//   - the woman of the GhostSpawner (Womann@Sad Idle) becomes "Solid"
// "Mirage" is the third type: drag your Mirage prefab (or an object) into the GhostSpawner's "Mirage Template".
// (The GhostSpawner keeps the woman out of the baby list, so a tagged woman is not caught by the gun.)
public static class GhostTypesTool
{
    [MenuItem("Tools/Ghost/Set Ghost Types (Orb = babies, Solid = the woman)")]
    public static void Run()
    {
        GhostSetupTool.AddTypeTags();

        var report = new StringBuilder();
        int orbs = 0;

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.gameObject.scene.IsValid() || t.gameObject.tag != BabyGhostFinder.Tag) continue;

            Undo.RecordObject(t.gameObject, "Ghost type Orb");
            t.gameObject.tag = "Orb";
            EditorUtility.SetDirty(t.gameObject);
            orbs++;
        }
        report.AppendLine("  " + orbs + " baby ghost(s) -> Orb");

        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner != null && spawner.ghost != null)
        {
            Undo.RecordObject(spawner.ghost.gameObject, "Ghost type Solid");
            spawner.ghost.gameObject.tag = "Solid";
            EditorUtility.SetDirty(spawner.ghost.gameObject);
            report.AppendLine("  " + spawner.ghost.name + " (the woman) -> Solid");
        }
        else
        {
            report.AppendLine("  no GhostSpawner / woman found: tag her yourself (Inspector > Tag > Solid)");
        }

        if (spawner != null && spawner.mirageTemplate == null)
            report.AppendLine("  Mirage: drag your Mirage prefab into GhostSpawner > Mirage Template (until then the game uses its own teal wisp Mirage)");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("GhostTypesTool:\n" + report + "Save the scene.");
    }
}
