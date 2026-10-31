using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Tools > Ghost > Check Solid Ghost NavMesh
// The Solid ghost (the woman) walks and chases over the NavMesh. This checks it:
//   - every spawn point (and her own place and the player's start) is on the NavMesh
//   - a path exists from every spawn point to every other one and to the player's start (so she can always reach the player)
//   - how big the NavMesh is
// If a spawn point is off the NavMesh you can let the tool move it onto the nearest walkable place.
public static class SolidNavMeshTool
{
    [MenuItem("Tools/Ghost/Check Solid Ghost NavMesh")]
    public static void Run()
    {
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Solid ghost NavMesh", "No GhostSpawner in the open scene.", "OK");
            return;
        }

        var triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.indices == null || triangulation.indices.Length < 3)
        {
            EditorUtility.DisplayDialog("Solid ghost NavMesh", "There is no NavMesh in the scene. Select the house and press Bake in its NavMeshSurface.", "OK");
            return;
        }

        // how big
        float area = 0f;
        for (int i = 0; i + 2 < triangulation.indices.Length; i += 3)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[i]];
            Vector3 b = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[i + 2]];
            area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }

        var report = new StringBuilder();
        report.AppendLine("  NavMesh: " + triangulation.indices.Length / 3 + " triangles, " + area.ToString("F0") + " m2 of floor");

        // the places to check
        var places = new List<KeyValuePair<string, Transform>>();
        if (spawner.spawnPoints != null)
            foreach (var p in spawner.spawnPoints)
                if (p != null) places.Add(new KeyValuePair<string, Transform>(p.name, p));
        if (spawner.ghost != null) places.Add(new KeyValuePair<string, Transform>("the woman (her place now)", spawner.ghost));
        if (spawner.player != null) places.Add(new KeyValuePair<string, Transform>("the player's start", spawner.player));

        var off = new List<Transform>();
        foreach (var place in places)
        {
            if (NavMesh.SamplePosition(place.Value.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                float gap = Vector3.Distance(place.Value.position, hit.position);
                report.AppendLine("  OK   " + place.Key + " (" + gap.ToString("F2") + " m from the NavMesh)");
            }
            else
            {
                report.AppendLine("  !!   " + place.Key + " is NOT on the NavMesh (no walkable floor within 1.5 m)");
                off.Add(place.Value);
            }
        }

        // can she walk from every spawn point to the others and to the player's start?
        int broken = 0;
        var path = new NavMeshPath();
        for (int i = 0; i < places.Count; i++)
        {
            for (int j = 0; j < places.Count; j++)
            {
                if (i == j) continue;
                if (!NavMesh.SamplePosition(places[i].Value.position, out NavMeshHit from, 3f, NavMesh.AllAreas)) continue;
                if (!NavMesh.SamplePosition(places[j].Value.position, out NavMeshHit to, 3f, NavMesh.AllAreas)) continue;

                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    report.AppendLine("  !!   no complete path: " + places[i].Key + "  ->  " + places[j].Key + " (" + path.status + ")");
                    broken++;
                }
            }
        }
        if (broken == 0) report.AppendLine("  OK   every place can be walked to from every other place");

        Debug.Log("SolidNavMeshTool:\n" + report);

        string summary = off.Count + " place(s) off the NavMesh, " + broken + " broken path(s).";
        if (off.Count > 0 && EditorUtility.DisplayDialog("Solid ghost NavMesh", summary + "\n\nMove the places that are off the NavMesh onto the nearest walkable floor?", "Move them", "Leave them"))
        {
            foreach (var t in off)
            {
                if (!NavMesh.SamplePosition(t.position, out NavMeshHit near, 25f, NavMesh.AllAreas)) continue;
                Undo.RecordObject(t, "Move onto the NavMesh");
                t.position = near.position;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("SolidNavMeshTool: " + off.Count + " place(s) moved onto the NavMesh. Save the scene.");
        }
        else
        {
            EditorUtility.DisplayDialog("Solid ghost NavMesh", summary + "\n\nThe details are in the Console.", "OK");
        }
    }
}
