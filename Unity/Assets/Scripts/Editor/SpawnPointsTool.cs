using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Tools > Ghost > Fix Ghost Spawn Points
// A Spwan Point on the roof (or anywhere the woman can not walk from) is moved into a room of the house:
//   - a point is bad when it is higher than the upper floor + 3 m, or when no NavMesh is within 3 m of it
//   - it goes to the first free spot of the list below (rooms where the baby ghosts and Mirages already hide, so they are on the baked
//     floor): on the NavMesh and at least 8 m from the other spawn points
// The woman walks from one spawn point (a "place") to the next, every place once before one comes again (see GhostSpawner).
public static class SpawnPointsTool
{
    const float RoofHeight = 14f;
    const float MinGap = 8f;

    // rooms of the upper floor and the ground floor that are known to be on the NavMesh
    static readonly Vector3[] Rooms =
    {
        new Vector3(188.7f, 9.3f, -45.1f),
        new Vector3(194.6f, 9.3f, -16.2f),
        new Vector3(184.0f, 9.3f, -11.5f),
        new Vector3(154.2f, 9.3f, -19.8f),
        new Vector3(151.8f, 0.9f, -23.2f),
    };

    [MenuItem("Tools/Ghost/Fix Ghost Spawn Points")]
    public static void Run()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Fix Ghost Spawn Points", "No GhostSpawner in the open scene. Open SampleScene and try again.", "OK");
            return;
        }

        var points = new List<Transform>();
        if (spawner.spawnPoints != null) foreach (var t in spawner.spawnPoints) if (t != null) points.Add(t);
        if (points.Count == 0)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Spwan Point") || t.name.StartsWith("Spawn Point")) points.Add(t);
            points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        var report = new StringBuilder();
        int moved = 0;

        foreach (var point in points)
        {
            if (IsGood(point.position))
            {
                report.AppendLine("  " + point.name + " is fine (" + Format(point.position) + ")");
                continue;
            }

            Vector3? target = null;
            foreach (var room in Rooms)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(room, out hit, 2.5f, NavMesh.AllAreas)) continue;
                if (TooClose(hit.position, points, point)) continue;
                target = hit.position;
                break;
            }

            if (target == null)
            {
                report.AppendLine("  " + point.name + " is bad (" + Format(point.position) + ") and no free room was found: move it onto the baked floor by hand");
                continue;
            }

            Undo.RecordObject(point, "Move spawn point");
            report.AppendLine("  " + point.name + " " + Format(point.position) + "  ->  " + Format(target.Value));
            point.position = target.Value;
            EditorUtility.SetDirty(point);
            moved++;
        }

        if (moved > 0) EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

        string text = (moved > 0 ? moved + " spawn point(s) moved.\n" : "Nothing to move.\n") + report +
                      "\nSave the scene (Ctrl+S). Run Create Tutorial Scene again to copy the change there.";
        Debug.Log("[Ghost] " + text);
        EditorUtility.DisplayDialog("Fix Ghost Spawn Points", text, "OK");
    }

    static bool IsGood(Vector3 position)
    {
        if (position.y > RoofHeight) return false;
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas);
    }

    static bool TooClose(Vector3 candidate, List<Transform> points, Transform except)
    {
        foreach (var p in points)
        {
            if (p == except) continue;
            if (Vector3.Distance(p.position, candidate) < MinGap) return true;
        }
        return false;
    }

    static string Format(Vector3 v)
    {
        return "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";
    }
}
