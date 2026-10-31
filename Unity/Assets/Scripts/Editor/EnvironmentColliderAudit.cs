using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnvironmentColliderAudit
{
    [MenuItem("Tools/Camera/Find Renderers Without Colliders")]
    public static void FindRenderersWithoutColliders()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        var sb = new StringBuilder();
        int count = 0;

        foreach (var renderer in renderers)
        {
            if (renderer.GetComponent<Collider>() != null)
            {
                continue;
            }

            count++;
            sb.AppendLine(GetPath(renderer.transform));
        }

        if (count == 0)
        {
            Debug.Log("EnvironmentColliderAudit: every MeshRenderer in the scene has a Collider.");
            return;
        }

        Debug.LogWarning(
            $"EnvironmentColliderAudit: {count} object(s) have a MeshRenderer but no Collider. " +
            "If any of these are walls/floors/doors, the player can walk through them and see the " +
            "un-rendered backside of the mesh (looks like the room went transparent).\n" + sb);
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
