using System.Collections.Generic;
using UnityEngine;

// Added to every capsule the generator produces. The player takes it with the Inventory (the generator also gives it an InvItem).
public class GhostCapsule : MonoBehaviour
{
    public static readonly List<GhostCapsule> All = new List<GhostCapsule>();

    [Tooltip("Degrees per second the capsule spins so it is easy to see (0 = no spin)")]
    public float spinSpeed = 60f;

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (spinSpeed != 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
}
