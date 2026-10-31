using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The TRAINING (like the training ground of a shooter): one clean training room (a floor, four walls and a ceiling, built by this script high
// above the house) with no ghost and no damage. It has 4 lessons, one after the other; each lesson brings in only the things it uses
// (the generator, the Drop Table, the NPC, the stand-by woman) and puts the rest far away. Every lesson has a few small tasks with an instruction:
//   ROOM 1  GUN + MEP         catch an Orb with the gun, walk it to the generator, take the MEP it makes
//   ROOM 2  FARADAY CAGE      drop an empty cage, fill it with the MEP, put it on the Drop Table, the NPC checks it
//   ROOM 3  RADAR + SOLID     take out the scanner and switch it on, then the woman (Solid) comes: hide
//   ROOM 4  NPC               talk to the NPC and spend a point on an upgrade
// A glowing marker shows where to go. It runs in the scene "TutorialScene" (Tools > Ghost > Create Tutorial Scene makes it),
// or in the normal game scene when the TUTORIAL button was used and there is no TutorialScene in the build.
// N skips a task, M goes back to the menu.
public class TutorialMode : MonoBehaviour
{
    public const string SceneName = "TutorialScene";
    public const string GameSceneName = "SampleScene";
    public const string MenuSceneName = "IntroScene";

    public static bool Requested;
    public static bool Active { get; private set; }

    // True while a page waits for Enter: the player can not move, look, shoot or use anything until the page is read (see UiLock)
    public static bool Frozen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Requested = false;
        Active = false;
        Frozen = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate();
    }

    static void TryCreate()
    {
        bool wanted = SceneManager.GetActiveScene().name == SceneName || Requested;
        if (!wanted || FindFirstObjectByType<TutorialMode>() != null) return;

        Debug.Log("[Tutorial] Training starts in scene " + SceneManager.GetActiveScene().name);
        Requested = false;
        new GameObject("Tutorial Mode (auto)").AddComponent<TutorialMode>();
    }

    // Opens the training: its own scene when it is in the build, otherwise the game scene in training mode
    public static void Load()
    {
        string path = "Assets/Scenes/" + SceneName + ".unity";
        if (SceneUtility.GetBuildIndexByScenePath(path) >= 0)
        {
            SceneManager.LoadScene(SceneName);
        }
        else
        {
            Requested = true;
            SceneManager.LoadScene(GameSceneName);
        }
    }

    // ---------------------------------------------------------------- the tasks

    class Step
    {
        public string pc;
        public string phone;
        public Func<bool> done;
        public Action begin;
        public Func<Vector3?> target;
        public float timeout;
        public int lesson;
        public bool intro;     // the page that opens a lesson: it takes the player to the room
        public bool needsEnter;   // a page: only Enter (or the CONTINUE button on a phone) goes on, and the player is frozen until then
    }

    class StepRow
    {
        public int step;
        public TMP_Text number, text;
        public GameObject root;
    }

    class Lesson
    {
        public string name;        // "How to use the Proton Emitter Gun"
        public string blurb;       // the green line under the title
        public bool ok = true;     // false when the room of the lesson can not be made (something is missing in the scene): it is skipped
        public int first;          // index of its first step
        public int count;          // steps, the card included
    }

    private readonly List<Step> steps = new List<Step>();
    private readonly List<Lesson> lessons = new List<Lesson>();
    private int index = -1;
    private float stepStart;
    private bool finished;

    private PlayerController playerController;
    private Transform player;
    private GhostSpawner spawner;
    private WallClockController clock;
    private GhostGun gun;
    private GhostDevice scanner;
    private BabyGhostGenerator generator;

    // what the player did
    private float moved;
    private float turned;
    private Vector3 lastPosition;
    private float lastYaw;
    private bool backpackSeen;
    private bool orbCollected;
    private bool upgradeSeen;
    private Transform orb;
    private bool drillStarted;
    private float drillStartTime;
    private float hiddenSince = -1f;
    private bool capsuleSeen;
    private int pointsAtStart;
    private int mepAtStart;
    private float groundOffset = 0.05f;     // how high the player's origin is above the floor

    // the things the lessons use
    private InvItemData emptyCage, filledCage, mep;
    private DropTable dropTable;

    // the frame of the room: the player can not leave it (a coloured wall shows where it is)
    private bool zoneOn;
    private Vector2 zoneMin, zoneMax;
    private GameObject zoneObject;
    private readonly List<LineRenderer> zoneLines = new List<LineRenderer>();
    private Color zoneColor = Color.cyan;
    private Transform radarGhost;                // the stand-by woman of the radar room (a plain model, no script)

    private bool hideOk;

    // the pointer
    private Canvas screenCanvas;
    private RectTransform pinRoot, edgeRoot;
    private Image pinRing, pinCore, pinChevron, edgePlate, edgeArrow;
    private TMP_Text pinLabel, edgeLabel;
    static readonly Color[] RoomColors =
    {
        new Color(0.25f, 0.85f, 1f, 1f),    // room 1: cyan
        new Color(1f, 0.70f, 0.20f, 1f),    // room 2: amber
        new Color(0.75f, 0.45f, 1f, 1f),    // room 3: violet
        new Color(0.35f, 1f, 0.50f, 1f),    // room 4: green
    };
    private readonly Dictionary<DoorInteraction, bool> doors = new Dictionary<DoorInteraction, bool>();

    // the screen
    // the page in the middle of the screen (the start of a lesson, and the last words)
    private GameObject card;
    private TMP_Text headingText;
    private TMP_Text blurbText;
    private RectTransform stepsBox;
    private GameObject promptRow;
    private TMP_Text pressText, enterLabel, toContinueText;

    // the small panel at the top left: the task now, one by one
    private TMP_Text smallHeading;
    private TMP_Text currentNo, currentText;
    private TMP_Text frameText;
    private TMP_Text hintText;
    private int shownLesson = -1;
    private readonly List<StepRow> rows = new List<StepRow>();
    private bool continueClicked;
    private bool radarNear;
    private int mepBefore;
    private GameObject panel;
    private GameObject finishPanel;
    private GameObject phoneButtons;
    private Transform marker;
    private Transform markerBeam;
    private bool markerReady;

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.78f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.70f, 0.08f, 0.12f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    bool Phone { get { return DeviceDetection.IsMobile(); } }

    IEnumerator Start()
    {
        Active = true;

        // one frame later: the other scripts have finished their own Start (the player may come a little later still)
        yield return null;

        playerController = FindFirstObjectByType<PlayerController>();
        for (int wait = 0; playerController == null && wait < 300; wait++)
        {
            yield return null;
            playerController = FindFirstObjectByType<PlayerController>();
        }
        player = playerController != null ? playerController.transform : null;
        spawner = FindFirstObjectByType<GhostSpawner>();
        clock = FindFirstObjectByType<WallClockController>();
        gun = GhostGun.Instance;
        scanner = FindFirstObjectByType<GhostDevice>();
        if (scanner != null) scanner.freeBattery = true;     // the clock stands still here: the battery would never be charged again
        generator = FindFirstObjectByType<BabyGhostGenerator>();

        if (player == null)
        {
            Debug.LogWarning("[Tutorial] No player (PlayerController) found: the training can not run in this scene");
            enabled = false;
            yield break;
        }
        Debug.Log("[Tutorial] Player found, building the task list");

        // a quiet house: daytime (the NPC is in), the clock stands still, no ghost, no damage
        if (clock != null)
        {
            clock.SetGameTime(10, 0);
            clock.realSecondsPerGameHour = 1000000f;
        }
        if (spawner != null)
        {
            spawner.TutorialPrepare();
            spawner.onBabyCollected += OnBabyCollected;
        }

        foreach (var door in FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None))
            doors[door] = door.IsOpen();

        lastPosition = player.position;
        lastYaw = player.eulerAngles.y;

        dropTable = FindFirstObjectByType<DropTable>();
        FindItems();

        // how high the player's origin is above the floor (to put the player on a floor spot)
        if (NavMesh.SamplePosition(player.position, out NavMeshHit under, 3f, NavMesh.AllAreas))
            groundOffset = Mathf.Max(0f, player.position.y - under.position.y);

        BuildSteps();
        BuildScreen();
        roomOk = BuildRoom();
        NextStep();
    }

    // The cage and the MEP are found from the backpack's starting items: the empty cage knows what fills it
    void FindItems()
    {
        var inv = Inventory.Instance;
        if (inv == null || inv.startingItems == null) return;

        foreach (var start in inv.startingItems)
        {
            if (start == null || start.item == null || start.item.combineWith == null) continue;
            emptyCage = start.item;
            mep = start.item.combineWith;
            filledCage = start.item.combineResult;
            break;
        }
    }

    void OnDestroy()
    {
        Active = false;
        Frozen = false;
        if (scanner != null) scanner.freeBattery = false;
        GhostDevice.OverrideGhost = null;
        if (spawner != null) spawner.onBabyCollected -= OnBabyCollected;
    }

    void OnBabyCollected(Transform baby)
    {
        orbCollected = true;
    }

    // ---------------------------------------------------------------- the list

    // Enter (or the CONTINUE button on a phone) is pressed. It is used up here.
    bool EnterPressed()
    {
        bool key = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!key && !continueClicked) return false;

        continueClicked = false;
        return true;
    }

    // A lesson opens with a page (title + the green line): nothing else happens until Enter is pressed
    void AddLesson(string title, string blurb, Action enter)
    {
        var lesson = new Lesson { name = title, blurb = blurb, first = steps.Count };
        lessons.Add(lesson);

        steps.Add(new Step
        {
            lesson = lessons.Count - 1,
            intro = true,
            needsEnter = true,
            pc = blurb,
            phone = blurb,
            begin = enter,
            done = EnterPressed
        });
    }

    void AddStep(string pc, string phone, Func<bool> done, Func<Vector3?> target = null, Action begin = null)
    {
        steps.Add(new Step
        {
            lesson = lessons.Count - 1,
            pc = pc,
            phone = phone,
            done = done,
            target = target,
            begin = begin
        });
    }

    // The last line of the training: a page too
    void AddEnterStep(string text)
    {
        steps.Add(new Step
        {
            lesson = lessons.Count - 1,
            needsEnter = true,
            pc = text,
            phone = text,
            done = EnterPressed
        });
    }

    void BuildSteps()
    {
        // ---------------------------------------------------------------- LESSON 1: the gun and the MEP
        AddLesson("How to use the Proton Emitter Gun",
            "Guide:  W A S D move you. The mouse looks around.",
            EnterGunRoom);
        AddStep("Take out the gun with 1", "Take out the gun: tap it on the bottom bar",
            () => gun != null && gun.IsHeld);
        AddStep("Aim at the glowing Orb and hold Left Click until it is pulled", "Aim at the glowing Orb and tap the hand button",
            () => gun != null && gun.HasBaby,
            () => orb != null ? orb.position + Vector3.up * 1.7f : (Vector3?)null);
        AddStep("Keep the Orb pulled and walk it to the generator", "Keep the pull on and walk the Orb to the generator",
            () => orbCollected,
            () => generator != null ? generator.PutPoint + Vector3.up * 1.2f : (Vector3?)null);
        AddStep("The generator is making a MEP. Wait a moment", "The generator is making a MEP. Wait a moment",
            () => GhostCapsule.All.Count > 0,
            () => generator != null ? generator.PutPoint + Vector3.up * 1.2f : (Vector3?)null);
        // done only when the MEP is really taken: one more MEP in the backpack, or the capsule was there and is gone now
        AddStep("Look at the MEP and press E to take it", "Look at the MEP and tap the hand button",
            () =>
            {
                if (GhostCapsule.All.Count > 0) capsuleSeen = true;
                var inv = Inventory.Instance;
                if (mep != null && inv != null && inv.GetCount(mep.Id) > mepBefore) return true;
                return capsuleSeen && GhostCapsule.All.Count == 0;
            },
            () => GhostCapsule.All.Count > 0 && GhostCapsule.All[0] != null ? GhostCapsule.All[0].transform.position + Vector3.up * 0.5f : (Vector3?)null,
            () =>
            {
                capsuleSeen = false;
                mepBefore = mep != null && Inventory.Instance != null ? Inventory.Instance.GetCount(mep.Id) : 0;
            });

        // ---------------------------------------------------------------- LESSON 2: the Faraday Cage
        AddLesson("How to use the Faraday Cage",
            "Guide:  Hand the souls you collected in a Faraday Cage to the GCC worker and you get points.",
            EnterCageRoom);
        AddStep("Open the backpack with B and click the Empty Faraday Cage to hold it",
            "Open the BAG and tap the Empty Faraday Cage to hold it",
            () => emptyCage != null && Inventory.Instance != null && Inventory.Instance.HeldId == emptyCage.Id);
        AddStep("Left click to drop the cage on the floor", "Tap DROP to drop the cage on the floor",
            () => PlacedCage.All.Count > 0);
        AddStep("Look at the cage and press F to put the MEP inside", "Look at the cage and tap the hand button to put the MEP inside",
            () => CageFilled(),
            () => PlacedCage.All.Count > 0 && PlacedCage.All[0] != null ? PlacedCage.All[0].transform.position + Vector3.up * 0.6f : (Vector3?)null);
        AddStep("The cage with the MEP is saved in your backpack. Open it and hold the filled cage",
            "The cage with the MEP is saved in your BAG. Open it and hold the filled cage",
            () => (filledCage != null && Inventory.Instance != null && Inventory.Instance.HeldId == filledCage.Id) || DropTable.AnyChecking);
        AddStep("Go to the Drop Table and press F to put the cage on it", "Go to the Drop Table and tap the hand button to put the cage on it",
            () => DropTable.AnyChecking,
            () => dropTable != null ? TablePoint() : (Vector3?)null);
        AddStep("The NPC checks the cage. Wait for your point", "The NPC checks the cage. Wait for your point",
            () => UpgradeSystem.Instance != null && UpgradeSystem.Instance.TotalPoints > pointsAtStart,
            () => NpcWorker.Instance != null ? NpcWorker.Instance.transform.position + Vector3.up * 2.2f : (Vector3?)null);

        // ---------------------------------------------------------------- LESSON 3: the scanner
        AddLesson("How to use the EMF-Scanner",
            "Guide:  The EMF Scanner can detect the aura of a dangerous spirit.",
            EnterRadarRoom);
        AddStep("Take out the scanner with 2", "Take out the scanner: tap it on the bottom bar",
            () => scanner != null && scanner.IsShown);
        // switched on AND the player went near her (the scanner can not run out of battery in the training)
        AddStep("Press O to switch the scanner on. The lights show how near a ghost is. Walk toward the woman",
            "Tap the power button to switch the scanner on. The lights show how near a ghost is. Walk toward the woman",
            () =>
            {
                bool near = scanner != null && scanner.IsShown && scanner.IsPowered && GhostWithin(3.5f);
                if (near) radarNear = true;
                return near;
            },
            () => radarGhost != null ? radarGhost.position + Vector3.up * 2.3f : (Vector3?)null);
        // only after the player was near her: walking away
        AddStep("Now walk away from her: the lights go out", "Now walk away from her: the lights go out",
            () => radarNear && !GhostWithin(6.5f));

        // ---------------------------------------------------------------- LESSON 4: the NPC
        AddLesson("How to upgrade your tools",
            "Guide:  To catch stronger spirits you must upgrade your tools.",
            EnterNpcRoom);
        AddStep("Walk up to the NPC", "Walk up to the NPC",
            () => player != null && UpgradePanel.NearNpc(player, 4.5f),
            () => NpcWorker.Instance != null ? NpcWorker.Instance.transform.position + Vector3.up * 2.2f : (Vector3?)null);
        AddStep("Press U to open the upgrades", "Look at the NPC and tap the hand button to open the upgrades",
            () => UpgradePanel.IsOpen);
        AddStep("Pick an item with the arrows and press Enter to spend your point", "Pick an item with the arrows and tap UPGRADE to spend your point",
            () => UpgradeSystem.Instance != null && UpgradeSystem.Instance.Points <= 0);
        AddStep("Close the upgrades with U", "Close the upgrades",
            () => !UpgradePanel.IsOpen);
        AddEnterStep("That is everything an agent needs to know. May you complete the mission. Good luck, Agent.");

        // count the steps of each lesson
        for (int i = 0; i < lessons.Count; i++)
        {
            int count = 0;
            foreach (var step in steps) if (step.lesson == i) count++;
            lessons[i].count = count;
        }
    }

    // ---------------------------------------------------------------- the rooms

    // Takes the player to a floor spot, facing a point. Everything in the hands is put away first.
    void GoTo(Vector3 spot, Vector3 look)
    {
        if (Hotbar.Instance != null) Hotbar.Instance.Clear();
        if (Inventory.Instance != null) Inventory.Instance.Unequip();

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.position = spot + Vector3.up * groundOffset;
        Vector3 flat = look - spot;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(flat);

        if (controller != null) controller.enabled = true;

        lastPosition = player.position;
        lastYaw = player.eulerAngles.y;
    }

    // The middle of the Drop Table's top (where a cage is put)
    Vector3 TablePoint()
    {
        if (dropTable == null) return player.position;
        if (dropTable.placePoint != null) return dropTable.placePoint.position + Vector3.up * 0.25f;
        return dropTable.transform.position + Vector3.up * 1.0f;
    }

    bool GhostWithin(float distance)
    {
        if (radarGhost == null || player == null) return false;
        Vector3 a = player.position, b = radarGhost.position;
        a.y = b.y = 0f;
        return Vector3.Distance(a, b) < distance;
    }

    static bool Near(Vector3 a, Vector3 b, float distance)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b) < distance;
    }

    // ---------------------------------------------------------------- the training room

    private const float RoomWidth = 18f, RoomDepth = 14f, RoomHeight = 6.5f;
    private float roomFloor;
    private Vector3 roomBase;                      // the middle of the floor
    private Vector3 parkBase;                      // far away: where the things of the other lessons wait
    private bool roomOk;

    // A point of the room: x = right, z = forward, from the middle of the floor
    Vector3 RoomPoint(float x, float z)
    {
        return new Vector3(roomBase.x + x, roomFloor, roomBase.z + z);
    }

    // The clean room: a floor, four walls and a ceiling, high above the house, in the house's own wood and wall look
    bool BuildRoom()
    {
        float floorOfHouse = player.position.y - groundOffset;
        roomFloor = floorOfHouse + 400f;
        roomBase = new Vector3(player.position.x, roomFloor, player.position.z);
        parkBase = new Vector3(player.position.x, floorOfHouse - 3000f, player.position.z);

        PickMaterials(out Material floorMaterial, out Material wallMaterial);
        if (floorMaterial == null || wallMaterial == null)
        {
            Debug.LogWarning("[Tutorial] No material found for the training room");
            return false;
        }

        zoneObject = new GameObject("Training Room");
        zoneObject.transform.SetParent(transform, false);
        var parent = zoneObject.transform;

        float t = 0.5f;   // thickness
        // floor and ceiling
        MakeBox(parent, "Floor", new Vector3(roomBase.x, roomFloor - t * 0.5f, roomBase.z), new Vector3(RoomWidth + 2f * t, t, RoomDepth + 2f * t), floorMaterial, 2.5f);
        MakeBox(parent, "Ceiling", new Vector3(roomBase.x, roomFloor + RoomHeight + t * 0.5f, roomBase.z), new Vector3(RoomWidth + 2f * t, t, RoomDepth + 2f * t), wallMaterial, 3f);
        // four walls
        float cy = roomFloor + RoomHeight * 0.5f;
        MakeBox(parent, "Wall North", new Vector3(roomBase.x, cy, roomBase.z + RoomDepth * 0.5f + t * 0.5f), new Vector3(RoomWidth + 2f * t, RoomHeight, t), wallMaterial, 3f);
        MakeBox(parent, "Wall South", new Vector3(roomBase.x, cy, roomBase.z - RoomDepth * 0.5f - t * 0.5f), new Vector3(RoomWidth + 2f * t, RoomHeight, t), wallMaterial, 3f);
        MakeBox(parent, "Wall East", new Vector3(roomBase.x + RoomWidth * 0.5f + t * 0.5f, cy, roomBase.z), new Vector3(t, RoomHeight, RoomDepth), wallMaterial, 3f);
        MakeBox(parent, "Wall West", new Vector3(roomBase.x - RoomWidth * 0.5f - t * 0.5f, cy, roomBase.z), new Vector3(t, RoomHeight, RoomDepth), wallMaterial, 3f);

        // warm light: one big in the middle, four around
        AddRoomLight(parent, new Vector3(0f, RoomHeight - 1.2f, 0f), 3f, 18f);
        foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            AddRoomLight(parent, new Vector3(corner.x * RoomWidth * 0.28f, RoomHeight - 1.2f, corner.y * RoomDepth * 0.28f), 1.6f, 12f);

        // the coloured line around the floor (the colour tells the lesson)
        zoneMin = new Vector2(roomBase.x - RoomWidth * 0.5f, roomBase.z - RoomDepth * 0.5f);
        zoneMax = new Vector2(roomBase.x + RoomWidth * 0.5f, roomBase.z + RoomDepth * 0.5f);
        zoneOn = true;
        BuildFloorLine(parent);

        // what the lessons use is brought in one lesson at a time: until then it waits far away
        if (dropTable != null) GatherTableModels();
        ParkAll();

        Debug.Log("[Tutorial] The training room is built (" + RoomWidth + " x " + RoomDepth + " m) at height " + roomFloor.ToString("0"));
        return true;
    }

    // The house's own floor (under the player) and wall (the nearest tall thin one) materials
    void PickMaterials(out Material floorMaterial, out Material wallMaterial)
    {
        floorMaterial = wallMaterial = null;

        if (Physics.Raycast(player.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            var r = hit.collider.GetComponentInParent<MeshRenderer>();
            if (r == null) r = hit.collider.GetComponentInChildren<MeshRenderer>();
            if (r != null && r.sharedMaterial != null) floorMaterial = r.sharedMaterial;
        }

        float best = 30f;
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r == null || r.sharedMaterial == null) continue;
            Bounds b = r.bounds;
            if (b.size.y < 2.5f || Mathf.Min(b.size.x, b.size.z) > 0.6f || Mathf.Max(b.size.x, b.size.z) < 2f) continue;
            float d = Vector3.Distance(b.center, player.position);
            if (d < best) { best = d; wallMaterial = r.sharedMaterial; }
        }

        if (floorMaterial == null) floorMaterial = wallMaterial;
        if (wallMaterial == null) wallMaterial = floorMaterial;

        if (floorMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                floorMaterial = wallMaterial = new Material(shader);
                floorMaterial.color = new Color(0.25f, 0.2f, 0.17f, 1f);
            }
        }
    }

    // A box with a collider and a model. 'tile' = meters of one picture of the material
    void MakeBox(Transform parent, string name, Vector3 centre, Vector3 size, Material material, float tile)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = centre;
        box.transform.localScale = size;

        var r = box.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // the picture repeats every 'tile' meters instead of being stretched over the whole wall
        var block = new MaterialPropertyBlock();
        float sx = Mathf.Max(1f, Mathf.Max(size.x, size.z) / tile);
        float sy = Mathf.Max(1f, (size.y > 1f ? size.y : Mathf.Min(size.x, size.z)) / tile);
        var st = new Vector4(sx, sy, 0f, 0f);
        block.SetVector("_BaseMap_ST", st);
        block.SetVector("_MainTex_ST", st);
        r.SetPropertyBlock(block);
    }

    void AddRoomLight(Transform parent, Vector3 local, float intensity, float range)
    {
        var go = new GameObject("Room Light");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(roomBase.x + local.x, roomFloor + local.y, roomBase.z + local.z);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.86f, 0.68f);
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    void BuildFloorLine(Transform parent)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        float hx = RoomWidth * 0.5f - 0.15f, hz = RoomDepth * 0.5f - 0.15f, y = roomFloor + 0.03f;
        Vector3[] loop =
        {
            new Vector3(roomBase.x - hx, y, roomBase.z - hz), new Vector3(roomBase.x + hx, y, roomBase.z - hz),
            new Vector3(roomBase.x + hx, y, roomBase.z + hz), new Vector3(roomBase.x - hx, y, roomBase.z + hz),
            new Vector3(roomBase.x - hx, y, roomBase.z - hz),
        };
        var go = new GameObject("Floor Line");
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = loop.Length;
        line.SetPositions(loop);
        line.widthMultiplier = 0.09f;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = new Material(shader);
        line.startColor = line.endColor = zoneColor;
        zoneLines.Add(line);
    }

    void SetZoneColor(int room)
    {
        zoneColor = RoomColors[Mathf.Clamp(room, 0, RoomColors.Length - 1)];
        if (frameText != null) frameText.color = zoneColor;
    }

    // ---------------------------------------------------------------- bringing things in and putting them away

    // The size of a lesson object: its models (those near it, so a far child does not stretch it)
    Bounds BodyBounds(Transform root)
    {
        Vector3 centre = root.position;
        bool any = false;
        Bounds b = new Bounds(centre, Vector3.zero);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (Vector3.Distance(r.bounds.center, centre) > 6f) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Moves a lesson object so that it stands on the floor of the room, with the point 'anchor' (or the middle of its models) at 'target'.
    // Transforms that belong to it but are not its children (a cage spot, put positions) move with it.
    void PlaceOnFloor(Transform root, Vector3? anchor, Vector3 target, params Transform[] extras)
    {
        Bounds b = BodyBounds(root);
        Vector3 a = anchor ?? b.center;
        Vector3 delta = new Vector3(target.x - a.x, roomFloor - b.min.y, target.z - a.z);
        root.position += delta;
        foreach (var e in extras)
            if (e != null && !e.IsChildOf(root) && e != root) e.position += delta;
    }

    // The things of the other lessons wait far away, under the house
    void Park(Transform root, int slot, params Transform[] extras)
    {
        if (root == null) return;
        Bounds b = BodyBounds(root);
        Vector3 target = parkBase + Vector3.right * (60f * slot);
        Vector3 delta = target - b.center;
        root.position += delta;
        foreach (var e in extras)
            if (e != null && !e.IsChildOf(root) && e != root) e.position += delta;
    }

    Transform[] GeneratorExtras()
    {
        if (generator == null) return new Transform[0];
        var list = new List<Transform>();
        if (generator.putPosition != null) list.Add(generator.putPosition);
        if (generator.producePosition != null) list.Add(generator.producePosition);
        if (generator.extraPutPositions != null) list.AddRange(generator.extraPutPositions);
        return list.ToArray();
    }

    Transform[] TableExtras()
    {
        return dropTable != null && dropTable.placePoint != null ? new[] { dropTable.placePoint } : new Transform[0];
    }

    // The table's own model may be an object beside the Drop Table script: the model under the cage spot becomes its child
    void GatherTableModels()
    {
        if (dropTable == null || dropTable.placePoint == null) return;
        Vector3 point = dropTable.placePoint.position;

        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r == null) continue;
            var t = r.transform;
            if (t.IsChildOf(dropTable.transform) || dropTable.transform.IsChildOf(t)) continue;

            Bounds b = r.bounds;
            if (point.x < b.min.x - 0.3f || point.x > b.max.x + 0.3f || point.z < b.min.z - 0.3f || point.z > b.max.z + 0.3f) continue;
            if (b.max.y < point.y - 0.5f || b.max.y > point.y + 0.3f) continue;                 // its top is right under the cage spot
            if (b.size.y > 1.8f || Mathf.Max(b.size.x, b.size.z) > 3.5f) continue;              // not a wall or a floor
            t.SetParent(dropTable.transform, true);
        }
    }

    void ParkAll()
    {
        // dropped cages and old capsules do not stay behind
        foreach (var cage in new List<PlacedCage>(PlacedCage.All)) if (cage != null) Destroy(cage.gameObject);

        Park(generator != null ? generator.transform : null, 0, GeneratorExtras());
        Park(dropTable != null ? dropTable.transform : null, 1, TableExtras());
        if (NpcWorker.Instance != null) NpcWorker.Instance.SetTrainingHidden(true);
        RemoveStandByGhost();
    }

    // ---------------------------------------------------------------- the frame line and the walls keep the player in

    void ClearZone()
    {
        zoneOn = false;
    }

    // The walls are solid; this only keeps the player from being pushed out by something
    void LateUpdate()
    {
        if (!zoneOn || player == null || finished) return;

        // the coloured line around the floor breathes a little
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.4f);
        var c = new Color(zoneColor.r, zoneColor.g, zoneColor.b, pulse);
        foreach (var line in zoneLines) if (line != null) line.startColor = line.endColor = c;

        Vector3 p = player.position;
        float x = Mathf.Clamp(p.x, zoneMin.x + 0.4f, zoneMax.x - 0.4f);
        float z = Mathf.Clamp(p.z, zoneMin.y + 0.4f, zoneMax.y - 0.4f);
        if (Mathf.Approximately(x, p.x) && Mathf.Approximately(z, p.z)) return;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.position = new Vector3(x, p.y, z);
        if (controller != null) controller.enabled = true;
    }

    // ---------------------------------------------------------------- the four lessons, one after the other

    void LessonFailed(string why)
    {
        Debug.LogWarning("[Tutorial] This lesson is skipped: " + why);
        if (lessons.Count > 0 && index >= 0 && index < steps.Count) lessons[steps[index].lesson].ok = false;
    }

    void EnterGunRoom()
    {
        if (!roomOk || generator == null || spawner == null)
        {
            LessonFailed("no training room, no generator or no ghost spawner");
            return;
        }

        SetZoneColor(0);
        ParkAll();

        // the generator (the EM unit) stands in the room; the Orb floats between it and the player
        PlaceOnFloor(generator.transform, null, RoomPoint(3.5f, 0f), GeneratorExtras());
        Vector3 spot = RoomPoint(-6.5f, 0f);
        Vector3 orbSpot = RoomPoint(-2.5f, -3.5f);

        GoTo(spot, orbSpot);
        orb = spawner.TutorialPlaceOrb(orbSpot);
        orbCollected = false;
        capsuleSeen = false;
    }

    void EnterCageRoom()
    {
        var inv = Inventory.Instance;
        if (!roomOk || dropTable == null || inv == null || emptyCage == null || mep == null || filledCage == null)
        {
            LessonFailed("no training room, no Drop Table, or the Empty Faraday Cage item is not in the backpack's starting items");
            return;
        }

        SetZoneColor(1);
        ParkAll();

        // the Drop Table with the NPC behind it
        Vector3? cageSpot = dropTable.placePoint != null ? dropTable.placePoint.position : (Vector3?)null;
        PlaceOnFloor(dropTable.transform, cageSpot, RoomPoint(0f, -3.6f), TableExtras());

        var npc = NpcWorker.Instance;
        Vector3 stand = RoomPoint(0f, -5.6f);
        if (npc != null)
        {
            npc.SetHome(stand, 180f, 0f, stand, TablePoint());
            AlignToFloor(npc.transform);
            npc.SetTrainingHidden(false);
        }

        Vector3 spot = RoomPoint(0f, 2.8f);
        GoTo(spot, TablePoint());

        // everything the room needs is in the backpack, and the check is short
        if (!inv.Has(emptyCage.Id)) inv.AddData(emptyCage, 1);
        if (!inv.Has(mep.Id)) inv.AddData(mep, 1);
        mepAtStart = inv.GetCount(mep.Id);
        dropTable.checkSeconds = 8f;
        pointsAtStart = UpgradeSystem.Instance != null ? UpgradeSystem.Instance.TotalPoints : 0;
    }

    // The MEP went into the cage: the filled cage is in the backpack, or the MEP is gone from it (whatever the order the player did things in)
    bool CageFilled()
    {
        var inv = Inventory.Instance;
        if (inv == null || filledCage == null) return false;
        if (inv.Has(filledCage.Id)) return true;
        return mep != null && mepAtStart > 0 && inv.GetCount(mep.Id) < mepAtStart && PlacedCage.All.Count == 0;
    }

    // A character stands with its feet on the floor of the room
    void AlignToFloor(Transform root)
    {
        Bounds b = BodyBounds(root);
        root.position += Vector3.up * (roomFloor - b.min.y);
    }

    // A plain model of the woman: a copy without any script, collider, agent or sound. Only the scanner reads it.
    void MakeStandByGhost(Vector3 spot)
    {
        var source = spawner != null ? spawner.ghost : null;
        if (source == null) return;

        var go = Instantiate(source.gameObject);
        go.name = "Stand-by woman (radar only)";
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true)) Destroy(agent);
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Destroy(audio);
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
        go.SetActive(true);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

        go.transform.position = spot;
        Vector3 flat = player.position - spot;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(flat) * Quaternion.Euler(0f, spawner.rotationOffsetY, 0f);
        AlignToFloor(go.transform);

        radarGhost = go.transform;
        GhostDevice.OverrideGhost = radarGhost;
    }

    void RemoveStandByGhost()
    {
        GhostDevice.OverrideGhost = null;
        if (radarGhost != null) Destroy(radarGhost.gameObject);
        radarGhost = null;
    }

    void EnterRadarRoom()
    {
        if (!roomOk || spawner == null || spawner.ghost == null)
        {
            LessonFailed("no training room or no ghost model");
            return;
        }

        SetZoneColor(2);
        ParkAll();
        radarNear = false;
        if (scanner != null) scanner.Recharge();

        Vector3 spot = RoomPoint(-5.5f, 0f);
        Vector3 ghostSpot = RoomPoint(5f, 0f);
        GoTo(spot, ghostSpot);
        MakeStandByGhost(ghostSpot);
    }

    void EnterNpcRoom()
    {
        var npc = NpcWorker.Instance;
        if (!roomOk || npc == null || UpgradeSystem.Instance == null)
        {
            LessonFailed("no training room or no NPC");
            return;
        }

        SetZoneColor(3);
        ParkAll();

        Vector3 home = RoomPoint(0f, -3.5f);
        npc.SetHome(home, 180f, 0f, home, RoomPoint(0f, 3.5f));
        AlignToFloor(npc.transform);
        npc.SetTrainingHidden(false);

        GoTo(RoomPoint(0f, 3.5f), home);

        // a point to spend
        if (UpgradeSystem.Instance.Points <= 0) UpgradeSystem.Instance.AddPoints(1);
    }

    // ---------------------------------------------------------------- the tasks that need something done first

    Vector3? NearestDoor()
    {
        DoorInteraction best = null;
        float bestDistance = float.MaxValue;
        foreach (var d in doors)
        {
            if (d.Key == null) continue;
            float distance = (d.Key.transform.position - player.position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = d.Key; }
        }
        if (best == null) return null;

        var r = best.GetComponentInChildren<Renderer>();
        return (r != null ? r.bounds.center : best.transform.position) + Vector3.up * 0.2f;
    }

    Vector3? NearestHidingPlace()
    {
        if (HideSystem.Instance != null && HideSystem.Instance.TryNearestSpot(player.position, out Vector3 point))
            return point + Vector3.up * 0.8f;
        return null;
    }

    // The woman appears some meters away and runs at the player (she does no damage)
    void StartDrill()
    {
        if (spawner == null || drillStarted) return;
        drillStarted = true;
        drillStartTime = Time.time;

        Vector3 from = player.position;
        for (int i = 0; i < 24; i++)
        {
            float angle = i * 15f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            if (!NavMesh.SamplePosition(player.position + direction * 16f, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;

            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(player.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                from = hit.position;
                break;
            }
        }

        if ((from - player.position).magnitude < 5f)
        {
            // nowhere to start from: this task is skipped
            stepStart = -1000f;
            return;
        }
        spawner.TutorialStartChase(from);
    }

    // ---------------------------------------------------------------- the loop

    void Update()
    {
        if (finished || steps.Count == 0) return;

        // what the player does
        moved += Vector3.Distance(player.position, lastPosition);
        lastPosition = player.position;
        turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, player.eulerAngles.y));
        lastYaw = player.eulerAngles.y;
        if (Inventory.Instance != null && Inventory.Instance.IsOpen) backpackSeen = true;
        if (UpgradePanel.IsOpen) upgradeSeen = true;

        // the drill is over when she gives up: she must not come back
        if (drillStarted && spawner != null && !spawner.IsChasing && Time.time - drillStartTime > 3f) spawner.TutorialEndDrill();

        var step = steps[index];

        // a room that can not be made is skipped
        if (!lessons[step.lesson].ok)
        {
            NextStep();
            return;
        }

        // the NPC looks at the player (not while she works at the table)
        if (NpcWorker.Instance != null && !DropTable.AnyChecking && player != null) NpcWorker.Instance.FaceTowards(player.position);

        // N skips a task (never a page: a page goes on with Enter only). M goes to the menu, also from a page
        bool panelOpen = UpgradePanel.IsOpen || SettingsPanel.IsOpen || GuidePanel.IsOpen;
        bool skip = Input.GetKeyDown(KeyCode.N) && !panelOpen && !step.needsEnter;
        bool toMenu = Input.GetKeyDown(KeyCode.M) && !panelOpen;

        if (toMenu) { ExitToMenu(); return; }

        if (skip || (step.done != null && step.done()) || stepStart < -500f)
        {
            NextStep();
            return;
        }

        UpdatePointer(step);
        UpdateScreen(step);
    }

    void NextStep()
    {
        index++;
        stepStart = Time.time;

        if (index >= steps.Count)
        {
            Finish();
            return;
        }

        var step = steps[index];
        Frozen = step.needsEnter;          // a page: the player waits until Enter is pressed
        continueClicked = false;
        if (step.begin != null) step.begin();
    }

    // ---------------------------------------------------------------- the end

    void Finish()
    {
        finished = true;
        Frozen = false;
        ClearZone();
        RemoveStandByGhost();
        if (pinRoot != null) pinRoot.gameObject.SetActive(false);
        if (edgeRoot != null) edgeRoot.gameObject.SetActive(false);
        if (marker != null) marker.gameObject.SetActive(false);
        if (panel != null) panel.SetActive(false);
        if (card != null) card.SetActive(false);
        if (finishPanel != null) finishPanel.SetActive(true);
        if (spawner != null) spawner.TutorialEndDrill();

        // the mouse is free for the two buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (playerController != null) playerController.enabled = false;
    }

    public void PlayGame()
    {
        Requested = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameSceneName);
    }

    public void ExitToMenu()
    {
        Requested = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MenuSceneName);
    }

    // ---------------------------------------------------------------- the marker

    // ---------------------------------------------------------------- the screen

    void BuildScreen()
    {
        var canvasGo = new GameObject("TutorialCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 72;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();
        screenCanvas = canvas;

        BuildPointer(canvasGo.transform);

        float inset = Phone ? 28f : 0f;

        var amber = new Color(1f, 0.76f, 0.10f, 1f);
        var green = new Color(0.18f, 0.82f, 0.42f, 1f);
        var heading = UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi;

        // ---- the PAGE in the middle of the screen: the title, the green line, all the tasks and the Enter key.
        // It shows at the start of a lesson and for the last words; the player is frozen until Enter.
        var cardPlate = HudStyler.MakePlate(canvasGo.transform, "Lesson Page", 22f, new Vector2(1060f, 200f));
        card = cardPlate.gameObject;
        var crt = cardPlate.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0f, 20f);
        MakeFlowPanel(cardPlate, new RectOffset(44, 44, 34, 34), 16f);

        headingText = FlowText(crt, "Heading", heading, 38f, amber);
        blurbText = FlowText(crt, "Blurb", UiKit.DmMedium, 30f, green);

        stepsBox = UiKit.NewRect("Steps", crt);
        var list = stepsBox.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 12f;
        list.childAlignment = TextAnchor.UpperLeft;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;

        // Press [Enter] to continue   (the key is a round pill like the other keys; it can be clicked or tapped too)
        var prompt = UiKit.NewRect("Prompt", crt);
        promptRow = prompt.gameObject;
        var promptLine = promptRow.AddComponent<HorizontalLayoutGroup>();
        promptLine.spacing = 16f;
        promptLine.childAlignment = TextAnchor.MiddleLeft;
        promptLine.childControlWidth = promptLine.childControlHeight = true;
        promptLine.childForceExpandWidth = false;
        promptLine.childForceExpandHeight = false;
        promptRow.AddComponent<LayoutElement>().minHeight = 64f;
        pressText = FlowText(prompt, "Press", UiKit.DmSemi, 30f, green);
        pressText.alignment = TextAlignmentOptions.Left;
        MakeKeyPill(prompt, Phone ? "CONTINUE" : "Enter", () => continueClicked = true, out enterLabel);
        toContinueText = FlowText(prompt, "To continue", UiKit.DmSemi, 30f, green);
        toContinueText.alignment = TextAlignmentOptions.Left;
        pressText.text = Phone ? "Tap" : "Press";
        toContinueText.text = "to continue";
        card.SetActive(false);

        // ---- the small panel (top left, under the health bar): the task now, ONE at a time
        var plate = HudStyler.MakePlate(canvasGo.transform, "Training", 18f, new Vector2(700f, 200f));
        panel = plate.gameObject;
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(48f, -150f - inset);
        MakeFlowPanel(plate, new RectOffset(30, 30, 22, 18), 12f);

        smallHeading = FlowText(rt, "Heading", heading, 28f, amber);

        var taskRow = UiKit.NewRect("Task", rt);
        var taskLine = taskRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        taskLine.spacing = 8f;
        taskLine.childAlignment = TextAnchor.UpperLeft;
        taskLine.childControlWidth = taskLine.childControlHeight = true;
        taskLine.childForceExpandWidth = false;
        taskLine.childForceExpandHeight = false;
        currentNo = FlowText(taskRow, "No", UiKit.DmSemi, 30f, amber);
        var noSize = currentNo.gameObject.AddComponent<LayoutElement>();
        noSize.minWidth = noSize.preferredWidth = 84f;
        noSize.flexibleWidth = 0f;
        currentText = FlowText(taskRow, "Text", UiKit.DmMedium, 30f, Color.white);
        currentText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // the hints at the bottom of the panel
        var bottom = UiKit.NewRect("Hints", rt);
        var bottomRow = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
        bottomRow.childControlWidth = bottomRow.childControlHeight = true;
        bottomRow.childForceExpandWidth = true;
        bottomRow.childForceExpandHeight = false;
        hintText = FlowText(bottom, "Hint", UiKit.DmMedium, 22f, new Color(Bone.r, Bone.g, Bone.b, 0.55f));
        hintText.text = Phone ? "" : "N  skip      M  menu";
        frameText = FlowText(bottom, "Frame", UiKit.DmMedium, 22f, Bone);
        frameText.text = "Stay inside the colored frame";
        frameText.alignment = TextAlignmentOptions.TopRight;

        // ---- on a phone: two small buttons (there is no N or M)
        phoneButtons = new GameObject("PhoneButtons", typeof(RectTransform));
        phoneButtons.transform.SetParent(canvasGo.transform, false);
        var prt = (RectTransform)phoneButtons.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f);
        prt.pivot = new Vector2(0f, 0f);
        prt.anchoredPosition = new Vector2(48f, 40f);
        prt.sizeDelta = new Vector2(600f, 70f);
        MakeButton(prt, "SKIP", new Vector2(-100f, 0f), new Vector2(160f, 60f), NextStep);
        MakeButton(prt, "MENU", new Vector2(100f, 0f), new Vector2(160f, 60f), ExitToMenu);
        phoneButtons.SetActive(Phone);

        // ---- the end
        var endPlate = HudStyler.MakePlate(canvasGo.transform, "Finish", 22f, new Vector2(860f, 420f));
        finishPanel = endPlate.gameObject;
        var ert = endPlate.rectTransform;
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f);
        ert.pivot = new Vector2(0.5f, 0.5f);
        ert.anchoredPosition = Vector2.zero;

        var done = UiKit.Label(ert, "Done", "TRAINING COMPLETE", UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi, 54f, Blood,
            TextAlignmentOptions.Center, new Vector2(0f, 100f), new Vector2(780f, 90f));
        done.characterSpacing = 3f;
        UiKit.Label(ert, "Well", "You know everything you need. Good luck tonight.", UiKit.DmMedium, 32f, Bone,
            TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(780f, 60f));
        MakeButton(ert, "PLAY THE GAME", new Vector2(-190f, -120f), new Vector2(340f, 84f), PlayGame);
        MakeButton(ert, "BACK TO THE MENU", new Vector2(190f, -120f), new Vector2(340f, 84f), ExitToMenu);
        finishPanel.SetActive(false);
    }

    // The pointer: a small ring with a chevron over the target and the distance under it. When the target is off the screen, a round
    // arrow sits at the edge of the screen and points the way.
    void BuildPointer(Transform parent)
    {
        pinRoot = UiKit.NewRect("Pin", parent);
        pinRoot.anchorMin = pinRoot.anchorMax = Vector2.zero;
        pinRoot.pivot = new Vector2(0.5f, 0.5f);
        pinRoot.sizeDelta = Vector2.zero;

        pinRing = UiKit.Shape(pinRoot, "Ring", UiKit.Ring, Bone, 1f, Vector2.zero, new Vector2(46f, 46f));
        pinRing.type = Image.Type.Simple;
        pinCore = UiKit.Shape(pinRoot, "Core", UiKit.Circle, Bone, 1f, Vector2.zero, new Vector2(12f, 12f));
        pinCore.type = Image.Type.Simple;
        pinChevron = UiKit.IconImage(pinRoot, "Chevron", "chevrons-up", Bone, new Vector2(0f, 44f), 34f);
        pinChevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        pinLabel = UiKit.Label(pinRoot, "Distance", "", UiKit.DmSemi, 24f, Bone, TextAlignmentOptions.Center, new Vector2(0f, -40f), new Vector2(140f, 30f));
        pinLabel.gameObject.AddComponent<LocalizeIgnore>();
        pinLabel.raycastTarget = false;

        edgeRoot = UiKit.NewRect("Edge Arrow", parent);
        edgeRoot.anchorMin = edgeRoot.anchorMax = Vector2.zero;
        edgeRoot.pivot = new Vector2(0.5f, 0.5f);
        edgeRoot.sizeDelta = Vector2.zero;
        edgePlate = UiKit.Shape(edgeRoot, "Plate", UiKit.Circle, new Color(0.03f, 0.03f, 0.035f, 0.85f), 1f, Vector2.zero, new Vector2(64f, 64f));
        edgePlate.type = Image.Type.Simple;
        edgeArrow = UiKit.IconImage(edgeRoot, "Arrow", "chevrons-up", Bone, Vector2.zero, 38f);
        edgeLabel = UiKit.Label(edgeRoot, "Distance", "", UiKit.DmSemi, 22f, Bone, TextAlignmentOptions.Center, new Vector2(0f, -50f), new Vector2(120f, 28f));
        edgeLabel.gameObject.AddComponent<LocalizeIgnore>();
        edgeLabel.raycastTarget = false;

        pinRoot.gameObject.SetActive(false);
        edgeRoot.gameObject.SetActive(false);
    }

    void UpdatePointer(Step step)
    {
        if (pinRoot == null || screenCanvas == null) return;

        Vector3? target = step.target != null ? step.target() : (Vector3?)null;
        var cam = Camera.main;
        if (target == null || cam == null)
        {
            pinRoot.gameObject.SetActive(false);
            edgeRoot.gameObject.SetActive(false);
            return;
        }

        Color color = zoneOn ? zoneColor : Bone;
        float scale = Mathf.Max(0.01f, screenCanvas.scaleFactor);
        float w = Screen.width, h = Screen.height;
        Vector3 sp = cam.WorldToScreenPoint(target.Value);
        bool front = sp.z > 0.1f;
        const float margin = 90f;
        bool onScreen = front && sp.x > margin && sp.x < w - margin && sp.y > margin + 40f && sp.y < h - margin;
        string label = Mathf.RoundToInt(Vector3.Distance(cam.transform.position, target.Value)) + " m";

        if (onScreen)
        {
            if (!pinRoot.gameObject.activeSelf) pinRoot.gameObject.SetActive(true);
            if (edgeRoot.gameObject.activeSelf) edgeRoot.gameObject.SetActive(false);

            pinRoot.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
            pinRing.rectTransform.localScale = Vector3.one * pulse;
            pinChevron.rectTransform.anchoredPosition = new Vector2(0f, 44f + 7f * Mathf.Sin(Time.time * 3.2f));
            pinRing.color = color;
            pinCore.color = color;
            pinChevron.color = color;
            pinLabel.color = color;
            if (pinLabel.text != label) pinLabel.text = label;
        }
        else
        {
            if (pinRoot.gameObject.activeSelf) pinRoot.gameObject.SetActive(false);
            if (!edgeRoot.gameObject.activeSelf) edgeRoot.gameObject.SetActive(true);

            Vector2 dir = new Vector2(sp.x - w * 0.5f, sp.y - h * 0.5f);
            if (!front) dir = -dir;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();

            float tx = (w * 0.5f - margin) / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
            float ty = (h * 0.5f - margin) / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
            Vector2 pos = new Vector2(w * 0.5f, h * 0.5f) + dir * Mathf.Min(tx, ty);

            edgeRoot.anchoredPosition = pos / scale;
            edgeArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            edgeArrow.color = color;
            edgeLabel.color = color;
            if (edgeLabel.text != label) edgeLabel.text = label;
        }
    }

    // Makes a plate grow with what is in it (a column of lines). The frame pieces of the plate stay out of the layout.
    static void MakeFlowPanel(Image plate, RectOffset padding, float spacing)
    {
        foreach (Transform child in plate.rectTransform) child.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var column = plate.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = padding;
        column.spacing = spacing;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        var fit = plate.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    // A key like the others of the HUD: a dark round pill with a thin red edge and the name of the key
    static void MakeKeyPill(Transform parent, string label, UnityEngine.Events.UnityAction action, out TMP_Text text)
    {
        var size = new Vector2(label.Length > 5 ? 230f : 170f, 62f);
        var pill = UiKit.Shape(parent, "Key " + label, UiKit.Round, new Color(0.02f, 0.02f, 0.025f, 0.95f), size.y * 0.5f, Vector2.zero, size);
        pill.raycastTarget = true;
        var edge = UiKit.Shape(pill.transform, "Edge", UiKit.RoundOutline, new Color(0.86f, 0.12f, 0.16f, 0.95f), size.y * 0.5f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var fixedSize = pill.gameObject.AddComponent<LayoutElement>();
        fixedSize.minWidth = fixedSize.preferredWidth = size.x;
        fixedSize.minHeight = fixedSize.preferredHeight = size.y;
        fixedSize.flexibleWidth = 0f;

        text = UiKit.Label(pill.transform, "Letter", label, UiKit.DmSemi, 32f, Bone, TextAlignmentOptions.Center, Vector2.zero, size);
        text.raycastTarget = false;
        text.gameObject.AddComponent<LocalizeIgnore>();

        var button = pill.gameObject.AddComponent<Button>();
        button.targetGraphic = pill;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        button.onClick.AddListener(action);
    }

    // A line of text for a layout (its width and height come from the layout group)
    static TMP_Text FlowText(Transform parent, string name, TMP_FontAsset font, float size, Color color)
    {
        var t = UiKit.Label(parent, name, "", font, size, color, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(100f, 40f));
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // The numbered tasks of one lesson: "(1)  Take out the gun with 1"
    void BuildRows(int lessonIndex)
    {
        foreach (var row in rows) if (row.root != null) Destroy(row.root);
        rows.Clear();
        shownLesson = lessonIndex;

        int number = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].lesson != lessonIndex || steps[i].intro) continue;
            number++;

            var rowRt = UiKit.NewRect("Task " + number, stepsBox);
            var line = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 8f;
            line.childAlignment = TextAnchor.UpperLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;

            var no = FlowText(rowRt, "No", UiKit.DmSemi, 29f, Bone);
            var noSize = no.gameObject.AddComponent<LayoutElement>();
            noSize.minWidth = noSize.preferredWidth = 84f;
            noSize.flexibleWidth = 0f;
            no.text = "(" + number + ")";

            var text = FlowText(rowRt, "Text", UiKit.DmMedium, 29f, Bone);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            rows.Add(new StepRow { step = i, number = no, text = text, root = rowRt.gameObject });
        }
    }

    // A dark plate button with a thin dark-red edge and bone text (position from the middle of the parent)
    static void MakeButton(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var plate = UiKit.Shape(parent, label + " Button", UiKit.Round, new Color(0.06f, 0.05f, 0.06f, 0.95f), 18f, position, size);
        plate.raycastTarget = true;
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 18f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(action);

        var text = UiKit.Label(plate.transform, "Label", label, UiKit.DmSemi, size.y * 0.36f, Bone, TextAlignmentOptions.Center, Vector2.zero, size);
        text.raycastTarget = false;
    }

    void UpdateScreen(Step step)
    {
        var lesson = lessons[step.lesson];
        string heading = "LESSON " + (step.lesson + 1) + "/" + lessons.Count + "  [" + lesson.name + "]";
        bool page = step.needsEnter;

        // ---- the page in the middle: the start of a lesson (all the tasks) or the last words
        if (card != null && card.activeSelf != page) card.SetActive(page);
        if (panel != null && panel.activeSelf == page) panel.SetActive(!page);

        if (page)
        {
            if (headingText.text != heading) headingText.text = heading;
            string words = step.intro ? lesson.blurb : (Phone ? step.phone : step.pc);
            if (blurbText.text != words) blurbText.text = words;

            // the page in the middle has only the title and the green words: the tasks (1, 2, 3...) are shown one by one in the small
            // panel at the top left, after Enter
            if (stepsBox.gameObject.activeSelf) stepsBox.gameObject.SetActive(false);
            return;
        }

        // ---- the small panel: the task now, one at a time
        if (smallHeading.text != heading) smallHeading.text = heading;

        int number = 0;
        for (int i = lesson.first; i <= index; i++)
            if (steps[i].lesson == step.lesson && !steps[i].intro) number++;
        string no = "(" + number + ")";
        if (currentNo.text != no) currentNo.text = no;

        string current = Phone ? step.phone : step.pc;
        if (currentText.text != current) currentText.text = current;
    }
}
