using System.Collections.Generic;
using UnityEngine;

// Added by the Inventory to an item the player dropped (e.g. the Empty Faraday Cage).
// It is thrown out of the hand, falls with gravity, lands, and then stays where it is.
// Look at it: F puts the needed item inside (e.g. MEP) and gives you the filled item, E takes it back.
public class PlacedCage : MonoBehaviour
{
    public static readonly List<PlacedCage> All = new List<PlacedCage>();

    // The item this came from (holds the texts and what it needs)
    public InvItemData source;

    // True for the empty cage the NPC puts back on the Drop Table
    public bool onTable;

    // Physics of the drop (set by the Inventory)
    public Rigidbody body;
    [Tooltip("Where it is put back if it ever falls through the world")]
    public Vector3 safePoint;

    private float stillTime;
    private float aliveTime;

    // The grow animation: from the small held size to its real size while it flies
    private Vector3 startScale;
    private Vector3 targetScale;
    private float scaleTime = -1f;
    private float scaleDuration = 0.3f;

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    private float lastKnock = -10f;

    // The box falls on the floor (or on something): a knock, once for every hit that is hard enough
    void OnCollisionEnter(Collision collision)
    {
        if (onTable || Time.time - lastKnock < 0.3f) return;
        if (collision.relativeVelocity.magnitude < 0.9f) return;

        lastKnock = Time.time;
        Sfx.PlayAt("Box drop", transform.position, Mathf.Clamp01(0.35f + collision.relativeVelocity.magnitude * 0.15f));
    }

    public void GrowTo(Vector3 scale, float seconds)
    {
        startScale = transform.localScale;
        targetScale = scale;
        scaleDuration = Mathf.Max(0.05f, seconds);
        scaleTime = 0f;
    }

    void Update()
    {
        if (scaleTime >= 0f)
        {
            scaleTime += Time.deltaTime;
            float t = Mathf.Clamp01(scaleTime / scaleDuration);
            // "back" easing: it grows a little too big and settles, which feels springy
            float u = t - 1f;
            float eased = 1f + 2.7f * u * u * u + 1.7f * u * u;
            transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
            if (t >= 1f)
            {
                transform.localScale = targetScale;
                scaleTime = -1f;
            }
        }

        if (body == null || body.isKinematic) return;

        aliveTime += Time.deltaTime;

        if (body.linearVelocity.sqrMagnitude < 0.01f && body.angularVelocity.sqrMagnitude < 0.05f) stillTime += Time.deltaTime;
        else stillTime = 0f;

        // Fell through the world: back to the floor point
        if (transform.position.y < safePoint.y - 4f)
        {
            transform.position = safePoint + Vector3.up * 0.3f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // Landed (or rolled long enough): it stays there
        if (stillTime > 0.6f || aliveTime > 6f)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    // Where the player has to look
    public Vector3 Point
    {
        get
        {
            var r = GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : transform.position;
        }
    }
}
