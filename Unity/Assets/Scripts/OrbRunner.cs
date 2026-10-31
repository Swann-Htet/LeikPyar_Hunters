using UnityEngine;
using UnityEngine.AI;

// An Orb that runs away. While the gun is hot (its heat is above "Orb Runs Above Heat" on the GhostGun, or it is cooling down) an Orb near the
// player runs to the opposite side: it picks the farthest spot it can reach on the far side from the player and goes there over the NavMesh.
// While it runs the gun can not aim at it. It is added to every Orb by the GhostSpawner.
public class OrbRunner : MonoBehaviour
{
    [Tooltip("Meters per second while it runs")]
    public float runSpeed = 3.6f;
    [Tooltip("It only runs when the player is closer than this (meters)")]
    public float fleeDistance = 14f;
    [Tooltip("How far it goes at a time (meters)")]
    public float runLength = 10f;

    private float hover = 0.5f;
    private bool hoverRead;
    private float lastMoveTime = -10f;
    private NavMeshPath path;
    private Vector3[] corners;
    private int cornerIndex;

    // True while it is running (the gun can not aim at it then)
    public bool IsRunning => Time.time - lastMoveTime < 0.35f;

    void Update()
    {
        if (!IsShown()) { hoverRead = false; corners = null; return; }

        var gun = GhostGun.Instance;
        if (gun == null || gun.PlayerRoot == null) return;

        // the one the gun holds does not run
        if (gun.LeashedBaby == transform) { corners = null; return; }

        bool hot = gun.IsCoolingDown || gun.HeatPercent >= gun.orbRunsAboveHeat;
        if (!hot) { corners = null; return; }

        Vector3 player = gun.PlayerRoot.position;
        Vector3 flat = transform.position - player;
        flat.y = 0f;
        if (flat.magnitude > fleeDistance) { corners = null; return; }

        if (!hoverRead) ReadHover();

        if (corners == null || cornerIndex >= corners.Length) PickRun(player, flat);
        if (corners == null) return;

        // along the path
        Vector3 target = corners[cornerIndex] + Vector3.up * hover;
        transform.position = Vector3.MoveTowards(transform.position, target, runSpeed * Time.deltaTime);
        lastMoveTime = Time.time;

        if ((transform.position - target).sqrMagnitude < 0.04f) cornerIndex++;
    }

    bool IsShown()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
        return false;
    }

    void ReadHover()
    {
        hoverRead = true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            hover = Mathf.Clamp(transform.position.y - hit.position.y, 0.1f, 3f);
    }

    // The spot on the far side of the player: straight away from him, or a little to the side if a wall is there
    void PickRun(Vector3 player, Vector3 away)
    {
        corners = null;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit here, 3f, NavMesh.AllAreas)) return;

        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
        away.y = 0f;
        away.Normalize();

        if (path == null) path = new NavMeshPath();

        float bestScore = float.MinValue;
        Vector3[] bestCorners = null;
        float[] turns = { 0f, 25f, -25f, 50f, -50f, 80f, -80f };
        float[] lengths = { 1f, 0.7f, 0.45f };

        foreach (float length in lengths)
        {
            foreach (float turn in turns)
            {
                Vector3 dir = Quaternion.Euler(0f, turn, 0f) * away;
                if (!NavMesh.SamplePosition(here.position + dir * runLength * length, out NavMeshHit there, 2f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(here.position, there.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) continue;

                // far from the player, and in his opposite direction
                Vector3 offset = there.position - player;
                offset.y = 0f;
                float score = offset.magnitude + Vector3.Dot(offset.normalized, away) * 4f - Mathf.Abs(turn) * 0.02f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCorners = (Vector3[])path.corners.Clone();
                }
            }
            if (bestCorners != null) break;
        }

        if (bestCorners == null) return;
        corners = bestCorners;
        cornerIndex = 1;
    }
}
