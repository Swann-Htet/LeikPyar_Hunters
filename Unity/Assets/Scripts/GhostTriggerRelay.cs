using UnityEngine;

// Added automatically by GhostSpawner to each trigger zone collider.
public class GhostTriggerRelay : MonoBehaviour
{
    public GhostSpawner spawner;
    public int pointIndex;

    void OnTriggerEnter(Collider other)
    {
        if (spawner != null && other.GetComponentInParent<PlayerController>() != null)
            spawner.OnPlayerEnteredZone(pointIndex);
    }
}
