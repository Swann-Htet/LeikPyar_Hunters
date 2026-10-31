using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.AI;

// Sequence: ghost spawns at Point 1 and HOLDS. When the player enters that
// point's trigger zone, the ghost walks to the player; once it reaches the
// player it vanishes and respawns at the next point (2, 3, ...).
// Only active during the ghost hours of the game clock.
public class GhostSpawner : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Transform ghost;
    public Transform[] spawnPoints;
    public Transform player;
    public WallClockController clock;

    [System.Serializable]
    public class SpawnLampLink
    {
        public Transform spawnPoint;
        [Tooltip("Trigger collider (Is Trigger) - when the player enters, the ghost comes")]
        public Collider triggerZone;
        [Tooltip("Lamps that turn OFF when the ghost spawns at this point")]
        public ScaryLamp[] lampsToTurnOff;
    }

    [Header("Per Spawn Point: Trigger Zone + Lamps")]
    [Tooltip("Leave lamps empty to auto-use the nearest lamp")]
    public SpawnLampLink[] lampLinks;

    [Header("Order")]
    [Tooltip("Off = Point 1, 2, 3... in list order")]
    public bool randomOrder = false;
    [Tooltip("After the last point, start again from the first")]
    public bool loopSequence = true;

    [Header("Chase")]
    public float runSpeed = 4f;
    [Tooltip("Ghost reaches the player at this distance, then respawns at next point")]
    public float catchDistance = 1.2f;
    public float respawnDelay = 2f;
    [Tooltip("Animator bool set true while the ghost runs at the player (add this parameter in the Animator)")]
    public string runBoolParameter = "Run";
    [Tooltip("Animator state names (used only if the bool parameter is missing)")]
    public string runStateName = "Running Crawl";
    public string idleStateName = "Sad Idle";

    [Header("Jump Scare (plays before the ghost runs)")]
    public AudioClip jumpScareClip;
    public AudioSource jumpScareAudio;
    [Tooltip("Seconds the jump scare plays before the ghost starts running")]
    public float jumpScareDuration = 4f;

    [Header("Damage")]
    [Tooltip("Health removed from the player when the ghost reaches them")]
    public int damageOnHit = 20;
    [Tooltip("Every day after the first, a hit hurts this much more (it grows slowly). Day 1 = Damage On Hit")]
    public int damagePerDay = 4;
    [Tooltip("The most the days can add to a hit")]
    public int damageMaxBonus = 20;

    [Header("Chase Audio")]
    [Tooltip("Song/sound played while the ghost is coming. Sing audio pauses during the chase.")]
    public AudioClip chaseClip;
    public AudioSource chaseAudio;

    [Header("Audio (auto-found on ghost, e.g. 'Ghost Sing')")]
    public AudioSource singAudio;

    [Header("Songs while she walks")]
    [Tooltip("Songs she sings while she is walking: a random one, and when it ends another one (never the same twice in a row). " +
             "Also every sound file in Assets/Resources/GhostSongs is used: just drop your songs there. Empty = her own 'Ghost Sing' sound")]
    public AudioClip[] walkSongs;

    [Header("Her voice: sounds she makes now and then (the woman, Solid)")]
    [Tooltip("Your sounds. They are also taken from the folder Assets/Resources/GhostVoices (put the files there)")]
    public AudioClip[] voiceClips;
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Tooltip("She makes a sound after a random time between these two (seconds)")]
    public float voiceMinGap = 8f;
    public float voiceMaxGap = 25f;
    [Tooltip("How far it can be heard (meters)")]
    public float voiceRange = 30f;
    [Tooltip("Played only when she catches the player (also from the folder Assets/Resources/GhostCatch: the laugh)")]
    public AudioClip[] catchClips;
    private readonly System.Collections.Generic.List<AudioClip> songs = new System.Collections.Generic.List<AudioClip>();
    private AudioClip originalSing;
    private bool originalLoop;
    private int lastSong = -1;

    [Header("Clock")]
    public bool overrideClockStart = true;
    [Range(0, 23)] public int gameStartHour = 20;
    [Range(0, 59)] public int gameStartMinute = 0;

    [Header("The Solid ghost night (game clock)")]
    [Tooltip("She appears at this hour and SINGS where she stands (8 PM = 20)")]
    public float appearHour = 20f;
    [Tooltip("At this hour she stops singing and WALKS through the house (10 PM = 22). Until then she only sings")]
    public float walkHour = 22f;
    [Tooltip("When she sees the player she runs at this speed (meters per second). The player walks at 5, so a player can get away")]
    public float chaseSpeed = 3.0f;
    [Tooltip("Every day after the first, the chase is this much faster (meters per second). Day 1 = the Chase Speed above")]
    public float chaseSpeedPerDay = 1f;
    [Tooltip("The most the days can add to the speed")]
    public float chaseSpeedMaxBonus = 3f;
    [Tooltip("Not used any more: she no longer waits and searches at the hiding place. When the player hides she loses track at once")]
    public float searchSeconds = 6f;

    [Header("Ghost Active Hours (the old fields: Hour End is still her last hour)")]
    [Tooltip("Ghost is active from this hour (inclusive)")]
    public float hourStart = 0f;   // 12:00 AM
    [Tooltip("...until this hour (exclusive)")]
    public float hourEnd = 5f;     // 5:00 AM

    [Header("Facing")]
    [Tooltip("Adjust if the model's front is not +Z")]
    public float rotationOffsetY = 0f;

    public enum PathMode { Once, PingPong, Loop }

    [Header("Ghost Pathway (ghost walks along waypoints)")]
    public bool usePath = true;
    [Tooltip("Empty object whose CHILDREN are the waypoints, in order (top to bottom in the Hierarchy)")]
    public Transform pathRoot;
    [Tooltip("Or drag the waypoints here one by one (used first if filled)")]
    public Transform[] pathPoints;
    [Tooltip("Game-clock hour when the ghost starts walking the pathway (must be inside the ghost hours)")]
    public float pathStartHour = 22f;
    public float pathWalkSpeed = 1.2f;
    [Tooltip("How fast the woman (Solid) walks while she patrols and wanders (meters per second). It replaces the old Path Walk Speed")]
    public float patrolSpeed = 3.2f;
    [Tooltip("Seconds the ghost stands still at each waypoint")]
    public float waypointPause = 1.5f;
    [Tooltip("Once = walk to the end, vanish, reappear at the start. PingPong = walk back and forth. Loop = jump back to the first waypoint")]
    public PathMode pathMode = PathMode.PingPong;
    [Tooltip("The lamp nearest to the ghost turns off while it walks past")]
    public bool pathLampsFollow = true;
    [Tooltip("Animator bool set true while the ghost walks (add this parameter in the Animator)")]
    public string walkBoolParameter = "Walk";
    [Tooltip("If the player comes this close to the walking ghost, jump scare + chase starts. 0 = never")]
    public float noticeDistance = 5f;
    public bool noticeNeedsLineOfSight = true;
    [Tooltip("The ghost also notices the player when the player LOOKS at the ghost from this far (clear line of sight needed). 0 = off")]
    public float playerSeesGhostDistance = 10f;
    [Tooltip("How centered the ghost must be in the player's view (degrees from the middle of the screen)")]
    public float playerSeesGhostAngle = 40f;

    [Tooltip("Prints in the Console once per second why the ghost does or doesn't notice the player")]
    public bool debugNotice = true;

    [Header("Chase (after the ghost sees the player)")]
    [Tooltip("The ghost gives up when the player is farther than this (meters) and goes back to normal")]
    public float chaseGiveUpDistance = 16f;
    [Tooltip("After giving up, the ghost ignores the player for this many seconds")]
    public float giveUpIgnoreTime = 4f;
    [Tooltip("The jump scare only plays if the last one was longer ago than this (seconds). Otherwise the ghost just chases")]
    public float jumpScareCooldown = 30f;

    [Header("Random Wander (needs a baked NavMesh - avoids walls and furniture)")]
    [Tooltip("ON = ghost walks to random reachable spots around the house instead of fixed waypoints")]
    public bool randomWander = true;
    [Tooltip("Farthest a new random destination can be from the ghost (meters)")]
    public float wanderRadius = 15f;
    [Tooltip("Closest a new random destination can be (meters)")]
    public float wanderMinDistance = 4f;

    [Header("Baby Ghost")]
    [Tooltip("Baby ghost spawners. Each one spawns its own baby, always at its own spot. " +
             "Empty = every object tagged 'bghost' is used")]
    public BabyGhostSpawner[] babySpawners;
    [Tooltip("One extra baby (optional). It spawns in a random room")]
    public Transform babyGhost;
    [Tooltip("Optional own points for the baby. Empty = uses the same Spawn Points as the woman")]
    public Transform[] babySpawnPoints;
    [Tooltip("Baby never spawns within this distance of the woman (= same room)")]
    public float sameRoomDistance = 6f;
    [Tooltip("Player can interact with the baby within this distance...")]
    public float babyInteractDistance = 4f;
    [Tooltip("...and when looking within this angle of the center of the view")]
    public float babySeeAngle = 30f;
    public KeyCode babyInteractKey = KeyCode.E;
    [Tooltip("Optional text object for the prompt. Empty = a simple on-screen label is drawn")]
    public TMP_Text babyPromptText;
    public string babyPromptMessage = "[E] Interact";
    [Tooltip("Baby turns toward the player (only rotates around Y, keeps its model tilt)")]
    public bool babyFacePlayer = true;
    [Tooltip("Adjust if the baby model's front is not facing the player")]
    public float babyRotationOffsetY = 0f;

    [Header("Random Baby Ghosts (spread over the whole NavMesh, needs a baked NavMesh)")]
    [Tooltip("ON = every night a random number of baby ghosts appear at random spots of the house (every room). The old Baby Spawners / Baby Spawn Points are then ignored")]
    public bool randomBabiesOnNavMesh = true;
    [Tooltip("How many baby ghosts appear each night: a random number between Min and Max")]
    public int babyCountMin = 2;
    public int babyCountMax = 5;
    [Tooltip("ORBS (Lv 1) in the house at the same time: a random number between these two (3-4). It replaces 'Baby Count' above")]
    public int orbsAtOnceMin = 3;
    public int orbsAtOnceMax = 4;
    [Tooltip("ON = when the player takes an Orb to the generator, a NEW Orb appears at another place, so the number in the house stays the same. " +
             "Nothing else changes: the woman (Solid) and the Mirages (Lv 2) stay, and a Mirage that is taken does not come back")]
    public bool respawnOrbs = true;
    [Tooltip("Extra Orb objects kept ready for the new Orbs (an Orb that was taken is used again after 'Orb Reuse Seconds')")]
    public int orbSpareObjects = 3;
    [Tooltip("A taken Orb can be used again after this many seconds (the generator needs it for its work: 9 seconds)")]
    public float orbReuseSeconds = 14f;
    [Tooltip("The baby to copy when more are needed. Empty = the first baby found in the scene")]
    public GameObject babyTemplate;

    [Header("Orb look")]
    [Tooltip("The model of an Orb (the baby ghost). The butterfly: ASSET/Orb/Butterfly Orb.prefab (Tools > Ghost > Use Butterfly Orb, MirageGhost And Muzzle Orb sets it). It replaces the baby's old model. Empty = the baby keeps its own model")]
    public GameObject orbPrefab;
    [Tooltip("How wide the glowing ball looks (meters)")]
    public float orbWidth = 0.9f;
    [Tooltip("How high above the baby's pivot it floats (meters)")]
    public float orbLift = 0.45f;

    [Header("Mirage (a second kind of ghost, at random spots of the house)")]
    [Tooltip("The Mirage to copy: a prefab or an object (its tag becomes Mirage). Empty = a teal wisp with a ring, made by the game, is used until you add your own")]
    public GameObject mirageTemplate;
    [Tooltip("The size of your own Mirage model (1 = as it comes, 1.6 = 60% bigger)")]
    public float mirageScale = 1.6f;
    [Tooltip("How many Mirages appear each night: a random number between Min and Max (0 = none)")]
    public int mirageCountMin = 1;
    public int mirageCountMax = 2;
    [Tooltip("Babies are at least this far apart (meters), so they end up in different rooms")]
    public float babySeparation = 6f;
    [Tooltip("A baby never appears closer than this to the player (meters)")]
    public float babyMinDistanceFromPlayer = 4f;
    [Tooltip("Only spots the player can walk to (a closed door can cut a room off)")]
    public bool babyMustBeReachable = true;
    [Tooltip("Height of a baby above the floor (meters). -1 = same height the baby has in the scene now")]
    public float babyHoverHeight = -1f;

    private enum State { Inactive, Waiting, Patrolling, Scare, Chasing, Respawning }
    private State state = State.Inactive;

    private List<Transform> pathCache;
    private int patrolIndex;
    private int patrolDir = 1;
    private float patrolWaitUntil;
    private bool isWalking;
    private bool hasWalkParam;
    private float nextDebugTime;
    private float lastScareTime = -999f;
    private float noticeBlockedUntil;
    private NavMeshPath chasePath;
    private Vector3[] chaseCorners;
    private int chaseCornerIndex;
    private float nextRepathTime;
    private Vector3[] wanderCorners;
    private int wanderCornerIndex;
    // The places (spawn points = rooms) still to visit in this round. She goes to every place once before one comes again.
    private readonly List<int> placeBag = new List<int>();
    private int lastPlace = -1;

    private Renderer[] ghostRenderers;
    private Animator ghostAnimator;
    private int currentIndex = -1;
    private float respawnTime;
    private float scareEndTime;
    private class BabyEntry
    {
        public Transform t;
        public Renderer[] renderers;
        public Vector3 baseEuler;
        public Transform home;
        public Vector3 homePos;
        public int lastPoint = -1;
        public bool active;
        public bool collected;
        public bool live;          // an Orb that is part of tonight (placed in the house, not taken yet)
        public float freeAt;       // a taken Orb object can be used again from this time
    }

    private bool randomBabiesActive;
    private int nightBabyCount;
    private float babyHover = 0.5f;
    private bool navReady;
    private Vector3[] navVertices;
    private int[] navIndices;
    private float[] navCumulativeArea;
    private float navTotalArea;

    private readonly List<BabyEntry> babies = new List<BabyEntry>();
    private readonly List<BabyEntry> mirages = new List<BabyEntry>();
    private int nightMirageCount = -1;
    private bool nightPlaced;     // the Orbs and Mirages of tonight already have their places
    private bool babiesReady;
    private Camera playerCam;
    private bool promptVisible;
    private string activePrompt;
    private GhostGun gun;
    private bool banishedTonight;
    private readonly List<ScaryLamp> offLamps = new List<ScaryLamp>();

    void Awake()
    {
        // Runs before any Start(), so the clock's own Start() reads the new hour
        if (clock == null) clock = FindFirstObjectByType<WallClockController>();

        if (overrideClockStart && clock != null)
        {
            clock.startHour = gameStartHour;
            clock.startMinute = gameStartMinute;
        }
    }

    void Start()
    {
        if (clock == null) clock = FindFirstObjectByType<WallClockController>();
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (ghost == null)
        {
            var g = GameObject.Find("Womann@Sad Idle");
            if (g != null) ghost = g.transform;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            var found = new List<Transform>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("Spwan Point") || t.name.StartsWith("Spawn Point"))
                    found.Add(t);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            spawnPoints = found.ToArray();
        }

        if (ghost != null)
        {
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
            ghostAnimator = ghost.GetComponentInChildren<Animator>(true);
            if (singAudio == null)
                singAudio = ghost.GetComponentInChildren<AudioSource>(true);
        }

        pathCache = BuildPath();
        if (ghostAnimator != null && !string.IsNullOrEmpty(walkBoolParameter))
        {
            foreach (var p in ghostAnimator.parameters)
                if (p.name == walkBoolParameter && p.type == AnimatorControllerParameterType.Bool)
                    hasWalkParam = true;
        }
        if (usePath && randomWander)
        {
            var probe = new NavMeshHit();
            if (!NavMesh.SamplePosition(ghost != null ? ghost.position : transform.position, out probe, 50f, NavMesh.AllAreas))
                Debug.LogWarning("[Ghost] Random Wander is ON but no NavMesh found. Bake one first (NavMeshSurface > Bake).");
        }
        else if (usePath && pathCache.Count < 2)
            Debug.LogWarning("[Ghost] Pathway needs at least 2 waypoints (assign Path Root or Path Points). The ghost will not patrol.");
        if (usePath && !hasWalkParam)
            Debug.LogWarning("[Ghost] Animator has no bool parameter '" + walkBoolParameter + "'. The ghost will slide while walking (add the Walk parameter).");

        EnsureBabies();
        if (babies.Count == 0)
            Debug.LogWarning("[Ghost] No baby ghost found. Give your baby ghosts the tag '" + BabyGhostFinder.Tag + "' (Tools > Ghost > Setup Baby Tag And Generator).");

        gun = FindFirstObjectByType<GhostGun>();

        if (player != null)
            playerCam = player.GetComponentInChildren<Camera>(true);
        if (playerCam == null)
            playerCam = Camera.main;

        if (singAudio != null)
        {
            singAudio.playOnAwake = false;
            singAudio.Stop();
            originalSing = singAudio.clip;
            originalLoop = singAudio.loop;
        }
        CollectSongs();

        if (chaseAudio == null && chaseClip != null && ghost != null)
        {
            chaseAudio = ghost.gameObject.AddComponent<AudioSource>();
            chaseAudio.spatialBlend = 1f;
            chaseAudio.rolloffMode = AudioRolloffMode.Linear;
            chaseAudio.maxDistance = 30f;
        }
        if (chaseAudio != null)
        {
            chaseAudio.playOnAwake = false;
            chaseAudio.Stop();
        }

        if (jumpScareAudio == null && jumpScareClip != null && ghost != null)
        {
            jumpScareAudio = ghost.gameObject.AddComponent<AudioSource>();
            jumpScareAudio.spatialBlend = 0f; // 2D: full volume in the player's ears
        }
        if (jumpScareAudio != null)
        {
            jumpScareAudio.playOnAwake = false;
            jumpScareAudio.loop = false;
            jumpScareAudio.Stop();
        }

        SetupTriggerZones();

        if (overrideClockStart && clock != null)
            clock.SetGameTime(gameStartHour, gameStartMinute);

        SetGhostVisible(false);
        SetBabiesVisible(false);
    }

    void SetupTriggerZones()
    {
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var link = GetLink(spawnPoints[i]);
            if (link == null || link.triggerZone == null) continue;

            link.triggerZone.isTrigger = true;
            var relay = link.triggerZone.GetComponent<GhostTriggerRelay>();
            if (relay == null)
                relay = link.triggerZone.gameObject.AddComponent<GhostTriggerRelay>();
            relay.spawner = this;
            relay.pointIndex = i;
        }
    }

    // ===================== TUTORIAL (see TutorialMode) =====================

    [HideInInspector] public bool tutorialMode;      // the training: the woman does not come by herself
    private bool tutorialDrill;                      // ... only while the hide drill runs
    private bool tutorialShow;                       // the radar room: she stands in the room and does nothing (the scanner reads her)
    public System.Action<Transform> onBabyCollected; // an Orb or a Mirage was taken to the generator

    // The training starts: no ghost, no damage
    public void TutorialPrepare()
    {
        tutorialMode = true;
        damageOnHit = 0;
        EnsureBabies();

        // The scene starts at 8 PM: the night may already have begun (the woman, the Orbs and the Mirages shown). The training is
        // by day, so all of it is put away now, before an Orb is placed (the spawner would hide a new Orb as part of the old night).
        Deactivate();
        tutorialDrill = false;
        if (ghost != null) SetGhostVisible(false);
        SetBabiesVisible(false);
    }

    // An Orb to practise on, placed here
    public Transform TutorialPlaceOrb(Vector3 position)
    {
        EnsureBabies();
        if (state != State.Inactive) Deactivate();

        foreach (var b in babies)
        {
            if (b.collected) continue;

            b.t.position = position + Vector3.up * babyHover;
            SetBabyVisible(b, true);
            FaceBaby(b);
            return b.t;
        }
        return null;
    }

    // The hide drill: the woman appears here and runs at the player (she does no damage)
    public void TutorialStartChase(Vector3 position)
    {
        if (ghost == null) return;

        tutorialDrill = true;
        ghost.position = position;
        SetGhostVisible(true);
        FacePlayer();
        StartChase();
    }

    // The radar room: the woman stands here, shown but harmless (she does not notice the player, no chase, no damage). Only the scanner reads her.
    public void TutorialShowGhost(Vector3 position)
    {
        if (ghost == null) return;

        tutorialShow = true;
        ghost.position = position;
        SetRunning(false);
        SetWalking(false);
        FacePlayer();
        SetGhostVisible(true);
        state = State.Waiting;
    }

    public void TutorialHideGhost()
    {
        tutorialShow = false;
        if (state != State.Inactive) Deactivate();
        else if (ghost != null) SetGhostVisible(false);
    }

    public bool TutorialGhostShown => tutorialShow;

    public void TutorialEndDrill()
    {
        tutorialDrill = false;
    }

    void Update()
    {
        if (ghost == null || clock == null || spawnPoints.Length == 0)
            return;

        bool ghostHour = IsGhostHour(clock.GameHour24);
        if (tutorialMode) ghostHour = tutorialDrill || tutorialShow;

        // Every night starts and ends clean (day 2, 3...): the woman, the Orbs and the Mirages are put away and come back new,
        // whatever state they were in. The log says when it happens.
        if (!tutorialMode && ghostHour != wasGhostHour)
        {
            wasGhostHour = ghostHour;
            ResetNight();
            Debug.Log("[Ghost] " + (ghostHour ? "The night begins" : "The night is over") + " (clock " + clock.GameHour24.ToString("0.0") +
                      ", day " + AgentStats.Day + ": she runs " + CurrentChaseSpeed.ToString("0.0") + " m/s and hits for " + CurrentDamage + ")");
        }

        // Ghost hours are over: the next night (12:00 AM) they can appear again
        if (!ghostHour)
        {
            banishedTonight = false;
            nightBabyCount = 0;
            nightMirageCount = -1;
            nightPlaced = false;
            foreach (var b in babies) { b.collected = false; b.live = false; }
            foreach (var m in mirages) m.collected = false;
        }

        if (ghostHour && state == State.Inactive && !banishedTonight && !tutorialMode)
        {
            SpawnAtNextPoint();
        }
        else if (!ghostHour && state != State.Inactive)
        {
            Deactivate();
            return;
        }

        KeepHerComing(ghostHour);
        UpdateVoice();

        BabyEntry lookedAt = null;
        foreach (var b in babies)
        {
            if (!b.active) continue;
            FaceBaby(b);
            if (lookedAt == null && CanSeeBaby(b)) lookedAt = b;
        }

        // With a GhostGun in the scene, the gun handles aiming, the prompt and collecting
        if (gun == null && babies.Count > 0)
        {
            SetPrompt(lookedAt != null);

            if (lookedAt != null && (Input.GetKeyDown(babyInteractKey) || MobileInput.GetInteract()))
            {
                SetPrompt(false);
                OnBabySeen(lookedAt);
                return;
            }
        }

        switch (state)
        {
            case State.Waiting:
                if (PathActive(clock.GameHour24) && (randomWander || GetPath().Count >= 2))
                {
                    StartPatrol();
                    break;
                }
                FacePlayer();
                break;

            case State.Patrolling:
                Patrol();
                break;

            case State.Scare:
                FacePlayer();
                if (Time.time >= scareEndTime)
                    StartChase();
                break;

            case State.Chasing:
                ChasePlayer();
                break;

            case State.Respawning:
                if (Time.time >= respawnTime)
                    SpawnAtNextPoint();
                break;
        }
    }

    private bool wasGhostHour;
    private bool caughtTonight;
    private float notOutSince = -1f;

    bool AnyGhostRendererOn()
    {
        if (ghostRenderers == null) return false;
        foreach (var r in ghostRenderers)
            if (r != null && r.enabled && !(r is ParticleSystemRenderer)) return true;
        return false;
    }

    // She comes EVERY night. If anything left her stuck (hidden for good, waiting to come back, switched off), this sets her free
    // again and writes why in the Console.
    void KeepHerComing(bool ghostHour)
    {
        if (tutorialMode || !ghostHour || banishedTonight || ghost == null) return;

        bool out_ = state == State.Waiting || state == State.Patrolling || state == State.Scare || state == State.Chasing;

        // she is "out" but nobody can see her: her model is off
        if (out_)
        {
            if (!ghost.gameObject.activeInHierarchy || !AnyGhostRendererOn())
            {
                if (notOutSince < 0f) notOutSince = Time.time;
                if (Time.time - notOutSince > 2f)
                {
                    Debug.LogWarning("[Ghost] The woman was out but not visible: she is switched on again");
                    if (!ghost.gameObject.activeSelf) ghost.gameObject.SetActive(true);
                    ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
                    SetGhostVisible(true);
                    notOutSince = -1f;
                }
            }
            else notOutSince = -1f;
            return;
        }

        notOutSince = -1f;

        // she waits to come back (after a catch, or "stay hidden") but the wait is long over
        if (state == State.Respawning && Time.time > respawnTime + 8f)
        {
            Debug.LogWarning("[Ghost] The woman waited too long to come back: she comes now");
            respawnTime = Time.time;
            SpawnAtNextPoint();
            return;
        }

        // a night with no woman at all (nothing started her)
        if (state == State.Inactive)
        {
            Debug.LogWarning("[Ghost] It is night and the woman is not out: she comes now");
            SpawnAtNextPoint();
        }
    }

    // ---------------------------------------------------------------- her voice
    private AudioSource voiceSource;
    private readonly List<AudioClip> voices = new List<AudioClip>();
    private bool voicesLoaded;
    private float nextVoice;
    private int lastVoice = -1;

    void LoadVoices()
    {
        voicesLoaded = true;
        voices.Clear();
        if (voiceClips != null)
            foreach (var clip in voiceClips)
                if (clip != null && !voices.Contains(clip)) voices.Add(clip);
        foreach (var clip in Resources.LoadAll<AudioClip>("GhostVoices"))
            if (clip != null && !voices.Contains(clip)) voices.Add(clip);

        if (voices.Count > 0) Debug.Log("[Ghost] " + voices.Count + " voice sound(s) for the woman");
    }

    private AudioClip[] catchLoaded;
    private AudioSource catchSource;

    // The laugh: only when she catches the player (not in the random sounds)
    void PlayCatchSound()
    {
        if (tutorialMode) return;

        if (catchLoaded == null)
        {
            var list = new List<AudioClip>();
            if (catchClips != null)
                foreach (var clip in catchClips) if (clip != null) list.Add(clip);
            foreach (var clip in Resources.LoadAll<AudioClip>("GhostCatch")) if (clip != null && !list.Contains(clip)) list.Add(clip);
            catchLoaded = list.ToArray();
        }
        if (catchLoaded.Length == 0 || ghost == null) return;

        // her voice source is on her; she is hidden a moment later, so the laugh is played from the player's side of her
        if (catchSource == null) catchSource = Sfx.AddSource(gameObject, false, false, 1f);       // on this manager: it does not stop when she vanishes
        catchSource.PlayOneShot(catchLoaded[Random.Range(0, catchLoaded.Length)], Mathf.Max(0.8f, voiceVolume));
    }

    // While she is out in the house she makes one of the sounds now and then (a random one, at a random time)
    void UpdateVoice()
    {
        // she only sings in the first two hours (her own song, nothing else): the random sounds start when the hunting starts
        bool hunting = clock != null && usePath && PathActive(clock.GameHour24);
        bool out_ = !tutorialMode && hunting && (state == State.Waiting || state == State.Patrolling || state == State.Scare || state == State.Chasing);
        if (!out_)
        {
            nextVoice = 0f;
            return;
        }

        if (!voicesLoaded) LoadVoices();
        if (voices.Count == 0) return;

        if (nextVoice <= 0f)
        {
            nextVoice = Time.time + Random.Range(voiceMinGap, Mathf.Max(voiceMinGap, voiceMaxGap)) * 0.5f;     // the first one comes sooner
            return;
        }
        if (Time.time < nextVoice) return;
        nextVoice = Time.time + Random.Range(voiceMinGap, Mathf.Max(voiceMinGap, voiceMaxGap));

        if (voiceSource == null)
        {
            voiceSource = Sfx.AddSource(ghost.gameObject, true, false, voiceVolume);
            voiceSource.maxDistance = voiceRange;
        }
        if (voiceSource.isPlaying) return;

        int pick = Random.Range(0, voices.Count);
        if (voices.Count > 1 && pick == lastVoice) pick = (pick + 1 + Random.Range(0, voices.Count - 1)) % voices.Count;
        lastVoice = pick;

        voiceSource.maxDistance = voiceRange;
        voiceSource.PlayOneShot(voices[pick], voiceVolume);
    }

    // Day 1 is as set above. Every day after it the ghost runs faster and hits harder (up to the maximum). The training has no days and no damage.
    int DaysPassed { get { return tutorialMode ? 0 : Mathf.Max(0, AgentStats.Day - 1); } }

    public float CurrentChaseSpeed
    {
        get { return chaseSpeed + Mathf.Min(chaseSpeedMaxBonus, chaseSpeedPerDay * DaysPassed); }
    }

    public int CurrentDamage
    {
        get
        {
            if (damageOnHit <= 0) return 0;
            return damageOnHit + Mathf.Min(damageMaxBonus, damagePerDay * DaysPassed);
        }
    }

    // A new night begins, or it ended: nothing of the last night is left (a ghost that was hidden, caught or stuck comes back)
    // Counts the nights (it goes up when a night begins and when it ends): the generator uses it to count the MEP made tonight
    public static int NightNumber { get; private set; }

    void ResetNight()
    {
        NightNumber++;
        if (state != State.Inactive) Deactivate();
        state = State.Inactive;
        currentIndex = -1;
        searchUntil = -1f;
        caughtTonight = false;
        banishedTonight = false;
        nightBabyCount = 0;
        nightMirageCount = -1;
        nightPlaced = false;
        foreach (var b in babies) { b.collected = false; b.live = false; }
        foreach (var m in mirages) m.collected = false;
    }

    // Called by GhostTriggerRelay when the player walks into a point's zone
    public void OnPlayerEnteredZone(int pointIndex)
    {
        if (state != State.Waiting || pointIndex != currentIndex || HideSystem.PlayerHidden || tutorialMode)
            return;

        BeginScare();
    }

    void BeginScare()
    {
        // 1) Jump scare first
        SetWalking(false);
        lastScareTime = Time.time;
        state = State.Scare;
        scareEndTime = Time.time + jumpScareDuration;
        if (singAudio != null) singAudio.Stop();

        if (jumpScareAudio != null && jumpScareClip != null)
        {
            jumpScareAudio.clip = jumpScareClip;
            jumpScareAudio.Play();
        }
    }

    // 2) After the jump scare: run at the player
    void StartChase()
    {
        if (jumpScareAudio != null) jumpScareAudio.Stop();

        state = State.Chasing;
        chaseCorners = null;
        nextRepathTime = 0f;
        SetRunning(true);

        if (chaseAudio != null && chaseClip != null)
        {
            chaseAudio.clip = chaseClip;
            chaseAudio.loop = true;
            chaseAudio.Play();
        }
    }

    private float searchUntil = -1f;      // (the old search at the hiding place is gone; kept so the night reset still compiles)

    // Used by the hide system: is she hunting the player, and how far is she?
    public bool IsChasing { get { return state == State.Chasing; } }
    public float DistanceToPlayer { get { return player != null && ghost != null ? Vector3.Distance(player.position, ghost.position) : float.MaxValue; } }

    void ChasePlayer()
    {
        if (player == null) return;

        // The player hides: she does not wait or search at the hiding place any more, she loses track and goes back to her walk
        if (HideSystem.PlayerHidden)
        {
            GiveUpChase();
            return;
        }

        if (searchUntil >= 0f)
        {
            // the player came out while she was searching: she runs again
            searchUntil = -1f;
            SetRunning(true);
            if (chaseAudio != null && chaseClip != null && !chaseAudio.isPlaying)
            {
                chaseAudio.clip = chaseClip;
                chaseAudio.loop = true;
                chaseAudio.Play();
            }
        }

        Vector3 flat = player.position - ghost.position;
        float heightDiff = Mathf.Abs(flat.y);
        flat.y = 0f;

        // Reached the player (same floor level): hurt them, vanish, respawn at the next point
        if (flat.magnitude <= catchDistance && heightDiff < 2.5f)
        {
            var health = player.GetComponentInParent<PlayerHealth>();
            if (health != null) health.TakeDamage(CurrentDamage);
            PlayCatchSound();
            caughtTonight = true;                     // she does not sing again tonight (she comes back silent)
            if (singAudio != null) singAudio.Stop();

            SetRunning(false);
            SetGhostVisible(false);
            SetBabiesVisible(false);
            ReleaseLamps();
            if (chaseAudio != null) chaseAudio.Stop();
            state = State.Respawning;
            respawnTime = Time.time + respawnDelay;
            return;
        }

        // Player ran far enough away: the ghost gives up
        if (chaseGiveUpDistance > 0f && Vector3.Distance(player.position, ghost.position) > chaseGiveUpDistance)
        {
            GiveUpChase();
            return;
        }

        ChaseStep();
    }

    // Follows the player along the NavMesh (around walls), re-aiming at where the player is now
    void ChaseStep()
    {
        if (chasePath == null) chasePath = new NavMeshPath();

        if (chaseCorners == null || Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + 0.3f;
            chaseCorners = null;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(player.position, out hit, 3f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(ghost.position, hit.position, NavMesh.AllAreas, chasePath) &&
                chasePath.corners.Length >= 2)
            {
                chaseCorners = chasePath.corners;
                chaseCornerIndex = 1;
            }
        }

        Vector3 target;
        bool onNavMesh = chaseCorners != null && chaseCornerIndex < chaseCorners.Length;
        if (onNavMesh)
        {
            target = chaseCorners[chaseCornerIndex];
        }
        else
        {
            // No NavMesh path (ghost or player off the baked floor): straight line, same height
            target = new Vector3(player.position.x, ghost.position.y, player.position.z);
        }

        Vector3 dir = target - ghost.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ghost.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, rotationOffsetY, 0f);

        ghost.position = Vector3.MoveTowards(ghost.position, target, CurrentChaseSpeed * Time.deltaTime);

        if (onNavMesh && (ghost.position - target).sqrMagnitude < 0.0036f)
            chaseCornerIndex++;
    }

    // Player escaped: go back to normal
    void GiveUpChase()
    {
        Debug.Log("[Ghost] Player got away - ghost gives up");

        SetRunning(false);
        if (chaseAudio != null) chaseAudio.Stop();
        noticeBlockedUntil = Time.time + giveUpIgnoreTime;

        bool pathOn = usePath && PathActive(clock.GameHour24) && (randomWander || GetPath().Count >= 2);
        if (pathOn)
        {
            // Back to wandering / walking the pathway from where it stands
            wanderCorners = null;
            patrolWaitUntil = Time.time + 2f;
            state = State.Patrolling;
            return;
        }

        // Before the pathway hour: vanish and come back at the next spawn point
        SetGhostVisible(false);
        ReleaseLamps();
        state = State.Respawning;
        respawnTime = Time.time + respawnDelay;
    }

    void SpawnAtNextPoint()
    {
        int next;
        if (randomOrder && spawnPoints.Length > 1)
        {
            next = currentIndex;
            while (next == currentIndex)
                next = Random.Range(0, spawnPoints.Length);
        }
        else
        {
            next = currentIndex + 1;
            if (next >= spawnPoints.Length)
            {
                if (!loopSequence)
                {
                    // Sequence finished: stay hidden
                    state = State.Respawning;
                    respawnTime = float.MaxValue;
                    return;
                }
                next = 0;
            }
        }

        // a spawn point that is gone (destroyed) is skipped
        for (int tries = 0; tries < spawnPoints.Length && (next < 0 || next >= spawnPoints.Length || spawnPoints[next] == null); tries++)
            next = (next + 1 + spawnPoints.Length) % spawnPoints.Length;

        currentIndex = next;
        Transform point = spawnPoints[currentIndex];
        if (point == null)
        {
            Debug.LogError("[Ghost] There is no spawn point left for the woman.");
            state = State.Respawning;
            respawnTime = Time.time + 5f;
            return;
        }

        // her model is looked up again every night (it can be rebuilt by something), and she is switched on
        if (!ghost.gameObject.activeSelf) ghost.gameObject.SetActive(true);
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
        if (ghostAnimator == null) ghostAnimator = ghost.GetComponentInChildren<Animator>(true);

        ghost.position = point.position;
        SetRunning(false);
        FacePlayer();
        SetGhostVisible(true);
        state = State.Waiting;

        // Lamps of this spawn point go OFF (previous ones come back)
        ReleaseLamps();
        var linked = GetLinkedLamps(point);
        if (linked.Count == 0)
        {
            var near = NearestLamp(point.position);
            if (near != null) linked.Add(near);
        }

        foreach (var lamp in linked)
        {
            lamp.SetForcedOff(true);
            offLamps.Add(lamp);
        }
        Debug.Log($"[Ghost] Spawned at {point.name}, lamps OFF: {offLamps.Count}");

        // Whole house glitches
        foreach (var lamp in ScaryLamp.All)
        {
            if (!offLamps.Contains(lamp))
                lamp.TriggerGlitch();
        }

        // her own song: only in the singing hours, and not after she has caught the player
        if (!caughtTonight && !(usePath && PathActive(clock.GameHour24))) PlayOriginalSing();
        else if (singAudio != null) singAudio.Stop();

        // the orbs and Mirages: if something goes wrong there, she must still be in the house
        try { SpawnBabies(point); }
        catch (System.Exception e) { Debug.LogError("[Ghost] The orbs could not be placed tonight (she is out anyway): " + e); }
    }

    static bool IsBabySpawnerName(string n)
    {
        if (n.StartsWith("Spwan Point") || n.StartsWith("Spawn Point")) return false;
        return n.StartsWith("Spwan") || n.StartsWith("Spawn");
    }

    // Babies come from the Baby Spawners list. With an empty list, every object tagged
    // "bghost" is used (inactive ones too).
    // A baby inside a spawner (or an object named "Spwan*") always appears where you put it.
    // Any other baby appears at a random spawn point in a different room than the woman.
    void EnsureBabies()
    {
        if (babiesReady) return;
        babiesReady = true;

        List<Transform> source;
        if (babySpawners != null && babySpawners.Length > 0)
        {
            source = new List<Transform>();
            foreach (var s in babySpawners)
            {
                if (s == null) continue;
                Transform b = s.EnsureBaby();
                if (b != null && !source.Contains(b)) source.Add(b);
            }
            if (babyGhost != null && !source.Contains(babyGhost)) source.Add(babyGhost);
        }
        else
        {
            source = BabyGhostFinder.Find(babyGhost);
        }

        // The woman (type Solid) and the Mirage template carry a ghost tag too, but they are not babies
        source.RemoveAll(t => t == null
            || (ghost != null && (t == ghost || t.IsChildOf(ghost) || ghost.IsChildOf(t)))
            || (mirageTemplate != null && (t == mirageTemplate.transform || t.IsChildOf(mirageTemplate.transform))));

        foreach (var t in source)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

            ReplaceWithOrb(t);

            var entry = new BabyEntry
            {
                t = t,
                renderers = t.GetComponentsInChildren<Renderer>(true),
                baseEuler = t.eulerAngles, // keeps the model's own tilt (e.g. X -90)
                homePos = t.position
            };
            if (t.parent != null && (t.parent.GetComponent<BabyGhostSpawner>() != null || IsBabySpawnerName(t.parent.name)))
                entry.home = t.parent;
            babies.Add(entry);
        }

        if (randomBabiesOnNavMesh)
            FillBabyPool();

        FillMiragePool();
    }

    // The baby's old model goes, the orb model takes its place (the baby keeps its scripts, its place and its floating)
    void ReplaceWithOrb(Transform baby)
    {
        // every Orb can run away (see OrbRunner)
        if (baby != null && baby.GetComponent<OrbRunner>() == null) baby.gameObject.AddComponent<OrbRunner>();

        if (orbPrefab == null || baby == null) return;
        if (baby.GetComponentInChildren<OrbVisual>(true) != null || baby.GetComponentInChildren<ButterflyOrb>(true) != null) return;

        foreach (var r in baby.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            if (r.transform == baby) r.enabled = false;
            else DestroyImmediate(r.gameObject);
        }

        GameObject orb = Instantiate(orbPrefab, baby);
        orb.name = "Orb";
        orb.SetActive(true);
        orb.transform.localPosition = Vector3.zero;

        // the butterfly looks after itself (it flaps, floats and glows)
        var butterfly = orb.GetComponent<ButterflyOrb>();
        if (butterfly == null) butterfly = orb.GetComponentInChildren<ButterflyOrb>(true);
        if (butterfly != null)
        {
            butterfly.diameter = orbWidth;
            butterfly.lift = orbLift;
            return;
        }

        var visual = orb.AddComponent<OrbVisual>();
        visual.diameter = orbWidth;
        visual.lift = orbLift;
    }

    // ===================== MIRAGES =====================

    // Copies of the Mirage template (hidden until the night starts)
    void FillMiragePool()
    {
        if (Mathf.Max(mirageCountMin, mirageCountMax) <= 0) return;

        if (!EnsureNavData())
        {
            Debug.LogWarning("[Ghost] Mirages need a baked NavMesh.");
            return;
        }

        bool ownLook = mirageTemplate == null;   // nothing assigned: the Mirage is built from code (MirageGhost.CreateVisual)

        int wanted = Mathf.Max(mirageCountMin, mirageCountMax);
        while (mirages.Count < wanted)
        {
            GameObject copy;
            if (ownLook)
            {
                copy = MirageGhost.CreateVisual("Mirage " + mirages.Count, null);
            }
            else
            {
                copy = Instantiate(mirageTemplate, mirageTemplate.transform.parent);
                copy.name = "Mirage " + mirages.Count;
                copy.transform.localScale = mirageTemplate.transform.localScale * Mathf.Max(0.1f, mirageScale);
                copy.SetActive(true);
                if (copy.GetComponent<MirageGhost>() == null) copy.AddComponent<MirageGhost>();
            }

            try { copy.tag = "Mirage"; }
            catch (UnityException) { Debug.LogWarning("[Ghost] The tag 'Mirage' does not exist. Add it in Tag > Add Tag."); }

            var catchable = copy.GetComponent<CatchableGhost>();
            if (catchable != null) catchable.ResetCatch();

            var entry = new BabyEntry
            {
                t = copy.transform,
                renderers = copy.GetComponentsInChildren<Renderer>(true),
                baseEuler = copy.transform.eulerAngles,
                homePos = copy.transform.position
            };
            mirages.Add(entry);
            SetBabyVisible(entry, false);
        }

        Debug.Log("[Ghost] Mirages ready: " + mirages.Count + " (" + mirageCountMin + "-" + mirageCountMax + " per night)");
    }

    // Tonight's Mirages go to random spots, away from the woman, the babies and the player
    void SpawnMirages(Transform womanPoint, List<Vector3> taken)
    {
        if (mirages.Count == 0) return;

        if (nightMirageCount < 0)
        {
            int lo = Mathf.Max(0, Mathf.Min(mirageCountMin, mirageCountMax));
            int hi = Mathf.Max(lo, Mathf.Max(mirageCountMin, mirageCountMax));
            nightMirageCount = Mathf.Min(Random.Range(lo, hi + 1), mirages.Count);
            Debug.Log("[Ghost] Tonight " + nightMirageCount + " Mirage(s) hide in the house");
        }

        for (int i = 0; i < mirages.Count; i++)
        {
            var m = mirages[i];
            Vector3 point;
            if (i >= nightMirageCount || m.collected || !PickBabyPoint(womanPoint.position, taken, out point))
            {
                SetBabyVisible(m, false);
                continue;
            }

            taken.Add(point);
            m.t.position = point + Vector3.up * babyHover;
            SetBabyVisible(m, true);
            FaceBaby(m);
            Debug.Log("[Ghost] Mirage '" + m.t.name + "' hides at " + point.ToString("F1"));
        }
    }

    // ===================== RANDOM BABIES ON THE NAVMESH =====================

    // Makes sure there are enough baby objects for the biggest night (copies of the template)
    void FillBabyPool()
    {
        if (!EnsureNavData())
        {
            Debug.LogWarning("[Ghost] Random babies need a baked NavMesh. Using the old baby spawn points instead.");
            return;
        }

        Transform template = babyTemplate != null ? babyTemplate.transform : (babies.Count > 0 ? babies[0].t : null);
        if (template == null)
        {
            Debug.LogWarning("[Ghost] No baby ghost to copy. Assign Baby Template (or keep one baby ghost in the scene).");
            return;
        }

        // Hover height of the baby above its floor
        babyHover = 0.5f;
        if (babyHoverHeight >= 0f)
        {
            babyHover = babyHoverHeight;
        }
        else
        {
            float ground;
            if (CatchableGhost.TryGetGround(template.position + Vector3.up * 0.3f, template, null, out ground))
                babyHover = Mathf.Clamp(template.position.y - ground, 0f, 3f);
        }

        foreach (var b in babies) b.home = null;

        int wanted = Mathf.Max(Mathf.Max(babyCountMin, babyCountMax), Mathf.Max(orbsAtOnceMin, orbsAtOnceMax) + Mathf.Max(0, orbSpareObjects));
        while (babies.Count < wanted)
        {
            GameObject copy = Instantiate(template.gameObject, template.parent);
            copy.name = template.name + " (random " + babies.Count + ")";
            copy.SetActive(true);
            ReplaceWithOrb(copy.transform);

            var catchable = copy.GetComponent<CatchableGhost>();
            if (catchable != null) catchable.ResetCatch();

            Transform t = copy.transform;
            babies.Add(new BabyEntry
            {
                t = t,
                renderers = t.GetComponentsInChildren<Renderer>(true),
                baseEuler = t.eulerAngles,
                homePos = t.position
            });
        }

        randomBabiesActive = true;
        Debug.Log("[Ghost] Random Orbs ready: " + babies.Count + " Orb objects, " + orbsAtOnceMin + "-" + orbsAtOnceMax + " in the house at once" +
                  (respawnOrbs ? " (a new one appears when one is taken)" : ""));
    }

    // Reads the baked NavMesh once so random points can be picked evenly over the floor
    bool EnsureNavData()
    {
        if (navReady) return navVertices != null;
        navReady = true;

        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.indices == null || tri.indices.Length < 3) return false;

        navVertices = tri.vertices;
        navIndices = tri.indices;

        int count = navIndices.Length / 3;
        navCumulativeArea = new float[count];
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = navVertices[navIndices[i * 3]];
            Vector3 b = navVertices[navIndices[i * 3 + 1]];
            Vector3 c = navVertices[navIndices[i * 3 + 2]];
            sum += 0.5f * Vector3.Cross(b - a, c - a).magnitude;
            navCumulativeArea[i] = sum;
        }
        navTotalArea = sum;

        if (navTotalArea < 0.01f)
        {
            navVertices = null;
            return false;
        }
        return true;
    }

    // A random walkable spot, chosen evenly over the whole floor area
    Vector3 RandomNavPoint()
    {
        float r = Random.value * navTotalArea;

        int lo = 0, hi = navCumulativeArea.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (navCumulativeArea[mid] < r) lo = mid + 1;
            else hi = mid;
        }

        Vector3 a = navVertices[navIndices[lo * 3]];
        Vector3 b = navVertices[navIndices[lo * 3 + 1]];
        Vector3 c = navVertices[navIndices[lo * 3 + 2]];

        float u = Random.value, v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return a + u * (b - a) + v * (c - a);
    }

    bool PickBabyPoint(Vector3 womanPos, List<Vector3> taken, out Vector3 result)
    {
        NavMeshHit playerHit = new NavMeshHit();
        bool playerOnNav = player != null && NavMesh.SamplePosition(player.position, out playerHit, 3f, NavMesh.AllAreas);
        var path = new NavMeshPath();

        // Strict first, then a bit looser if the house is too small for all the rules
        for (int pass = 0; pass < 3; pass++)
        {
            float separation = pass == 0 ? babySeparation : (pass == 1 ? babySeparation * 0.5f : 0f);
            float womanDistance = pass == 2 ? 0f : sameRoomDistance;

            for (int i = 0; i < 40; i++)
            {
                Vector3 p = RandomNavPoint();

                if (Vector3.Distance(p, womanPos) < womanDistance) continue;
                if (player != null && Vector3.Distance(p, player.position) < babyMinDistanceFromPlayer) continue;

                bool tooClose = false;
                foreach (var other in taken)
                {
                    if (Vector3.Distance(p, other) < separation) { tooClose = true; break; }
                }
                if (tooClose) continue;

                if (babyMustBeReachable && playerOnNav && pass < 2)
                {
                    if (!NavMesh.CalculatePath(playerHit.position, p, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete)
                        continue;
                }

                result = p;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    // Tonight's babies (a random 2-5) go to random spots in different rooms, away from the woman
    void SpawnRandomBabies(Transform womanPoint)
    {
        if (nightBabyCount <= 0)
        {
            int lo = Mathf.Max(1, Mathf.Min(orbsAtOnceMin, orbsAtOnceMax));
            int hi = Mathf.Max(lo, Mathf.Max(orbsAtOnceMin, orbsAtOnceMax));
            nightBabyCount = Mathf.Min(Random.Range(lo, hi + 1), babies.Count);
            Debug.Log("[Ghost] Tonight " + nightBabyCount + " Orb(s) hide in the house");
        }

        var taken = new List<Vector3>();
        for (int i = 0; i < babies.Count; i++)
        {
            var b = babies[i];

            b.live = false;
            if (i >= nightBabyCount || b.collected)
            {
                SetBabyVisible(b, false);
                continue;
            }

            Vector3 point;
            if (!PickBabyPoint(womanPoint.position, taken, out point))
            {
                SetBabyVisible(b, false);
                continue;
            }

            b.live = true;
            taken.Add(point);
            b.t.position = point + Vector3.up * babyHover;
            SetBabyVisible(b, true);
            FaceBaby(b);
            Debug.Log("[Ghost] Baby ghost '" + b.t.name + "' hides at " + point.ToString("F1") + " (woman at " + womanPoint.name + ")");
        }

        SpawnMirages(womanPoint, taken);
    }

    void SpawnBabies(Transform womanPoint)
    {
        // The Orbs and Mirages keep their places for the whole night: when the woman (Solid) shows up again, or is detected,
        // they do not jump to new spots. They only show again (the ones not caught yet)
        if (nightPlaced)
        {
            ShowTonightsGhosts();
            return;
        }
        nightPlaced = true;

        if (randomBabiesActive)
        {
            SpawnRandomBabies(womanPoint);
            return;
        }

        var used = new List<int>();

        foreach (var b in babies)
        {
            if (b.collected) continue;

            if (b.home != null)
            {
                b.t.position = b.homePos;
            }
            else if (!PlaceInPool(b, womanPoint, used))
            {
                continue;
            }

            SetBabyVisible(b, true);
            FaceBaby(b);
        }

        SpawnMirages(womanPoint, new List<Vector3>());
    }

    // Shows tonight's Orbs and Mirages again, where they already are
    void ShowTonightsGhosts()
    {
        for (int i = 0; i < babies.Count; i++)
        {
            var b = babies[i];
            bool tonight = !randomBabiesActive || b.live;
            if (b.collected || !tonight) continue;

            SetBabyVisible(b, true);
            FaceBaby(b);
        }

        for (int i = 0; i < mirages.Count; i++)
        {
            var m = mirages[i];
            if (m.collected || i >= nightMirageCount) continue;

            SetBabyVisible(m, true);
            FaceBaby(m);
        }
    }

    bool PlaceInPool(BabyEntry b, Transform womanPoint, List<int> used)
    {
        // Empty slots in the list (None) are ignored
        bool hasOwnPoints = false;
        if (babySpawnPoints != null)
            foreach (var p in babySpawnPoints)
                if (p != null) { hasOwnPoints = true; break; }

        Transform[] pool = hasOwnPoints ? babySpawnPoints : spawnPoints;

        // Different room than the woman (and not the same point as last time)
        var candidates = new List<int>();
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == null || pool[i] == womanPoint) continue;
            if (Vector3.Distance(pool[i].position, womanPoint.position) < sameRoomDistance) continue;
            candidates.Add(i);
        }
        if (candidates.Count == 0)
        {
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null && pool[i] != womanPoint) candidates.Add(i);
        }
        if (candidates.Count == 0) return false;

        // Prefer points no other baby took this round
        var free = candidates.FindAll(i => !used.Contains(i));
        if (free.Count > 0) candidates = free;

        if (candidates.Count > 1)
            candidates.Remove(b.lastPoint);

        int pick = candidates[Random.Range(0, candidates.Count)];
        b.lastPoint = pick;
        used.Add(pick);

        b.t.position = pool[pick].position;
        Debug.Log($"[Ghost] Baby ghost '{b.t.name}' spawned at {pool[pick].name} (woman at {womanPoint.name})");
        return true;
    }

    void FaceBaby(BabyEntry b)
    {
        if (!babyFacePlayer || player == null) return;

        Vector3 dir = player.position - b.t.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + babyRotationOffsetY;
        b.t.rotation = Quaternion.Euler(b.baseEuler.x, yaw, b.baseEuler.z);
    }

    bool CanSeeBaby(BabyEntry b)
    {
        Transform babyGhost = b.t;
        if (playerCam == null) return false;

        Vector3 target = babyGhost.position + Vector3.up * 0.4f;
        Vector3 origin = playerCam.transform.position;
        Vector3 toBaby = target - origin;

        if (toBaby.magnitude > babyInteractDistance) return false;
        if (Vector3.Angle(playerCam.transform.forward, toBaby) > babySeeAngle) return false;

        // Blocked by a wall/door?
        var hits = Physics.RaycastAll(origin, toBaby.normalized, toBaby.magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(babyGhost) || h.transform.IsChildOf(player)) continue;
            return false;
        }
        return true;
    }

    // An Orb (Lv 1) was taken to the generator. The woman (Solid) and the Mirages stay where they are. A NEW Orb appears at another place of the
    // house, so the number of Orbs stays the same (3-4). A Mirage that is taken does not come back (see CollectBaby).
    void OnBabySeen(BabyEntry found)
    {
        found.collected = true;
        found.live = false;
        found.freeAt = Time.time + orbReuseSeconds;
        SetBabyVisible(found, false);

        // the training places its own Orbs
        if (tutorialMode) return;

        if (randomBabiesActive && respawnOrbs) SpawnReplacementOrb();
    }

    // A new Orb at a random place, away from the other Orbs, the Mirages, the woman and the player (it uses an Orb object that is free)
    void SpawnReplacementOrb()
    {
        BabyEntry free = null;
        foreach (var b in babies)
        {
            if (b.collected && !b.live && !b.active && Time.time >= b.freeAt) { free = b; break; }
        }
        if (free == null)
        {
            Debug.LogWarning("[Ghost] No Orb object is free for a new Orb. Raise 'Orb Spare Objects' on the Ghost Manager.");
            return;
        }

        var taken = new List<Vector3>();
        foreach (var b in babies)
            if (b.live && !b.collected) taken.Add(b.t.position - Vector3.up * babyHover);
        foreach (var m in mirages)
            if (!m.collected && m.active) taken.Add(m.t.position - Vector3.up * babyHover);

        Vector3 womanPosition = ghost != null ? ghost.position : Vector3.zero;
        Vector3 point;
        if (!PickBabyPoint(womanPosition, taken, out point)) return;

        free.collected = false;
        free.live = true;
        free.t.position = point + Vector3.up * babyHover;

        var catchable = free.t.GetComponent<CatchableGhost>();
        if (catchable != null) catchable.ResetCatch();

        // shown at once while the night is on; when the woman is away (she caught the player) it shows with the others
        bool shown = state != State.Inactive && state != State.Respawning;
        SetBabyVisible(free, shown);
        FaceBaby(free);
        Debug.Log("[Ghost] A new Orb '" + free.t.name + "' appears at " + point.ToString("F1"));
    }

    // True while the big ghost is out in the house (used by the ghost detector device)
    public bool GhostShown => state == State.Waiting || state == State.Patrolling || state == State.Scare || state == State.Chasing;

    // Every baby this manager controls (used by the gun)
    public List<Transform> GetBabies()
    {
        EnsureBabies();
        var list = new List<Transform>();
        foreach (var b in babies) list.Add(b.t);
        foreach (var m in mirages) list.Add(m.t);
        return list;
    }

    // Called by GhostGun when the player takes a baby. Returns false if this spawner doesn't manage it
    public bool CollectBaby(Transform baby)
    {
        // A Mirage just disappears (finding it does not count for the babies)
        foreach (var m in mirages)
        {
            if (m.t != baby) continue;
            m.collected = true;
            SetBabyVisible(m, false);
            AgentStats.AddCapture();
            if (onBabyCollected != null) onBabyCollected(baby);
            return true;
        }

        foreach (var b in babies)
        {
            if (b.t != baby) continue;
            SetPrompt(false);
            OnBabySeen(b);
            AgentStats.AddCapture();
            if (onBabyCollected != null) onBabyCollected(baby);
            return true;
        }
        return false;
    }

    public void ShowBabyPrompt(bool show, string message)
    {
        promptVisible = show;
        activePrompt = message;

        // The HUD draws the prompt (HudPrompt): the old centre text stays hidden
        if (show) HudPrompt.Show(message);
        if (babyPromptText != null) babyPromptText.gameObject.SetActive(false);
    }

    void SetPrompt(bool show)
    {
        ShowBabyPrompt(show, babyPromptMessage);
    }

    void OnGUI()
    {
        if (!promptVisible || babyPromptText != null || HudPrompt.Drawing) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(Screen.height * 0.04f),
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.7f, Screen.width, Screen.height * 0.08f), activePrompt, style);
    }

    void SetBabyVisible(BabyEntry b, bool visible)
    {
        b.active = visible;
        foreach (var r in b.renderers)
            if (r != null) r.enabled = visible;
    }

    void SetBabiesVisible(bool visible)
    {
        foreach (var b in babies)
            SetBabyVisible(b, visible);
        if (!visible)
            foreach (var m in mirages) SetBabyVisible(m, false);
        if (!visible) SetPrompt(false);
    }

    void Deactivate()
    {
        state = State.Inactive;
        currentIndex = -1;
        patrolIndex = 0;
        patrolDir = 1;
        SetWalking(false);
        SetRunning(false);
        SetGhostVisible(false);
        SetBabiesVisible(false);
        ReleaseLamps();
        if (singAudio != null) singAudio.Stop();
        if (chaseAudio != null) chaseAudio.Stop();
        if (jumpScareAudio != null) jumpScareAudio.Stop();
    }

    bool IsGhostHour(float hour)
    {
        if (appearHour <= hourEnd)
            return hour >= appearHour && hour < hourEnd;
        return hour >= appearHour || hour < hourEnd;
    }

    void FacePlayer()
    {
        if (player == null) return;

        Vector3 dir = player.position - ghost.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        ghost.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, rotationOffsetY, 0f);
    }

    void SetRunning(bool running)
    {
        if (ghostAnimator == null) return;

        if (!string.IsNullOrEmpty(runBoolParameter))
        {
            foreach (var p in ghostAnimator.parameters)
            {
                if (p.name == runBoolParameter && p.type == AnimatorControllerParameterType.Bool)
                {
                    ghostAnimator.SetBool(runBoolParameter, running);
                    return;
                }
            }
        }

        // Fallback if the bool parameter doesn't exist: play the states by name
        string state = running ? runStateName : idleStateName;
        if (!string.IsNullOrEmpty(state))
            ghostAnimator.CrossFade(state, 0.1f);
    }

    // ===================== PATHWAY =====================

    List<Transform> BuildPath()
    {
        var list = new List<Transform>();
        if (pathPoints != null)
            foreach (var p in pathPoints)
                if (p != null) list.Add(p);

        if (list.Count == 0 && pathRoot != null)
            foreach (Transform c in pathRoot)
                list.Add(c);

        return list;
    }

    List<Transform> GetPath()
    {
        if (pathCache == null) pathCache = BuildPath();
        return pathCache;
    }

    // True once the game clock passed "Path Start Hour" (counted from "Hour Start", so it also works across midnight)
    bool PathActive(float hour)
    {
        if (!usePath) return false;
        float elapsed = (hour - appearHour + 24f) % 24f;
        float startElapsed = (walkHour - appearHour + 24f) % 24f;
        return elapsed >= startElapsed;
    }

    void StartPatrol()
    {
        var path = GetPath();

        state = State.Patrolling;
        patrolIndex = 1;
        patrolDir = 1;
        wanderCorners = null;
        patrolWaitUntil = Time.time + waypointPause;

        // a new walk: every place is still to be visited, except the one she starts in
        placeBag.Clear();
        RefillPlaceBag();
        lastPlace = currentIndex;
        placeBag.Remove(currentIndex);

        Vector3 startPos;
        if (randomWander)
        {
            startPos = ghost.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(startPos, out hit, 5f, NavMesh.AllAreas))
                startPos = hit.position;
            else
                Debug.LogWarning("[Ghost] Ghost spawn point is not on the NavMesh. Move the Spwan Point onto the baked floor.");
        }
        else
        {
            startPos = path[0].position;
        }

        ghost.position = startPos;
        SetGhostVisible(true);
        SetRunning(false);

        ReleaseLamps();
        PatrolLamp(startPos);
        foreach (var lamp in ScaryLamp.All)
            if (!offLamps.Contains(lamp))
                lamp.TriggerGlitch();

        // the singing time is over: she hunts (her own song stops, the random sounds take over)
        if (singAudio != null) singAudio.Stop();

        Debug.Log(randomWander
            ? "[Ghost] Started random wandering"
            : "[Ghost] Started walking the pathway (" + path.Count + " waypoints)");
    }

    // ===================== SONGS =====================

    // Her own sound plus the songs in the list and in Resources/GhostSongs
    void CollectSongs()
    {
        songs.Clear();
        if (walkSongs != null)
            foreach (var clip in walkSongs)
                if (clip != null && !songs.Contains(clip)) songs.Add(clip);

        foreach (var clip in Resources.LoadAll<AudioClip>("GhostSongs"))
            if (clip != null && !songs.Contains(clip)) songs.Add(clip);

        if (songs.Count > 0) Debug.Log("[Ghost] " + songs.Count + " song(s) for the walk");
    }

    // A random song (not the one that just played). Without songs, her own sound
    void PlayWalkSong()
    {
        if (singAudio == null) return;
        if (songs.Count == 0)
        {
            if (!singAudio.isPlaying) singAudio.Play();
            return;
        }

        int next = Random.Range(0, songs.Count);
        if (songs.Count > 1 && next == lastSong) next = (next + 1 + Random.Range(0, songs.Count - 1)) % songs.Count;
        lastSong = next;

        singAudio.Stop();
        singAudio.clip = songs[next];
        singAudio.loop = false;       // when it ends, the next one is chosen (see Patrol)
        singAudio.Play();
        Debug.Log("[Ghost] She sings: " + songs[next].name);
    }

    // Her own sound again (where she stands and waits)
    void PlayOriginalSing()
    {
        if (singAudio == null) return;
        if (originalSing != null && (singAudio.clip != originalSing || songs.Count > 0))
        {
            singAudio.Stop();
            singAudio.clip = originalSing;
            singAudio.loop = originalLoop;
        }
        if (!singAudio.isPlaying) singAudio.Play();
    }

    void Patrol()
    {
        var path = GetPath();
        if (!randomWander && path.Count < 2)
        {
            state = State.Waiting;
            return;
        }

        if (Time.time >= noticeBlockedUntil && CanNoticePlayer())
        {
            if (Time.time - lastScareTime >= jumpScareCooldown)
            {
                BeginScare();
            }
            else
            {
                // Jump scare happened recently: skip it and chase right away
                SetWalking(false);
                if (singAudio != null) singAudio.Stop();
                StartChase();
            }
            return;
        }

        if (Time.time < patrolWaitUntil)
        {
            SetWalking(false);
            return;
        }

        if (randomWander)
        {
            WanderMove();
            return;
        }

        SetWalking(true);

        Transform target = path[patrolIndex];

        Vector3 flat = target.position - ghost.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
            ghost.rotation = Quaternion.LookRotation(flat) * Quaternion.Euler(0f, rotationOffsetY, 0f);

        ghost.position = Vector3.MoveTowards(ghost.position, target.position, patrolSpeed * Time.deltaTime);

        if ((ghost.position - target.position).sqrMagnitude > 0.0004f)
            return;

        // Arrived at this waypoint
        PatrolLamp(target.position);
        patrolWaitUntil = Time.time + waypointPause;
        patrolIndex += patrolDir;

        if (patrolIndex >= path.Count || patrolIndex < 0)
        {
            switch (pathMode)
            {
                case PathMode.Once:
                    FinishPatrol();
                    break;

                case PathMode.Loop:
                    patrolIndex = patrolDir > 0 ? 0 : path.Count - 1;
                    break;

                case PathMode.PingPong:
                    patrolDir = -patrolDir;
                    patrolIndex += patrolDir * 2;
                    break;
            }
        }
    }

    void WanderMove()
    {
        if (wanderCorners == null || wanderCornerIndex >= wanderCorners.Length)
        {
            if (!PickWanderDestination())
            {
                SetWalking(false);
                patrolWaitUntil = Time.time + 1f;
                return;
            }
        }

        SetWalking(true);

        Vector3 target = wanderCorners[wanderCornerIndex];

        Vector3 flat = target - ghost.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
            ghost.rotation = Quaternion.LookRotation(flat) * Quaternion.Euler(0f, rotationOffsetY, 0f);

        ghost.position = Vector3.MoveTowards(ghost.position, target, patrolSpeed * Time.deltaTime);

        if ((ghost.position - target).sqrMagnitude > 0.0016f)
            return;

        wanderCornerIndex++;
        if (wanderCornerIndex >= wanderCorners.Length)
        {
            // Reached the random spot: stand a moment, then pick another
            wanderCorners = null;
            SetWalking(false);
            PatrolLamp(ghost.position);
            patrolWaitUntil = Time.time + waypointPause;
        }
    }

    // All places once, in a random order. The first of the new round is never the one she just visited.
    void RefillPlaceBag()
    {
        placeBag.Clear();
        if (spawnPoints == null) return;

        for (int i = 0; i < spawnPoints.Length; i++)
            if (spawnPoints[i] != null) placeBag.Add(i);

        for (int i = placeBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int t = placeBag[i]; placeBag[i] = placeBag[j]; placeBag[j] = t;
        }

        if (placeBag.Count > 1 && placeBag[0] == lastPlace)
        {
            int j = Random.Range(1, placeBag.Count);
            int t = placeBag[0]; placeBag[0] = placeBag[j]; placeBag[j] = t;
        }
    }

    // The next place of the round: she does not go back to a place before she has seen all the others.
    // Without spawn points (or when none can be reached) she wanders to random spots as before.
    bool PickWanderDestination()
    {
        if (spawnPoints != null && spawnPoints.Length > 1)
        {
            var navPath = new NavMeshPath();

            for (int attempt = 0; attempt < spawnPoints.Length * 2; attempt++)
            {
                if (placeBag.Count == 0) RefillPlaceBag();
                if (placeBag.Count == 0) break;

                int index = placeBag[0];
                placeBag.RemoveAt(0);

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(spawnPoints[index].position, out hit, 3f, NavMesh.AllAreas)) continue;

                // already standing there: that place counts as seen
                Vector3 flat = hit.position - ghost.position;
                if (flat.sqrMagnitude < 9f) { lastPlace = index; continue; }

                if (!NavMesh.CalculatePath(ghost.position, hit.position, NavMesh.AllAreas, navPath)) continue;
                if (navPath.status != NavMeshPathStatus.PathComplete || navPath.corners.Length < 2) continue;

                wanderCorners = navPath.corners;
                wanderCornerIndex = 1;
                lastPlace = index;
                return true;
            }
        }

        return PickRandomWanderDestination();
    }

    // Random spot on the NavMesh that is really reachable (walls/furniture are routed around)
    bool PickRandomWanderDestination()
    {
        var navPath = new NavMeshPath();

        for (int i = 0; i < 12; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 candidate = ghost.position + new Vector3(dir.x, 0f, dir.y) * Random.Range(wanderMinDistance, wanderRadius);

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(ghost.position, hit.position, NavMesh.AllAreas, navPath)) continue;
            if (navPath.status != NavMeshPathStatus.PathComplete || navPath.corners.Length < 2) continue;

            wanderCorners = navPath.corners;
            wanderCornerIndex = 1;
            return true;
        }
        return false;
    }

    void FinishPatrol()
    {
        SetWalking(false);
        SetGhostVisible(false);
        ReleaseLamps();
        state = State.Respawning;
        respawnTime = Time.time + respawnDelay;
    }

    void PatrolLamp(Vector3 position)
    {
        if (!pathLampsFollow) return;

        ReleaseLamps();
        var near = NearestLamp(position);
        if (near != null)
        {
            near.SetForcedOff(true);
            offLamps.Add(near);
        }
    }

    // The ghost notices the player when:
    //  - the player is close (Notice Distance) with a clear line of sight, OR
    //  - the player LOOKS at the ghost from farther away (Player Sees Ghost Distance), also with a clear line of sight.
    // Walls, floors and closed doors block both.
    bool CanNoticePlayer()
    {
        if (player == null || HideSystem.PlayerHidden) return false;

        Vector3 from = ghost.position + Vector3.up * 1.2f;
        Vector3 to = playerCam != null ? playerCam.transform.position : player.position + Vector3.up;
        float dist = (to - from).magnitude;

        bool close = noticeDistance > 0f && dist <= noticeDistance;
        bool playerLooksAtGhost = playerSeesGhostDistance > 0f && dist <= playerSeesGhostDistance && PlayerLooksAt(from);

        string blocker = null;
        bool clear = true;
        if ((close || playerLooksAtGhost) && noticeNeedsLineOfSight)
            clear = HasLineOfSight(from, to, out blocker);

        if (debugNotice && Time.time >= nextDebugTime)
        {
            nextDebugTime = Time.time + 1f;
            Debug.Log("[Ghost debug] state=" + state + "  distance=" + dist.ToString("F1") + "m  close(<" + noticeDistance +
                      ")=" + close + "  playerLooksAtGhost(<" + playerSeesGhostDistance + ")=" + playerLooksAtGhost +
                      "  lineOfSight=" + (clear ? "clear" : "BLOCKED by '" + blocker + "'"));
        }

        if (!close && !playerLooksAtGhost) return false;
        return clear;
    }

    bool PlayerLooksAt(Vector3 point)
    {
        if (playerCam == null) return false;
        Vector3 toPoint = point - playerCam.transform.position;
        return Vector3.Angle(playerCam.transform.forward, toPoint) <= playerSeesGhostAngle;
    }

    bool HasLineOfSight(Vector3 from, Vector3 to, out string blocker)
    {
        blocker = null;
        Vector3 dir = to - from;
        var hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(ghost) || h.transform.IsChildOf(player)) continue;
            blocker = h.collider.name;
            return false;
        }
        return true;
    }

    void SetWalking(bool walking)
    {
        if (walking == isWalking) return;
        isWalking = walking;

        if (hasWalkParam && ghostAnimator != null)
            ghostAnimator.SetBool(walkBoolParameter, walking);
    }

    // Draws the pathway in the Scene view: green = start, red = end
    void OnDrawGizmos()
    {
        if (!usePath) return;

        var path = BuildPath();
        for (int i = 0; i < path.Count; i++)
        {
            Gizmos.color = i == 0 ? Color.green : (i == path.Count - 1 ? Color.red : Color.yellow);
            Gizmos.DrawSphere(path[i].position, 0.2f);

            if (i > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(path[i - 1].position, path[i].position);
            }
        }

        if (pathMode == PathMode.Loop && path.Count > 2)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f);
            Gizmos.DrawLine(path[path.Count - 1].position, path[0].position);
        }
    }

    SpawnLampLink GetLink(Transform point)
    {
        if (lampLinks == null) return null;
        foreach (var link in lampLinks)
            if (link != null && link.spawnPoint == point)
                return link;
        return null;
    }

    List<ScaryLamp> GetLinkedLamps(Transform point)
    {
        var result = new List<ScaryLamp>();
        var link = GetLink(point);
        if (link == null || link.lampsToTurnOff == null) return result;

        foreach (var lamp in link.lampsToTurnOff)
            if (lamp != null) result.Add(lamp);
        return result;
    }

    ScaryLamp NearestLamp(Vector3 pos)
    {
        ScaryLamp best = null;
        float bestDist = float.MaxValue;
        foreach (var lamp in ScaryLamp.All)
        {
            float d = (lamp.LightPosition - pos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = lamp;
            }
        }
        return best;
    }

    void ReleaseLamps()
    {
        foreach (var lamp in offLamps)
            if (lamp != null) lamp.SetForcedOff(false);
        offLamps.Clear();
    }

    void SetGhostVisible(bool visible)
    {
        if (ghostRenderers == null) return;
        foreach (var r in ghostRenderers)
            if (r != null) r.enabled = visible;
    }
}
