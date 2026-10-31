using UnityEngine;

// Added automatically by GhostGun to every catchable ghost (Orb / Mirage / Solid / bghost).
// Remembers how many times this ghost still breaks free from the gun, and runs the escape:
// it is stunned for a moment, then darts around the player (never far away) until it is caught again.
public class CatchableGhost : MonoBehaviour
{
    public enum Mode { Idle, Stunned, Fleeing }

    [Header("Escape dice (filled in by GhostGun)")]
    [Tooltip("True once the dice was rolled for this ghost")]
    public bool rolled;
    [Tooltip("How many more times this ghost will break free from the gun")]
    public int escapesLeft;

    public Mode mode { get; private set; }
    public bool IsEscaping => mode != Mode.Idle;

    // The Unity tag decides the type: Orb, Mirage, Solid (anything else = the old 'bghost' tag)
    public string TypeTag => gameObject.tag;

    private float stunEnd;
    private float fleeEnd;
    private float fleeDuration;
    private float fleeSpeed;
    private Transform center;
    private float minDistance;
    private float maxDistance;

    private bool hasTarget;
    private Vector3 target;
    private BabyGhostFloat floating;

    // The ghost's normal height above the floor, remembered the first time it is caught,
    // so it settles back to it after being carried (looking up must not leave it in the air)
    private float restHeight;
    private bool hasRest;
    private bool hasGround;
    private const float DescendSpeed = 2.5f;

    // Height of the first solid surface below 'from' (ignoring the given objects)
    public static bool TryGetGround(Vector3 from, Transform ignoreA, Transform ignoreB, out float groundY)
    {
        groundY = 0f;
        bool found = false;
        float best = float.MaxValue;

        var hits = Physics.RaycastAll(from, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (ignoreA != null && h.transform.IsChildOf(ignoreA)) continue;
            if (ignoreB != null && h.transform.IsChildOf(ignoreB)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                groundY = h.point.y;
                found = true;
            }
        }
        return found;
    }

    // Called by the gun right before the ghost is carried (while it still hovers at its normal height)
    public void RememberRestHeight()
    {
        if (hasRest) return;
        hasRest = true;

        Transform player = GhostGun.Instance != null ? GhostGun.Instance.PlayerRoot : null;
        float ground;
        hasGround = TryGetGround(transform.position + Vector3.up * 0.3f, transform, player, out ground);
        restHeight = hasGround ? Mathf.Max(0f, transform.position.y - ground) : 0f;
    }

    // While escaping, the ghost sinks back to its normal hover height above the floor beneath it
    void UpdateHeight()
    {
        if (!hasGround) return;

        Transform player = GhostGun.Instance != null ? GhostGun.Instance.PlayerRoot : null;
        float ground;
        if (!TryGetGround(transform.position + Vector3.up * 0.3f, transform, player, out ground)) return;

        float bob = floating != null ? Mathf.Sin(Time.time * floating.speed) * floating.amplitude : 0f;
        float targetY = ground + restHeight + bob;

        Vector3 p = transform.position;
        p.y = Mathf.MoveTowards(p.y, targetY, DescendSpeed * Time.deltaTime);
        transform.position = p;
    }

    // Rolls the dice once (the first time the ghost is caught)
    public void EnsureRolled(GhostGun gun)
    {
        if (rolled) return;
        escapesLeft = gun.RollEscapes(this);
        rolled = true;
    }

    // Uses up one escape. False if the ghost has none left
    public bool ConsumeEscape()
    {
        if (escapesLeft <= 0) return false;
        escapesLeft--;
        return true;
    }

    // Stun for 'stunSeconds', then dart around 'around' (the player) between minDist and maxDist meters.
    // fleeSeconds <= 0 = keeps going until caught again
    public void Escape(float stunSeconds, float fleeSeconds, float speed, Transform around, float minDist, float maxDist)
    {
        floating = GetComponent<BabyGhostFloat>();

        fleeDuration = fleeSeconds;
        fleeSpeed = speed;
        center = around;
        minDistance = Mathf.Max(0.5f, Mathf.Min(minDist, maxDist));
        maxDistance = Mathf.Max(minDistance + 0.5f, Mathf.Max(minDist, maxDist));
        hasTarget = false;

        if (stunSeconds > 0f)
        {
            mode = Mode.Stunned;
            stunEnd = Time.time + stunSeconds;
            if (floating != null) floating.enabled = false;
        }
        else
        {
            BeginFlee();
        }
    }

    public void StopEscape()
    {
        mode = Mode.Idle;
        hasTarget = false;
        if (floating == null) floating = GetComponent<BabyGhostFloat>();
        if (floating != null) floating.enabled = true;
    }

    // The ghost was taken by the generator: next time it starts fresh
    public void ResetCatch()
    {
        rolled = false;
        escapesLeft = 0;
        StopEscape();
    }

    void BeginFlee()
    {
        mode = Mode.Fleeing;
        fleeEnd = fleeDuration > 0f ? Time.time + fleeDuration : float.MaxValue;
        hasTarget = false;
        if (floating != null) floating.enabled = true;
    }

    void Update()
    {
        if (mode == Mode.Idle) return;

        if (mode == Mode.Stunned)
        {
            UpdateHeight();
            if (Time.time >= stunEnd) BeginFlee();
            return;
        }

        if (Time.time >= fleeEnd)
        {
            StopEscape();
            return;
        }

        UpdateHeight();

        if (center == null) return;

        // Pick a new spot around the player when needed
        if (!hasTarget || ReachedTarget() || TargetTooFarFromPlayer() || PathBlocked(target))
            PickTarget();

        if (!hasTarget) return;

        // Only x/z change: the up/down float keeps its own height
        Vector3 to = target - transform.position;
        to.y = 0f;
        float step = fleeSpeed * Time.deltaTime;
        if (to.magnitude <= step)
            transform.position += to;
        else
            transform.position += to.normalized * step;
    }

    bool ReachedTarget()
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        return to.sqrMagnitude < 0.04f;
    }

    bool TargetTooFarFromPlayer()
    {
        Vector3 to = target - center.position;
        to.y = 0f;
        return to.magnitude > maxDistance * 1.3f;
    }

    // A random spot on a ring around the player that can be reached in a straight line
    void PickTarget()
    {
        hasTarget = false;

        for (int i = 0; i < 12; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(minDistance, maxDistance);
            Vector3 candidate = center.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            candidate.y = transform.position.y;

            if (PathBlocked(candidate)) continue;

            target = candidate;
            hasTarget = true;
            return;
        }
    }

    // Something solid (wall, furniture, door) between the ghost and that spot?
    bool PathBlocked(Vector3 point)
    {
        Vector3 from = transform.position + Vector3.up * 0.4f;
        Vector3 to = new Vector3(point.x, from.y, point.z);
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;

        var hits = Physics.RaycastAll(from, dir / dist, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (center != null && h.transform.IsChildOf(center)) continue;
            return true;
        }
        return false;
    }
}
