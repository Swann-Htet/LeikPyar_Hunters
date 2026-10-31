using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Tools > Ghost > Move Ghost Spawns To GCC_house (1) And Remove house_GM
//  1. the woman ghost's spawn points (Spwan Point 1 - 5, the ones in the GhostSpawner) were in the old house: they are
//     moved to walkable places (NavMesh) spread through GCC_house (1), away from the player's start
//     (the trigger zone of every point is its child, so it moves with it)
//  2. the old house "house_GM" is deleted (Ctrl+Z brings it back)
// Nothing is saved by the tool: look at the result, then save the scene.
public static class GhostHouseTool
{
    const string NewHouse = "GCC_house (1)";
    const string OldHouse = "house_GM";

    [MenuItem("Tools/Ghost/Move Ghost Spawns To GCC_house (1) And Remove house_GM")]
    public static void Run()
    {
        var newHouse = FindRoot(NewHouse);
        var oldHouse = FindRoot(OldHouse);
        var spawner = Object.FindFirstObjectByType<GhostSpawner>();

        if (newHouse == null || spawner == null || spawner.spawnPoints == null || spawner.spawnPoints.Length == 0)
        {
            EditorUtility.DisplayDialog("Ghost house",
                "Could not find " + (newHouse == null ? "'" + NewHouse + "'" : "the GhostSpawner with its Spawn Points") +
                " in the open scene.\nOpen SampleScene and try again.", "OK");
            return;
        }

        // the floor of the new house: NavMesh triangles that lie inside it
        Bounds box = HouseBounds(newHouse);
        var triangulation = NavMesh.CalculateTriangulation();
        var candidates = new List<Vector3>();

        for (int i = 0; i + 2 < triangulation.indices.Length; i += 3)
        {
            Vector3 a = triangulation.vertices[triangulation.indices[i]];
            Vector3 b = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[i + 2]];
            Vector3 center = (a + b + c) / 3f;

            // area: tiny slivers are bad places to stand
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            if (area < 0.25f) continue;
            if (box.Contains(center + Vector3.up * 0.5f)) candidates.Add(center);
        }

        if (candidates.Count < spawner.spawnPoints.Length)
        {
            EditorUtility.DisplayDialog("Ghost house",
                "There is no (or too little) NavMesh inside " + NewHouse + " (" + candidates.Count + " places found).\n" +
                "Select " + NewHouse + " and press Bake in its NavMeshSurface, then run this again.", "OK");
            return;
        }

        Vector3 player = spawner.player != null ? spawner.player.position : box.center;
        var chosen = Pick(candidates, spawner.spawnPoints.Length, player, 8f);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var report = new StringBuilder();

        for (int i = 0; i < spawner.spawnPoints.Length; i++)
        {
            var point = spawner.spawnPoints[i];
            if (point == null) continue;

            Vector3 where = chosen[i];
            if (NavMesh.SamplePosition(where, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) where = hit.position;

            report.AppendLine("  " + point.name + ": " + Format(point.position) + "  ->  " + Format(where));
            Undo.RecordObject(point, "Move spawn point");
            point.position = where;
        }

        // things that stand near the old house (to look at yourself: they may belong to it)
        var near = new StringBuilder();
        if (oldHouse != null)
        {
            foreach (var root in oldHouse.scene.GetRootGameObjects())
            {
                if (root == oldHouse || root == newHouse) continue;
                if (Vector3.Distance(root.transform.position, oldHouse.transform.position) < 70f)
                    near.AppendLine("  " + root.name + "  " + Format(root.transform.position));
            }

            Undo.DestroyObjectImmediate(oldHouse);
        }

        Undo.SetCurrentGroupName("Ghost spawns to GCC_house (1), remove house_GM");
        Undo.CollapseUndoOperations(group);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string message = "Woman ghost spawn points moved into " + NewHouse + ":\n" + report +
                         (oldHouse != null ? OldHouse + " deleted.\n" : OldHouse + " was not in the scene.\n") +
                         (near.Length > 0 ? "\nObjects that stood close to the old house (check they are where you want them):\n" + near : "") +
                         "\nSave the scene when it looks right (Ctrl+Z undoes all of it).";
        Debug.Log("GhostHouseTool:\n" + message);
        EditorUtility.DisplayDialog("Ghost house", "Done. The list is in the Console.", "OK");
    }

    static GameObject FindRoot(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    static Bounds HouseBounds(GameObject house)
    {
        bool any = false;
        Bounds box = new Bounds(house.transform.position, Vector3.zero);
        foreach (var r in house.GetComponentsInChildren<Renderer>())
        {
            if (!any) { box = r.bounds; any = true; }
            else box.Encapsulate(r.bounds);
        }
        return box;
    }

    // the spread-out places: each new one is as far as possible from the ones taken (and not next to the player)
    static List<Vector3> Pick(List<Vector3> candidates, int count, Vector3 player, float minFromPlayer)
    {
        var far = new List<Vector3>();
        foreach (var c in candidates)
            if (Vector3.Distance(c, player) >= minFromPlayer) far.Add(c);
        if (far.Count < count) far = candidates;

        var chosen = new List<Vector3>();

        // the first one: the place furthest from the player
        Vector3 first = far[0];
        float best = -1f;
        foreach (var c in far)
        {
            float d = Vector3.Distance(c, player);
            if (d > best) { best = d; first = c; }
        }
        chosen.Add(first);

        while (chosen.Count < count)
        {
            Vector3 next = far[0];
            float bestMin = -1f;
            foreach (var c in far)
            {
                float min = float.MaxValue;
                foreach (var p in chosen) min = Mathf.Min(min, Vector3.Distance(c, p));
                if (min > bestMin) { bestMin = min; next = c; }
            }
            chosen.Add(next);
        }
        return chosen;
    }

    static string Format(Vector3 v)
    {
        return "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";
    }
}
