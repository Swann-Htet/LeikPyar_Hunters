using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > Ghost > Check Hiding Places
// Finds every place the player can hide in (beds, wardrobes, cupboards: see HideSystem), lists them in the Console with their size
// and any problem (no floor under it, too small, too low), and SELECTS them all, so you can see in the Scene view which ones count.
public static class HidingPlacesTool
{
    [MenuItem("Tools/Ghost/Check Hiding Places")]
    public static void Run()
    {
        var hide = Object.FindFirstObjectByType<HideSystem>();
        var spots = HideSystem.Scan(null,
            hide != null ? hide.minCabinetHeight : 1.35f,
            hide != null ? hide.wardrobeHeight : 1.7f,
            hide != null ? hide.extraNames : new[] { "polySurface185", "polySurface186" },
            hide == null || hide.findWoodCupboards);

        Physics.SyncTransforms();

        var report = new StringBuilder();
        var selection = new List<Object>();
        int beds = 0, wardrobes = 0, cupboards = 0, problems = 0;

        foreach (var spot in spots)
        {
            var issues = new List<string>();
            Bounds b = spot.bounds;

            float floor = HideSystem.FloorBelow(new Vector3(b.center.x, b.min.y + 0.3f, b.center.z), float.NaN);
            if (float.IsNaN(floor)) issues.Add("no floor found under it");
            else if (b.min.y - floor > 1.2f) issues.Add("it floats " + (b.min.y - floor).ToString("F1") + " m above the floor");

            switch (spot.kind)
            {
                case HideSystem.Kind.Bed:
                    beds++;
                    if (Mathf.Min(b.size.x, b.size.z) < 0.7f || Mathf.Max(b.size.x, b.size.z) < 1.4f) issues.Add("small for a bed (" + b.size.x.ToString("F1") + " x " + b.size.z.ToString("F1") + ")");
                    if (b.size.y > 1.6f) issues.Add("very tall for a bed (" + b.size.y.ToString("F1") + " m): is it a bunk bed?");
                    break;
                case HideSystem.Kind.Wardrobe: wardrobes++; break;
                default: cupboards++; break;
            }

            if (spot.t != null && spot.t.GetComponentInChildren<Collider>() == null) issues.Add("has no collider");

            report.AppendLine("  " + spot.name + "  " + spot.kind + "  size " + b.size.x.ToString("F1") + " x " + b.size.y.ToString("F1") + " x " + b.size.z.ToString("F1") +
                              (issues.Count > 0 ? "   !! " + string.Join("; ", issues) : ""));
            if (issues.Count > 0) problems++;
            if (spot.t != null) selection.Add(spot.t.gameObject);
        }

        Selection.objects = selection.ToArray();

        string summary = spots.Count + " hiding place(s): " + beds + " bed(s), " + wardrobes + " wardrobe(s), " + cupboards + " cupboard(s). " + problems + " with a problem.";
        Debug.Log("HidingPlacesTool: " + summary + "\n" + report +
                  "They are all selected now: look at the Scene view. A wardrobe or cupboard that is missing: add its name to Extra Names of the Hide System, " +
                  "or check it is at least " + (hide != null ? hide.minCabinetHeight : 1.35f).ToString("F2") + " m tall.");
        EditorUtility.DisplayDialog("Hiding places", summary + "\n\nThe list is in the Console and the places are selected in the Scene view.", "OK");
    }
}
