using UnityEngine;

// Add to the Baby Ghost. Makes it float smoothly up (+Y) and down (-Y) only.
// Works with GhostSpawner moving the ghost to new spawn points.
public class BabyGhostFloat : MonoBehaviour
{
    [Header("Float (Y axis only)")]
    [Tooltip("How far it moves up and down from its spawn height (meters)")]
    public float amplitude = 0.15f;

    [Tooltip("Speed of the up/down movement")]
    public float speed = 1.5f;

    [Tooltip("Random start so it doesn't always begin at the same height")]
    public bool randomStart = true;

    private float baseY;
    private float lastSetY;
    private float phase;

    void Start()
    {
        baseY = transform.position.y;
        lastSetY = baseY;
        phase = randomStart ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void LateUpdate()
    {
        Vector3 pos = transform.position;

        // Something else (GhostSpawner) moved us to a new spawn point: use its height as the new base
        if (!Mathf.Approximately(pos.y, lastSetY))
            baseY = pos.y;

        pos.y = baseY + Mathf.Sin(Time.time * speed + phase) * amplitude;
        lastSetY = pos.y;
        transform.position = pos;
    }
}
