using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

// The NPC at the Drop Table (the man in the white protective suit).
//  - It only shows up while the table is open (7:00 AM - 4:00 PM on the game clock), like the point exchange time.
//  - It stands at its place near the door and does not walk ("Stays In Place"): it plays the idle animation.
//  - When the player puts a Faraday Cage on the table it turns to the table and plays the SCAN animation until every cage is
//    checked, then it turns back and idles again.
// (With "Stays In Place" off it walks about and goes to the "NPC Stand Check" point like before.)
// Set it up with Tools > Ghost > Use The Real NPC. It plays the clips with Playables, so no Animator Controller is needed.
public class NpcWorker : MonoBehaviour
{
    private enum State { Hidden, Idle, Look, Walk, GoToDesk, Work }
    private enum Clip { Idle = 0, Look = 1, Walk = 2, Push = 3 }

    [Header("Animation clips")]
    public AnimationClip idleClip;
    public AnimationClip lookAroundClip;
    public AnimationClip walkClip;
    public AnimationClip pushButtonClip;
    [Tooltip("Seconds to blend from one animation into the next")]
    public float blendTime = 0.3f;
    [Tooltip("How many seconds one round of the CHECK animation takes (the file plays in 3.9 s at 30 frames a second; made at 24 frames it is 4.9 s = 5). 0 = as the file plays")]
    public float checkAnimationSeconds = 5f;
    [Tooltip("When the check is over, the NPC finishes the round of the animation first (no cut in the middle). The most seconds it waits")]
    public float finishCheckRound = 6f;

    [Header("The check spot")]
    [Tooltip("The 'NPC Stand Check' object")]
    public Transform standPoint;
    [Tooltip("Rotation at the stand point, relative to it (0, 180, 0)")]
    public Vector3 standLocalEuler = new Vector3(0f, 180f, 0f);
    [Tooltip("Position at the stand point, relative to it (0, 0, 0)")]
    public Vector3 standLocalPosition = Vector3.zero;

    [Header("Movement")]
    [Tooltip("On: the NPC never walks. It stands at its place, turns to the table and plays the check animation there")]
    public bool staysInPlace = true;
    [Tooltip("How fast it turns (degrees per second)")]
    public float turnSpeed = 220f;

    [Header("Where it normally stands")]
    [Tooltip("On: it appears here, wanders from here and walks back here. Off: its place in the scene")]
    public bool useHome = true;
    public Vector3 homePosition = new Vector3(162.29f, 0.4098f, -18.43f);
    public Vector3 homeEuler = new Vector3(0f, 238.72f, 0f);

    [Header("Beside the table")]
    [Tooltip("The NPC stands right next to the Drop Table, at the end of the table that is nearest to Home Position. Off = she stands at Home Position")]
    public bool besideTable = true;
    [Tooltip("Free space between the table and her (meters)")]
    public float besideGap = 0.6f;

    [Header("Working hours")]
    [Tooltip("Only there while a Drop Table is open")]
    public bool onlyDuringOpenHours = true;

    [Header("Idle / walking")]
    public float idleMin = 6f;
    public float idleMax = 14f;
    [Range(0f, 1f)] public float walkChance = 0.3f;
    [Range(0f, 1f)] public float lookChance = 0.12f;
    [Tooltip("How far from its home it may walk (meters). Big = the whole first floor")]
    public float wanderRadius = 40f;
    [Tooltip("It only walks where the floor is at its own height (+/- this, meters): not onto chairs, sofas, tables or stairs")]
    public float maxHeightDelta = 0.3f;
    [Tooltip("Walking speed at size 1. It is multiplied by the NPC's scale")]
    public float walkSpeed = 1.4f;

    private bool finishing;
    private float finishWait;
    private float lastPushTime;

    private Animator animator;
    private NavMeshAgent agent;
    private Renderer[] renderers;
    private Collider[] colliders;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private readonly AnimationClipPlayable[] players = new AnimationClipPlayable[4];
    private readonly AnimationClip[] clips = new AnimationClip[4];
    private readonly float[] weights = new float[4];
    private readonly float[] times = new float[4];
    private Clip current = Clip.Idle;
    private bool graphReady;

    private State state = State.Hidden;
    private float timer;
    private float walkTimeout;
    private Vector3 home;
    private Quaternion homeRotation;
    private Vector3 manualTarget;
    private float scaleY = 1f;

    public static NpcWorker Instance { get; private set; }

    // Move the NPC object where it should stand (near the door), then use this: right-click "NpcWorker" in the Inspector >
    // "Use This Place As Home". (The NPC appears at Home Position, not where the object sits in the scene.)
    [ContextMenu("Use This Place As Home")]
    void UseThisPlaceAsHome()
    {
        homePosition = transform.position;
        homeEuler = transform.eulerAngles;
        useHome = true;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[NPC] Home is now " + homePosition + ", turned " + homeEuler.y.ToString("0") + " degrees");
    }

    // Home is set right next to the Drop Table: at the end of the table that is nearest to the old home, on the floor, turned to the table
    void PlaceBesideTable()
    {
        var table = FindFirstObjectByType<DropTable>();
        if (table == null) return;

        Bounds b = table.TableBounds;
        bool alongX = b.extents.x >= b.extents.z;
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        float half = alongX ? b.extents.x : b.extents.z;

        Vector3 a = b.center + axis * (half + besideGap);
        Vector3 z = b.center - axis * (half + besideGap);
        Vector3 pick = (a - homePosition).sqrMagnitude <= (z - homePosition).sqrMagnitude ? a : z;

        // the floor: the lowest thing under the spot
        float floor = homePosition.y;
        var hits = Physics.RaycastAll(new Vector3(pick.x, b.max.y + 1f, pick.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(table.transform) || hit.collider.transform.IsChildOf(transform)) continue;
            if (!found || hit.point.y < floor) { floor = hit.point.y; found = true; }
        }
        pick.y = floor;

        Vector3 look = b.center - pick;
        look.y = 0f;
        homePosition = pick;
        if (look.sqrMagnitude > 0.01f) homeEuler = new Vector3(0f, Quaternion.LookRotation(look).eulerAngles.y, 0f);
        Debug.Log("[NPC] She stands next to the table at " + pick + " (Beside Table: on)");
    }

    // The training puts the NPC in its room: where she lives, how far she may walk, and where she stands to check a cage
    public void SetHome(Vector3 position, float yaw, float wander, Vector3 stand, Vector3 lookAt)
    {
        home = position;
        homeRotation = Quaternion.Euler(0f, yaw, 0f);
        homePosition = position;
        useHome = true;
        wanderRadius = wander;
        walkChance = 0f;      // the training NPC stays where she is: she does not walk about

        if (standPoint != null)
        {
            standPoint.position = stand;
            Vector3 flat = lookAt - stand;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                standPoint.rotation = Quaternion.LookRotation(flat) * Quaternion.Inverse(Quaternion.Euler(standLocalEuler));
        }

        PlaceOnNavMesh(position);
    }

    // The training: while she stands there (not working) she turns to the player
    public void FaceTowards(Vector3 point)
    {
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) return;

        homeRotation = Quaternion.LookRotation(flat);
        if (state == State.Idle || state == State.Look)
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation, 0.15f);
    }

    // True while the NPC is in the house (the Drop Table is open: 7 AM - 4 PM)
    public bool IsPresent { get { return state != State.Hidden; } }

    void Awake()
    {
        Instance = this;
        // the Animator sits on the character model (a child) or on this object
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        agent = GetComponent<NavMeshAgent>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        scaleY = Mathf.Max(0.01f, transform.lossyScale.y);
    }

    void Start()
    {
        // The NPC may sit inside the stand point object in the scene: it must be free to walk away from it
        if (standPoint != null && transform.IsChildOf(standPoint))
            transform.SetParent(null, true);

        scaleY = Mathf.Max(0.01f, transform.lossyScale.y);
        if (besideTable && useHome && !TutorialMode.Active) PlaceBesideTable();
        home = useHome ? homePosition : transform.position;
        homeRotation = useHome ? Quaternion.Euler(homeEuler) : transform.rotation;

        if (agent != null)
        {
            agent.speed = walkSpeed * scaleY;
            agent.acceleration = agent.speed * 3f;
            agent.angularSpeed = 300f;
            agent.updateRotation = true;
        }

        if (animator != null) animator.applyRootMotion = false;
        BuildGraph();

        SetVisible(false);
        state = State.Hidden;
    }

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    // ===================== MAIN LOOP =====================

    void Update()
    {
        if (!graphReady) return;

        float dt = Time.deltaTime;
        bool open = !onlyDuringOpenHours || DropTable.AnyOpen;

        if (state == State.Hidden && open) Appear();
        else if (state != State.Hidden && !open) Disappear();

        if (state != State.Hidden) Think(dt);

        UpdateAnimation(dt);
    }

    void Think(float dt)
    {
        bool wantsWork = standPoint != null && DropTable.AnyChecking && (!onlyDuringOpenHours || DropTable.AnyOpen);

        switch (state)
        {
            case State.Idle:
                if (wantsWork) { GoToDesk(); break; }
                TurnToHome(dt);
                timer -= dt;
                if (timer <= 0f) PickNext();
                break;

            case State.Look:
                if (wantsWork) { GoToDesk(); break; }
                if (ClipFinished(Clip.Look)) StartIdle();
                break;

            case State.Walk:
                if (wantsWork) { GoToDesk(); break; }
                walkTimeout -= dt;
                if (Arrived() || walkTimeout <= 0f) StartIdle();
                break;

            case State.GoToDesk:
                if (!wantsWork) { StartIdle(); break; }
                if (MoveToDesk(dt)) BeginWork();
                break;

            case State.Work:
                if (!wantsWork)
                {
                    // the check is over: the round of the animation is finished first, then she goes back to idle
                    if (finishCheckRound > 0f && clips[(int)Clip.Push] != null)
                    {
                        if (!finishing) { finishing = true; finishWait = finishCheckRound; lastPushTime = times[(int)Clip.Push]; }
                        finishWait -= dt;
                        bool wrapped = times[(int)Clip.Push] < lastPushTime;        // the round ended: the clip started again
                        lastPushTime = times[(int)Clip.Push];
                        if (staysInPlace) FaceTable(dt);
                        if (!wrapped && finishWait > 0f) break;
                    }
                    finishing = false;
                    EndWork();
                    break;
                }
                finishing = false;
                if (staysInPlace) FaceTable(dt);
                else AlignToStand(dt);
                break;
        }
    }

    // ===================== STATES =====================

    void Appear()
    {
        SetVisible(true);

        PlaceOnNavMesh(home);
        transform.rotation = homeRotation;

        StartIdle();
    }

    void Disappear()
    {
        StopAgent();
        if (agent != null) agent.enabled = false;
        SetVisible(false);
        state = State.Hidden;
    }

    void StartIdle()
    {
        state = State.Idle;
        timer = Random.Range(idleMin, Mathf.Max(idleMin, idleMax));
        StopAgent();
        Play(Clip.Idle);
    }

    void StartLook()
    {
        state = State.Look;
        StopAgent();
        Play(Clip.Look);
    }

    void StartWalk(Vector3 target)
    {
        state = State.Walk;
        walkTimeout = 25f;
        agent.isStopped = false;
        agent.SetDestination(target);
        Play(Clip.Walk);
    }

    void PickNext()
    {
        float r = Random.value;

        if (!staysInPlace && r < walkChance && TryRandomPoint(out Vector3 point))
            StartWalk(point);
        else if (r < (staysInPlace ? 0f : walkChance) + lookChance && lookAroundClip != null)
            StartLook();
        else
            StartIdle();   // stays idle longer: most of the time it just stands
    }

    void GoToDesk()
    {
        // it does not walk: the check is done where it stands
        if (staysInPlace)
        {
            BeginWork();
            return;
        }

        state = State.GoToDesk;
        Play(Clip.Walk);

        if (UseAgent)
        {
            agent.isStopped = false;
            if (NavMesh.SamplePosition(StandPosition(), out NavMeshHit hit, 3f * scaleY, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(StandPosition());
        }
        else
        {
            manualTarget = StandPosition();
        }
    }

    // True when the NPC arrived at the stand point
    bool MoveToDesk(float dt)
    {
        if (UseAgent)
        {
            bool close = (transform.position - StandPosition()).sqrMagnitude < (0.8f * scaleY) * (0.8f * scaleY);
            return Arrived() || close;
        }

        // No NavMesh under the NPC: walk in a straight line
        Vector3 to = manualTarget - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.05f) return true;

        transform.position = Vector3.MoveTowards(transform.position, new Vector3(manualTarget.x, transform.position.y, manualTarget.z), agentSpeed * dt);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 300f * dt);
        return false;
    }

    void BeginWork()
    {
        state = State.Work;
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;   // the exact spot and rotation are set by hand now
        }
        Play(Clip.Push);
    }

    // Slides to the exact stand position and turns to the stand rotation
    void AlignToStand(float dt)
    {
        transform.position = Vector3.MoveTowards(transform.position, StandPosition(), agentSpeed * dt);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, StandRotation(), 360f * dt);
    }

    // It turns to the table (at its stand point, or the way the stand point looks when it is standing there)
    void FaceTable(float dt)
    {
        Quaternion want = transform.rotation;
        if (standPoint != null)
        {
            Vector3 flat = standPoint.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.64f) want = Quaternion.LookRotation(flat);
            else want = StandRotation();
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
    }

    // At rest it turns back to the way it stands
    void TurnToHome(float dt)
    {
        if (!staysInPlace) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, turnSpeed * dt);
    }

    // The check is done: it walks back to its normal place
    void EndWork()
    {
        if (staysInPlace)
        {
            StartIdle();
            return;
        }

        PlaceOnNavMesh(transform.position);

        if (UseAgent && NavMesh.SamplePosition(home, out NavMeshHit hit, 3f * scaleY, NavMesh.AllAreas))
            StartWalk(hit.position);
        else
            StartIdle();
    }

    // ===================== HELPERS =====================

    float agentSpeed => walkSpeed * scaleY;

    bool UseAgent => agent != null && agent.enabled && agent.isOnNavMesh;

    Vector3 StandPosition()
    {
        return standPoint.TransformPoint(standLocalPosition);
    }

    Quaternion StandRotation()
    {
        return standPoint.rotation * Quaternion.Euler(standLocalEuler);
    }

    bool Arrived()
    {
        if (!UseAgent) return true;
        if (agent.pathPending) return false;
        if (agent.remainingDistance > agent.stoppingDistance + 0.15f) return false;
        return !agent.hasPath || agent.velocity.sqrMagnitude < 0.01f;
    }

    void StopAgent()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    // Puts the NPC on the NavMesh near a position (or just there if there is no NavMesh)
    void PlaceOnNavMesh(Vector3 position)
    {
        if (agent != null)
        {
            // (the agent is only switched on where there is a NavMesh: off the NavMesh it logs an error)
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f * scaleY, NavMesh.AllAreas))
            {
                if (!agent.enabled) agent.enabled = true;
                agent.Warp(hit.position);
                return;
            }
            if (agent.enabled) agent.enabled = false;
        }

        transform.position = position;
    }

    bool TryRandomPoint(out Vector3 point)
    {
        point = transform.position;
        if (!UseAgent) return false;

        for (int i = 0; i < 20; i++)
        {
            Vector3 candidate = home + Random.insideUnitSphere * wanderRadius;
            candidate.y = home.y;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f * scaleY, NavMesh.AllAreas)) continue;
            if (!OnFloor(hit.position)) continue;                                  // a chair, a sofa, a table, upstairs...
            if ((hit.position - transform.position).magnitude < 2f) continue;

            var path = new NavMeshPath();
            if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete && PathStaysOnFloor(path))
            {
                point = hit.position;
                return true;
            }
        }
        return false;
    }

    bool OnFloor(Vector3 position)
    {
        return Mathf.Abs(position.y - home.y) <= maxHeightDelta;
    }

    // The whole way stays on the floor level: it never climbs over furniture or the stairs
    bool PathStaysOnFloor(NavMeshPath path)
    {
        Vector3[] corners = path.corners;
        foreach (var c in corners)
            if (!OnFloor(c)) return false;

        float step = Mathf.Max(0.3f, 0.4f * scaleY);
        for (int i = 0; i < corners.Length - 1; i++)
        {
            int count = Mathf.CeilToInt(Vector3.Distance(corners[i], corners[i + 1]) / step);
            for (int k = 1; k < count; k++)
            {
                Vector3 p = Vector3.Lerp(corners[i], corners[i + 1], k / (float)count);
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f * scaleY, NavMesh.AllAreas) && !OnFloor(hit.position))
                    return false;
            }
        }
        return true;
    }

    private bool trainingHidden;

    // The training shows the NPC only in the lessons that need her
    public void SetTrainingHidden(bool hide)
    {
        trainingHidden = hide;
        SetVisible(state != State.Hidden);
    }

    void SetVisible(bool value)
    {
        value = value && !trainingHidden;
        foreach (var r in renderers) if (r != null) r.enabled = value;
        foreach (var c in colliders) if (c != null) c.enabled = value;
    }

    // ===================== ANIMATION (Playables) =====================

    void BuildGraph()
    {
        clips[(int)Clip.Idle] = idleClip;
        clips[(int)Clip.Look] = lookAroundClip;
        clips[(int)Clip.Walk] = walkClip;
        clips[(int)Clip.Push] = pushButtonClip;

        graph = PlayableGraph.Create("NpcWorker");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        if (animator == null)
        {
            Debug.LogWarning("NpcWorker: no Animator on the NPC (run Tools > Ghost > Use The Real NPC)");
            return;
        }

        var output = AnimationPlayableOutput.Create(graph, "NPC", animator);
        mixer = AnimationMixerPlayable.Create(graph, clips.Length);
        output.SetSourcePlayable(mixer);

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;

            players[i] = AnimationClipPlayable.Create(graph, clips[i]);
            players[i].SetSpeed(0.0);   // the time is set by hand (looping, or once)
            graph.Connect(players[i], 0, mixer, i);
            mixer.SetInputWeight(i, 0f);
        }

        graph.Play();

        if (clips[(int)Clip.Idle] == null)
            Debug.LogWarning("NpcWorker: no Idle clip assigned (run Tools > Ghost > Setup NPC Worker)");

        graphReady = true;
        Play(Clip.Idle);
        weights[(int)Clip.Idle] = 1f;
    }

    void Play(Clip clip)
    {
        // A missing clip falls back to idle
        if (clips[(int)clip] == null) clip = Clip.Idle;

        if (clip != current) times[(int)clip] = 0f;
        current = clip;
    }

    bool ClipFinished(Clip clip)
    {
        var c = clips[(int)clip];
        return c == null || times[(int)clip] >= c.length - 0.05f;
    }

    void UpdateAnimation(float dt)
    {
        float sum = 0f;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;

            float target = i == (int)current ? 1f : 0f;
            weights[i] = Mathf.MoveTowards(weights[i], target, dt / Mathf.Max(0.01f, blendTime));
            sum += weights[i];

            if (weights[i] <= 0f && i != (int)current) continue;

            float length = Mathf.Max(0.01f, clips[i].length);

            // the CHECK animation is slowed so that one round takes 'Check Animation Seconds' (5)
            float speed = i == (int)Clip.Push && checkAnimationSeconds > 0.1f ? length / checkAnimationSeconds : 1f;
            times[i] += dt * speed;

            // Look Around plays once, the others loop
            if (i == (int)Clip.Look) times[i] = Mathf.Min(times[i], length);
            else times[i] = Mathf.Repeat(times[i], length);

            players[i].SetTime(times[i]);
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            mixer.SetInputWeight(i, sum > 1f ? weights[i] / sum : weights[i]);
        }
    }
}
