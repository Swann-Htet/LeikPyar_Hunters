using UnityEngine;

// Put on an empty GameObject where a baby ghost should appear. Make as many as you like,
// then add them to the "Baby Spawners" list on the Ghost Manager (GhostSpawner).
// It creates a copy of the baby template inside this object.
public class BabyGhostSpawner : MonoBehaviour
{
    [Tooltip("A baby ghost to copy. Drag one from the Hierarchy (e.g. 'Bby Ghost')")]
    public GameObject babyTemplate;

    [Tooltip("Where the baby appears, relative to this object")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Adds the up/down float to the copy if the template doesn't have it")]
    public bool addFloat = true;

    private Transform baby;

    void Awake()
    {
        EnsureBaby();
    }

    // Creates the baby once and returns it (null if there is no template)
    public Transform EnsureBaby()
    {
        if (baby != null) return baby;

        // A baby placed inside this object by hand already counts
        foreach (Transform child in transform)
        {
            if (BabyGhostFinder.IsGhostTag(child.gameObject.tag))
            {
                baby = child;
                return baby;
            }
        }

        if (babyTemplate == null)
        {
            Debug.LogWarning("[BabyGhostSpawner] '" + name + "' has no Baby Template assigned.", this);
            return null;
        }

        var copy = Instantiate(babyTemplate, transform.TransformPoint(offset), babyTemplate.transform.rotation, transform);
        copy.name = "Bby Ghost";
        copy.SetActive(true);

        // A template that already has Orb / Mirage / Solid keeps its type
        if (!BabyGhostFinder.IsGhostTag(copy.tag))
        {
            try { copy.tag = BabyGhostFinder.Tag; }
            catch (UnityException) { Debug.LogWarning("[BabyGhostSpawner] The tag '" + BabyGhostFinder.Tag + "' does not exist yet. Add it in Tag > Add Tag.", this); }
        }

        if (addFloat && copy.GetComponent<BabyGhostFloat>() == null)
            copy.AddComponent<BabyGhostFloat>();

        baby = copy.transform;
        return baby;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.9f);
        Vector3 p = transform.TransformPoint(offset);
        Gizmos.DrawWireSphere(p, 0.3f);
        Gizmos.DrawLine(p, p + Vector3.up * 0.8f);
    }
}
