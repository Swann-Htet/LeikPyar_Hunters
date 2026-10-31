using System.Collections.Generic;
using UnityEngine;

// Finds every baby ghost in the scene: objects with the tag "bghost" that have a model
// (inactive ones too).
public static class BabyGhostFinder
{
    public const string Tag = "bghost";

    // The ghost types. Tag a ghost with one of these (or the old "bghost") to make it catchable
    public static readonly string[] TypeTags = { "Orb", "Mirage", "Solid" };

    public static bool IsGhostTag(string tag)
    {
        if (tag == Tag) return true;
        foreach (var t in TypeTags)
            if (tag == t) return true;
        return false;
    }

    public static List<Transform> Find(Transform extra = null)  // extra: a baby to include even if it isn't tagged
    {
        var candidates = new List<Transform>();
        if (extra != null) candidates.Add(extra);

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsGhostTag(t.gameObject.tag) || candidates.Contains(t)) continue;

            // A spawner or an empty object is not a baby
            if (t.GetComponent<BabyGhostSpawner>() != null) continue;
            if (t.GetComponentsInChildren<Renderer>(true).Length == 0) continue;

            candidates.Add(t);
        }

        candidates.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // Skip parts of a baby (e.g. a tagged child inside a tagged baby)
        var result = new List<Transform>();
        foreach (var t in candidates)
        {
            bool isPart = false;
            foreach (var other in candidates)
            {
                if (other != t && t.IsChildOf(other)) { isPart = true; break; }
            }
            if (!isPart) result.Add(t);
        }
        return result;
    }
}
