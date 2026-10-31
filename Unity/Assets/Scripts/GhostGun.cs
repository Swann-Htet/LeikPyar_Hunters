using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// Add to the Player (Person) or the gun. G shows/hides the gun (you cannot pull without it).
// Aim at a baby ghost (tag "bghost") and HOLD THE LEFT MOUSE BUTTON for the gun's pull time (1 second): an electric beam
// connects the gun to the baby and it follows you like a pet on a leash while you keep holding the button and walk.
// The pull sound plays on every click, even when no ghost is aimed at.
// Walk it into the generator's Put Position (BabyGhostGenerator).
public class GhostGun : MonoBehaviour
{
    public static GhostGun Instance { get; private set; }

    [Header("References (auto-found if empty)")]
    [Tooltip("The object that holds the gun model, e.g. 'Gun Hand'")]
    public GameObject gunObject;
    public Camera playerCamera;
    public GhostSpawner spawner;
    [Tooltip("Where the beam starts. Empty = the middle of the gun model")]
    public Transform muzzle;
    [Tooltip("When no Muzzle object is set: how far (meters) in front of the gun's front the beam starts")]
    public float muzzleForward = 0.02f;

    [Header("Gun")]
    public KeyCode toggleKey = KeyCode.G;
    public bool startHolstered = true;
    [Tooltip("The gun tilts up/down with the camera (Y look)")]
    public bool followCameraLook = true;
    [Tooltip("How far the held gun floats up and down (meters)")]
    public float bobHeight = 0.05f;
    [Tooltip("Floating speed (higher = faster)")]
    public float bobSpeed = 1.6f;
    [Tooltip("Small up/down tilt that goes with the floating (degrees)")]
    public float bobTilt = 1.5f;
    [Tooltip("The gun is pulled back when a wall is closer than this in front of the camera (meters)")]
    public float gunReach = 1.8f;
    public float wallPushSpeed = 10f;
    [Tooltip("The gun is never pushed closer to the camera than this (meters), so it can't vanish behind it")]
    public float minGunDistance = 0.45f;

    [Header("Level and Pull Time")]
    [Tooltip("Gun level. Starts at 1")]
    public int gunLevel = 1;
    [Tooltip("Seconds you must hold E on a baby before it is hooked, one value per level (first = level 1)")]
    public float[] pullTimes = { 3f, 2f, 1f };
    [Tooltip("On: the pull reaches 100% exactly when the pull (start) sound ends")]
    public bool pullMatchesSound = true;
    [Tooltip("Seconds the pull takes when there is no sound (3 = three seconds). 0 = use the list above")]
    public float pullSeconds = 3f;
    [Tooltip("The pull (start) sound is played this much faster, so the pull to 100% is shorter. 1 = normal, 1.6 = 40% shorter")]
    [Range(1f, 3f)] public float pullSoundSpeed = 1.6f;

    [Header("Pull time (the bar to 100%)")]
    [Tooltip("Seconds the pull bar takes to reach 100%. 1 = one second. The pull sound is played faster to match (at most 3 times as fast, so a very long sound is not shortened below its length / 3). 0 = the older settings above")]
    public float pullTo100Seconds = 1f;

    [Header("Orb")]
    [Tooltip("An Orb near the player runs away to the opposite side while the gun's heat is above this (0 - 1), or while it cools down")]
    [Range(0f, 1f)] public float orbRunsAboveHeat = 0.5f;

    [Header("Mirage")]
    [Tooltip("The Mirage runs away from the player, and can not be pulled, while the Beam Power is at or below this (%)")]
    public float mirageRunsAwayBelowPower = 20f;

    [Header("Beam Power")]
    [Tooltip("The gun's beam power in percent. Starts at 10. A weak beam lets ghosts slip away more times (see Ghost Rules)")]
    public float beamPower = 10f;
    public float maxBeamPower = 100f;

    [Header("Heat Control")]
    [Tooltip("Heat Control level. 0 = first value in Heat Limits. Raise it (or call UpgradeHeatControl) for a longer use time")]
    public int heatControlLevel = 0;
    [Tooltip("Seconds the gun can be used (pulling / holding a ghost) before it overheats, one value per level")]
    public float[] heatLimits = { 10f, 20f };
    [Tooltip("Seconds the gun is locked after it overheats")]
    public float cooldownTime = 5f;
    [Tooltip("How fast the heat drops while the gun is not used (1 = as fast as it builds up)")]
    public float heatRecovery = 1f;
    [Tooltip("Optional bar that shows the heat (0 to 1)")]
    public Slider heatSlider;
    [Tooltip("{0} = seconds left")]
    public string overheatPrompt = "Gun overheated! Cooling down {0:0.0}s";

    [Header("Ghost Escape")]
    [Tooltip("When the gun overheats, the ghost it holds is frozen this long (seconds)...")]
    public float overheatStunTime = 3f;
    [Tooltip("...when it slips away because of the dice, it is frozen this long (seconds)")]
    public float escapeStunTime = 0f;
    [Tooltip("After the stun the ghost darts AROUND you for this long (seconds). 0 = until you catch it again")]
    public float fleeTime = 12f;
    [Tooltip("...at this speed (meters per second)")]
    public float fleeSpeed = 1.2f;
    [Tooltip("The escaped ghost stays between these two distances from you (meters). Keep Max below Aim Distance so you can catch it again")]
    public float fleeMinDistance = 2.5f;
    public float fleeMaxDistance = 5f;
    public string escapePrompt = "The ghost broke free! Catch it again";
    [Tooltip("A caught ghost breaks free at a RANDOM moment while you carry it: between these two values (seconds after it is hooked)")]
    public float escapeMinTime = 3f;
    public float escapeMaxTime = 8f;
    [Tooltip("The ghost struggles (shakes harder) this many seconds before it breaks free")]
    public float escapeWarnTime = 1.5f;
    [Tooltip("A carried ghost cannot break free when it is this close to the generator (meters). Its escape timer waits")]
    public float noEscapeNearGenerator = 7f;

    [System.Serializable]
    public class GhostRule
    {
        [Tooltip("The ghost's Unity tag: Orb, Mirage or Solid")]
        public string tag = "Orb";
        [Tooltip("The dice is used when the gun's Beam Power is this percent or lower")]
        public float beamPowerThreshold = 20f;
        [Tooltip("Chance (%) that the dice is used when the Beam Power is at or below the threshold")]
        [Range(0f, 100f)] public float possibility = 100f;
        [Tooltip("The dice: the ghost slips away this many times (random from Min to Max)")]
        public int diceMin = 2;
        public int diceMax = 5;
        [Tooltip("How many times it slips away when the dice is NOT used")]
        public int normalEscapes = 1;
    }

    [Header("Ghost Rules (one per tag)")]
    public GhostRule[] ghostRules =
    {
        new GhostRule { tag = "Orb",    beamPowerThreshold = 20f, possibility = 100f, diceMin = 2, diceMax = 5, normalEscapes = 1 },
        new GhostRule { tag = "Mirage", beamPowerThreshold = 40f, possibility = 75f,  diceMin = 2, diceMax = 5, normalEscapes = 1 },
        new GhostRule { tag = "Solid",  beamPowerThreshold = 60f, possibility = 50f,  diceMin = 2, diceMax = 5, normalEscapes = 1 },
    };
    [Tooltip("Ghosts with the old 'bghost' tag use the rule of this tag")]
    public string untypedGhostUses = "Orb";

    [Header("Gun VFX (drag your own particle prefabs here, leave empty for none)")]
    [Tooltip("The lightning line between the gun and the ghost. Turn it off to use only the particle VFX below")]
    public bool showLineBeam = true;
    [Tooltip("THE PULL EFFECT: copies of this particle prefab are placed along the whole beam, from the gun to the ghost. " +
             "While it is set, the lightning line is hidden. Use a looping effect, e.g. CFXR Electrified 3")]
    public GameObject beamVfx;
    [Tooltip("Distance between the copies along the beam (meters). Smaller = a denser beam")]
    public float beamVfxSpacing = 0.6f;
    [Tooltip("Most copies used for a long beam")]
    public int beamVfxMaxCopies = 12;
    [Tooltip("Size of each copy (1 = the prefab's own size)")]
    public float beamVfxScale = 0.5f;
    [Tooltip("The copies wobble sideways by this much (meters) so the beam looks alive")]
    public float beamVfxWobble = 0.05f;
    [Tooltip("Plays at the gun's muzzle while the beam is on")]
    public GameObject muzzleVfx;
    [Tooltip("The muzzle effect and the start of the beam are moved this far in front of the muzzle head, along the beam (meters). Small = the effect sits on the muzzle head")]
    public float vfxStartForward = 0.03f;
    [Tooltip("Moves the muzzle head, if the effect does not sit exactly on it: right / up / forward as the player sees it (meters)")]
    public Vector3 muzzleTipOffset = Vector3.zero;
    [Tooltip("A small glowing orb and a little light in the space between the gun's muzzle and where the electric effect starts, while the gun fires")]
    public bool fillMuzzleGap = true;
    [Tooltip("The orb model shown there (the old magical orb: ASSET/Orb/magical_orb.glb). Tools > Ghost > Use Butterfly Orb, MirageGhost And Muzzle Orb sets it. Empty = only the light")]
    public GameObject muzzleOrb;
    [Tooltip("How wide that glowing orb looks (meters)")]
    public float muzzleOrbSize = 0.14f;
    [Tooltip("How far in front of the barrel the orb sits (meters). It is fixed on the gun: it does not move while you fire")]
    public float muzzleOrbForward = 0.04f;
    [Tooltip("Plays on the ghost while the beam is on")]
    public GameObject targetVfx;
    [Tooltip("Plays once on the ghost when it is hooked")]
    public GameObject hookVfx;
    [Tooltip("Plays once on the ghost when it breaks free")]
    public GameObject escapeVfx;
    [Tooltip("Plays once at the gun when it overheats")]
    public GameObject overheatVfx;
    [Tooltip("Seconds before a one-shot VFX is removed")]
    public float oneShotLifetime = 3f;

    [Header("Aim (range of the pull)")]
    public float aimDistance = 8f;
    [Tooltip("How far off the crosshair the baby can be (meters) when close")]
    public float aimRadius = 0.8f;
    [Tooltip("Extra tolerance per meter of distance")]
    public float aimSpread = 0.1f;
    [Tooltip("Height of the baby's body center above its pivot")]
    public float bodyCenterOffset = 0.4f;

    [Header("Pull")]
    [Tooltip("Pull with the LEFT mouse button (the key below is then not used). Phones use the interact button")]
    public bool useLeftClick = true;
    [Tooltip("Only used when Use Left Click is off")]
    public KeyCode pullKey = KeyCode.E;
    [Tooltip("How fast the progress drops when you let go or look away")]
    public float pullCancelSpeed = 2f;
    public string pullPrompt = "Hold [Left Click] to pull baby ghost";
    [Tooltip("{0} = percent")]
    public string pullingPrompt = "Pulling baby ghost... {0}%";
    public string leashPrompt = "Keep holding [Left Click] and walk. Take the baby ghost to the generator";
    [Tooltip("How hard the gun shakes while pulling (meters)")]
    public float pullShake = 0.01f;
    [Tooltip("How far the gun kicks back when the baby is hooked (meters)")]
    public float recoil = 0.08f;
    public float kickTime = 0.3f;

    [Header("Sounds (empty = the default in Resources/Sfx)")]
    [Tooltip("Plays only while the left mouse button is held (a 3 second sound matches the 3 second pull). Plays even when no ghost is aimed at. It stops the moment you let go")]
    public AudioClip pullLoadClip;
    [Tooltip("Loops while the gun is pulling a ghost along")]
    public AudioClip pullingLoopClip;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private AudioSource loadSource;
    private AudioSource loopSource;
    private bool loadFromTap;

    [Header("Hold (the baby is frozen by the electricity and stays in your view)")]
    [Tooltip("Closest the held baby can be to the camera (meters)")]
    public float minHoldDistance = 2f;
    [Tooltip("Furthest the held baby can be from the camera (meters)")]
    public float maxHoldDistance = 5f;
    [Tooltip("How fast the baby slides into the hold distance")]
    public float settleSpeed = 4f;
    [Tooltip("The carried ghost never goes higher than this above the floor (meters), even when you look up")]
    public float carryMaxHeight = 1.8f;
    [Tooltip("...and never lower than this")]
    public float carryMinHeight = 0.4f;
    [Tooltip("Seconds the baby takes to reach the center of your view when you move or turn (more = slower)")]
    public float followDelay = 0.5f;
    [Tooltip("How much the baby shakes from the electric shock (meters)")]
    public float shockJitter = 0.03f;

    [Header("Electric Beam (VFX)")]
    [Tooltip("Material of the beam (Tools > Ghost > Setup... creates and assigns one)")]
    public Material beamMaterial;
    [Tooltip("How many lightning lines (1 = a single bolt). The extra lines are thinner and wilder")]
    public int beamLines = 4;
    public float beamWidth = 0.04f;
    [Tooltip("How far the lightning zigzags off the straight line (meters)")]
    public float beamJitter = 0.12f;
    public int beamSegments = 14;
    [Tooltip("Seconds between lightning shape changes")]
    public float beamRefresh = 0.04f;
    [Tooltip("The beam (and the heat bar) fade to this color as the gun heats up")]
    public Color hotBeamColor = new Color(1f, 0.12f, 0.08f, 1f);

    private float leashEscapeTime = -1f;
    private BabyGhostGenerator[] generators;
    private class BeamCopy
    {
        public GameObject go;
        public ParticleSystem[] systems;
        public ParticleSystem.MinMaxGradient[] originalColors;
        public Color[] baseColors;
        public float tint = -1f;
    }

    private readonly List<BeamCopy> beamCopies = new List<BeamCopy>();
    private GameObject muzzleVfxInstance;
    private GameObject targetVfxInstance;
    private Color normalBeamColor = new Color(0.4f, 0.85f, 1f, 1f);
    private MaterialPropertyBlock beamBlock;
    private Image sliderFill;
    private Color sliderNormalColor = Color.white;
    private float heat;
    private bool coolingDown;
    private float coolEnd;
    private string messageText;
    private float messageUntil;

    private bool held;
    public bool IsHeld => held;
    public float BeamPower => beamPower;
    private bool autoPull;
    private Transform playerRoot;
    private Renderer[] gunRenderers;
    private Transform holder;
    private Vector3 holderLocalPos;
    private Quaternion holderLocalRot;
    private Vector3 kick;
    private Vector3 shakeOffset;
    private float wallPush;
    private static readonly RaycastHit[] wallHits = new RaycastHit[8];

    private List<Transform> babies = new List<Transform>();
    private readonly List<Renderer[]> babyRenderers = new List<Renderer[]>();

    private Transform pullTarget;
    private float progress;
    private bool charging;
    private Transform leashedBaby;
    private Vector3 holdPosition;
    private Vector3 holdVelocity;
    private float holdDistance;
    private float holdTargetDistance;
    private BabyGhostFloat frozenFloat;

    private LineRenderer[] beams;
    private Vector2[][] beamOffsets;
    private float[] beamTimers;

    public float HeatLimit => heatLimits != null && heatLimits.Length > 0
        ? Mathf.Max(0.1f, heatLimits[Mathf.Clamp(heatControlLevel, 0, heatLimits.Length - 1)])
        : 10f;
    public float HeatPercent => Mathf.Clamp01(heat / HeatLimit);
    public bool IsCoolingDown => coolingDown;

    public bool HasBaby => leashedBaby != null;
    public Transform LeashedBaby => leashedBaby;
    public Transform PlayerRoot => playerRoot;
    // How fast the pull sound is played
    float PullPitch
    {
        get
        {
            AudioClip clip = PullClip;
            if (pullTo100Seconds > 0f && clip != null && clip.length > 0.3f)
                return Mathf.Clamp(clip.length / Mathf.Max(0.1f, PullTime), 1f, 3f);
            return Mathf.Max(1f, pullSoundSpeed);
        }
    }

    void PlayPullSound()
    {
        if (loadSource == null || PullClip == null) return;
        loadSource.pitch = PullPitch;
        Sfx.Play(loadSource, PullClip);
    }

    public float PullTime
    {
        get
        {
            // the time you set (Pull To 100 Seconds); a very long sound can only be played 3 times as fast
            if (pullTo100Seconds > 0f)
            {
                AudioClip c = PullClip;
                float t = pullTo100Seconds;
                if (c != null && c.length > 0.3f) t = Mathf.Max(t, c.length / 3f);
                return t;
            }

            // 100% is reached the moment the pull sound ends
            if (pullMatchesSound)
            {
                AudioClip clip = PullClip;
                if (clip != null && clip.length > 0.3f) return clip.length / Mathf.Max(1f, pullSoundSpeed);
            }

            if (pullSeconds > 0f) return pullSeconds;

            return pullTimes != null && pullTimes.Length > 0
                ? pullTimes[Mathf.Clamp(gunLevel - 1, 0, pullTimes.Length - 1)]
                : 3f;
        }
    }

    private AudioClip pullClipResolved;

    // The pull sound: the clip you gave, or the default one
    AudioClip PullClip
    {
        get
        {
            if (pullClipResolved == null) pullClipResolved = Sfx.Clip(pullLoadClip, "GunPull");
            return pullClipResolved;
        }
    }

    // The backpack or the upgrade panel is open: the mouse is for the UI then
    static bool UiBlocksGun => (Inventory.Instance != null && Inventory.Instance.IsOpen) || UiLock.Open;

    // A finger on a phone also counts as a mouse button in Unity: touches are left to the interact button
    bool PullHeld => !UiBlocksGun && (useLeftClick
        ? Input.GetMouseButton(0) && Input.touchCount == 0
        : Input.GetKey(pullKey));

    bool PullDown => !UiBlocksGun && (useLeftClick
        ? Input.GetMouseButtonDown(0) && Input.touchCount == 0
        : Input.GetKeyDown(pullKey));

    // The prompts say [Left Click] even if an old text in the scene still says [E]
    string K(string text)
    {
        if (text == null) return text;
        if (useLeftClick) text = text.Replace("[E]", "[Left Click]");

        // "baby ghost" in the prompts becomes the name of the type: Orb, Mirage or Solid
        return BabyWord.Replace(text, TypeName(promptGhost));
    }

    static readonly Regex BabyWord = new Regex("baby ghost", RegexOptions.IgnoreCase);

    // The ghost the prompts are about (the one aimed at, pulled or on the leash)
    private Transform promptGhost;

    // Orb / Mirage / Solid. An old "bghost" (or untagged) ghost counts as the type in Untyped Ghost Uses
    public string TypeName(Transform ghost)
    {
        string tag = ghost != null ? ghost.gameObject.tag : null;
        if (string.IsNullOrEmpty(tag) || tag == "Untagged" || tag == BabyGhostFinder.Tag) tag = untypedGhostUses;
        return string.IsNullOrEmpty(tag) ? "Orb" : tag;
    }

    // Where the beam leaves the gun: the muzzle head (and the little nudge you set)
    public Vector3 MuzzlePoint
    {
        get
        {
            Vector3 point = RawMuzzlePoint;
            if (muzzleTipOffset != Vector3.zero && playerCamera != null) point += playerCamera.transform.TransformDirection(muzzleTipOffset);
            return point;
        }
    }

    Vector3 RawMuzzlePoint
    {
        get
        {
            if (muzzle != null) return muzzle.position;
            if (gunObject != null)
            {
                // The beam leaves the FRONT of the gun (the side that looks away from the player), at the middle of its height:
                // the point of the gun's box that is furthest along the direction the camera looks
                var renderers = gunObject.GetComponentsInChildren<Renderer>();
                bool any = false;
                Bounds box = new Bounds();
                foreach (var r in renderers)
                {
                    if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!any) { box = r.bounds; any = true; }
                    else box.Encapsulate(r.bounds);
                }

                if (any)
                {
                    Vector3 forward = playerCamera != null ? playerCamera.transform.forward : gunObject.transform.forward;
                    Vector3 e = box.extents;
                    float reach = Mathf.Abs(forward.x) * e.x + Mathf.Abs(forward.y) * e.y + Mathf.Abs(forward.z) * e.z;
                    return box.center + forward * (reach + muzzleForward);
                }
                return gunObject.transform.position;
            }
            return playerCamera.transform.position + playerCamera.transform.forward;
        }
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var pc = GetComponentInParent<PlayerController>();
        playerRoot = pc != null ? pc.transform : transform;

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) playerCamera = Camera.main;
        if (spawner == null) spawner = FindFirstObjectByType<GhostSpawner>();
        if (gunObject == null) gunObject = FindDeep(playerRoot, "Gun Hand");

        if (gunObject == null)
            Debug.LogWarning("[GhostGun] Gun object not found. Assign 'Gun Hand' to Gun Object.");
        else
            gunRenderers = gunObject.GetComponentsInChildren<Renderer>(true);

        // The inventory (UI slots) lives on the player
        if (FindFirstObjectByType<Inventory>() == null)
            playerRoot.gameObject.AddComponent<Inventory>();

        // The babies are the ones the Ghost Manager controls; with no manager, every tagged baby
        if (spawner != null) babies = spawner.GetBabies();
        if (babies.Count == 0) babies = BabyGhostFinder.Find();
        foreach (var b in babies)
            babyRenderers.Add(b.GetComponentsInChildren<Renderer>(true));

        foreach (var b in babies)
            GetCatchable(b);

        beamPower = Mathf.Clamp(beamPower, 0f, maxBeamPower);
        if (heatSlider != null)
        {
            heatSlider.minValue = 0f;
            heatSlider.maxValue = 1f;
            heatSlider.value = 0f;

            sliderFill = heatSlider.fillRect != null ? heatSlider.fillRect.GetComponent<Image>() : null;
            if (sliderFill != null) sliderNormalColor = sliderFill.color;
        }

        loadSource = Sfx.AddSource(gameObject, false, false, sfxVolume);
        if (loadSource != null) loadSource.pitch = PullPitch;   // the sound ends when the pull reaches 100%
        loopSource = Sfx.AddSource(gameObject, false, true, sfxVolume);

        HideVfxTemplates();
        BuildBeam();
        SetupFollow();
        SetHeld(!startHolstered);
    }

    // For a later gun upgrade
    public void UpgradeGun()
    {
        gunLevel++;
    }

    // Adds beam power in percent (e.g. 5 = +5%)
    public void AddBeamPower(float percent)
    {
        beamPower = Mathf.Clamp(beamPower + percent, 0f, maxBeamPower);
    }

    public void UpgradeHeatControl()
    {
        heatControlLevel++;
    }

    // The generator takes the baby over when it reaches the Put Position
    public Transform ReleaseBaby()
    {
        Transform baby = leashedBaby;
        EndLeash();
        if (baby != null)
        {
            var catchable = baby.GetComponent<CatchableGhost>();
            if (catchable != null) catchable.ResetCatch();
        }
        return baby;
    }

    void Update()
    {
        if (coolingDown)
        {
            UpdateCooldown();
        }
        else
        {
            if (leashedBaby != null)
                UpdateLeash();
            else
                UpdatePull(held ? FindAimedBaby() : null);

            UpdateHeat();

            // The pull sound plays on every click, even when no ghost is aimed at
            bool tappedWithoutAim = MobileInput.GetInteract() && !autoPull && leashedBaby == null;
            if (held && (PullDown || tappedWithoutAim))
            {
                loadFromTap = !PullDown;   // a phone tap can not be "held": its sound plays out
                PlayPullSound();
            }
            else if (!loadFromTap && !PullHeld && !autoPull && leashedBaby == null && loadSource != null && loadSource.isPlaying)
            {
                loadSource.Stop();   // the left button was let go: the pull sound stops
            }

            if (PullDown && held && leashedBaby == null && pullTarget == null)
                ExplainNoAim();
        }

        if (!coolingDown && Time.time < messageUntil)
            InteractPrompt.Request(messageText);

        if (sliderFill != null)
            sliderFill.color = Color.Lerp(sliderNormalColor, hotBeamColor, coolingDown ? 1f : HeatPercent);
    }

    // E was pressed but nothing is being pulled: tell the player (and the Console) why
    void ExplainNoAim()
    {
        Transform nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < babies.Count; i++)
        {
            if (!IsVisible(i)) continue;
            float d = Vector3.Distance(playerCamera.transform.position, babies[i].position);
            if (d < best) { best = d; nearest = babies[i]; }
        }

        if (nearest == null) return;

        if (best > aimDistance)
        {
            ShowMessage("The ghost is too far - get closer", 1.5f);
            Debug.Log("[GhostGun] Can't pull '" + nearest.name + "': " + best.ToString("F1") + "m away, max " + aimDistance + "m");
        }
        else
        {
            ShowMessage("Aim the crosshair at the ghost, then hold [Left Click]", 1.5f);
            Debug.Log("[GhostGun] Can't pull '" + nearest.name + "': " + best.ToString("F1") + "m away but not under the crosshair (or a wall is in between)");
        }
    }

    // ===================== HEAT =====================

    // Heat builds while the gun is pulling or holding a ghost, and drops when it is not used
    void UpdateHeat()
    {
        // The gun only heats up once the ghost is hooked (after 100%), not while the pull bar is filling
        bool inUse = leashedBaby != null;

        if (inUse) heat += Time.deltaTime;
        else heat = Mathf.Max(0f, heat - heatRecovery * Time.deltaTime);

        if (heatSlider != null) heatSlider.value = HeatPercent;

        if (heat >= HeatLimit)
            Overheat();
    }

    // Too hot: the gun locks. The ghost it was holding is stunned, then drifts away slowly
    void Overheat()
    {
        Transform caught = leashedBaby != null ? leashedBaby : pullTarget;

        if (leashedBaby != null) EndLeash();
        ResetPull();

        Sfx.Stop(loadSource);
        coolingDown = true;
        coolEnd = Time.time + cooldownTime;
        heat = HeatLimit;
        if (heatSlider != null) heatSlider.value = 1f;

        PlayOneShot(overheatVfx, MuzzlePoint);
        Debug.Log("[GhostGun] Overheated - locked for " + cooldownTime + "s");

        if (caught != null)
        {
            var ghost = GetCatchable(caught);
            if (ghost != null) StartEscape(ghost, overheatStunTime);
        }
    }

    void UpdateCooldown()
    {
        float left = coolEnd - Time.time;

        if (heatSlider != null) heatSlider.value = Mathf.Clamp01(left / Mathf.Max(0.01f, cooldownTime));

        if (left <= 0f)
        {
            coolingDown = false;
            heat = 0f;
            if (heatSlider != null) heatSlider.value = 0f;
            return;
        }

        if (held)
            InteractPrompt.Request(string.Format(overheatPrompt, left));
    }

    // ===================== ESCAPE DICE =====================

    CatchableGhost GetCatchable(Transform baby)
    {
        if (baby == null) return null;

        var ghost = baby.GetComponent<CatchableGhost>();
        if (ghost == null) ghost = baby.gameObject.AddComponent<CatchableGhost>();
        return ghost;
    }

    GhostRule GetRule(string tag)
    {
        if (ghostRules == null || ghostRules.Length == 0) return null;

        if (string.IsNullOrEmpty(tag) || tag == BabyGhostFinder.Tag) tag = untypedGhostUses;

        foreach (var r in ghostRules)
            if (r != null && r.tag == tag) return r;

        return ghostRules[0];
    }

    // How many times this ghost slips away from the gun.
    // Beam Power at or below the ghost's threshold: the dice (Min..Max) is rolled with the rule's possibility.
    // Otherwise: Normal Escapes (1).
    public int RollEscapes(CatchableGhost ghost)
    {
        GhostRule rule = GetRule(ghost.TypeTag);
        if (rule == null) return 1;

        bool weak = beamPower <= rule.beamPowerThreshold;
        bool useDice = weak && Random.value * 100f < rule.possibility;

        int escapes = rule.normalEscapes;
        if (useDice)
            escapes = Random.Range(Mathf.Min(rule.diceMin, rule.diceMax), Mathf.Max(rule.diceMin, rule.diceMax) + 1);

        Debug.Log("[GhostGun] " + ghost.name + " (" + rule.tag + "): beam " + beamPower + "% vs " + rule.beamPowerThreshold +
                  "% -> " + (useDice ? "dice " + rule.diceMin + "-" + rule.diceMax + " rolled " + escapes : "no dice") +
                  " -> slips away " + escapes + " time(s)");
        return escapes;
    }

    void ShowMessage(string text, float seconds)
    {
        messageText = K(text);
        messageUntil = Time.time + seconds;
    }

    // Phase 1: hold E while aiming at a baby until the pull time is reached
    void UpdatePull(Transform aimed)
    {
        // Mobile: one tap on the interact button keeps pulling while you stay aimed
        if (aimed != null && MobileInput.GetInteract())
        {
            if (!autoPull)
            {
                loadFromTap = true;
                PlayPullSound();
            }
            autoPull = true;
        }
        if (aimed == null && pullTarget == null) autoPull = false;
        bool pressing = PullHeld || autoPull;

        charging = false;

        if (aimed != null) promptGhost = aimed;
        else if (pullTarget != null) promptGhost = pullTarget;

        if (pullTarget == null)
        {
            if (aimed == null) { shakeOffset = Vector3.zero; return; }
            pullTarget = aimed;
            progress = 0f;
        }

        // The baby vanished (e.g. the night ended)
        if (!IsVisible(pullTarget))
        {
            ResetPull();
            return;
        }

        bool onTarget = aimed == pullTarget;

        // The moment the crosshair is on the ghost while the button is held, the sound starts again from its beginning,
        // so the bar and the sound always run together (100% = the end of the sound)
        if (onTarget && pressing && !soundSynced)
        {
            soundSynced = true;
            soundRestartTime = Time.time;
            lastAudioProgress = 0f;
            if (loadSource != null && PullClip != null && (!loadSource.isPlaying || loadSource.time > 0.1f))
                PlayPullSound();
        }

        // The bar is read from the sound itself (its time, with its speed), so the bar, the beam effect and the sound
        // start together and 100% is exactly the end of the sound. Without a sound the bar runs on the clock.
        bool audioDriven = soundSynced && loadSource != null && PullClip != null;

        if (onTarget && pressing)
        {
            if (audioDriven && loadSource.isPlaying)
            {
                lastAudioProgress = loadSource.time / Mathf.Max(0.1f, loadSource.pitch);
                progress = Mathf.Clamp(lastAudioProgress, 0.001f, PullTime);   // never 0: that would cancel the pull
            }
            else if (audioDriven && Time.time - soundRestartTime > 0.3f)
            {
                if (lastAudioProgress >= PullTime - 0.25f)
                {
                    progress = PullTime;           // the sound played to its end
                }
                else
                {
                    soundSynced = false;           // the sound was stopped on the way: it starts again with the bar from the start
                    progress = 0.0001f;
                }
            }
            else
            {
                progress += Time.deltaTime;        // the sound is about to start
            }
        }
        else
        {
            progress -= pullCancelSpeed * Time.deltaTime;
        }

        if (progress >= PullTime)
        {
            Hook(pullTarget);
            return;
        }

        if (progress <= 0f)
        {
            ResetPull();
            if (aimed != null) InteractPrompt.Request(K(pullPrompt));
            return;
        }

        float e = Mathf.Clamp01(progress / PullTime);
        shakeOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (pullShake * e);
        // the beam effect starts when the sound has started, and grows with the bar
        charging = onTarget && pressing && (!audioDriven || loadSource.isPlaying);
        effectCharge = e;

        if (onTarget)
            InteractPrompt.Request(K(string.Format(pullingPrompt, Mathf.RoundToInt(e * 100f))));
    }

    private bool soundSynced;
    private float soundRestartTime;
    private float lastAudioProgress;
    private float effectCharge;     // 0 - 1: how far the pull is (the beam effect grows with it)

    void ResetPull()
    {
        effectCharge = 0f;
        soundSynced = false;
        pullTarget = null;
        progress = 0f;
        charging = false;
        shakeOffset = Vector3.zero;
    }

    // Phase 2: the baby is frozen by the electricity and stays locked in your view
    // while you hold E (needs the gun out)
    void Hook(Transform baby)
    {
        ResetPull();


        // The ghost is stuck on the beam. The dice decides how many times it breaks free while
        // you carry it, each time at a random moment
        var ghost = GetCatchable(baby);
        leashEscapeTime = -1f;
        if (ghost != null)
        {
            ghost.StopEscape();
            ghost.EnsureRolled(this);
            ghost.RememberRestHeight();
            if (ghost.escapesLeft > 0)
            {
                float min = Mathf.Min(escapeMinTime, escapeMaxTime);
                float max = Mathf.Max(escapeMinTime, escapeMaxTime);
                leashEscapeTime = Time.time + Random.Range(min, max);
            }
        }

        leashedBaby = baby;
        Sfx.Play(loopSource, Sfx.Clip(pullingLoopClip, "GunPulling"));   // the beam hums while the ghost is pulled along
        PlayOneShot(hookVfx, baby.position + Vector3.up * bodyCenterOffset);

        // It starts where it is and is then drawn to the center of your view
        holdPosition = baby.position;
        holdVelocity = Vector3.zero;
        holdDistance = Vector3.Distance(playerCamera.transform.position, baby.position);
        holdTargetDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);

        // Frozen: no up/down floating while held
        frozenFloat = baby.GetComponent<BabyGhostFloat>();
        if (frozenFloat != null) frozenFloat.enabled = false;

        StartCoroutine(KickRoutine());
    }

    void UpdateLeash()
    {
        promptGhost = leashedBaby;

        // Mobile: tapping again lets the baby go
        if (autoPull && MobileInput.GetInteract()) autoPull = false;
        bool pressing = PullHeld || autoPull;

        if (!held || !pressing || !IsVisible(leashedBaby))
        {
            EndLeash();
            return;
        }

        if (leashEscapeTime > 0f)
        {
            if (NearGenerator(leashedBaby))
            {
                // Close to the generator: the escape timer is paused, so the ghost can't get away now
                leashEscapeTime += Time.deltaTime;
            }
            else if (Time.time >= leashEscapeTime)
            {
                BreakFree();
                return;
            }
        }

        HoldInView(leashedBaby);
        InteractPrompt.Request(K(leashPrompt));
    }

    void EndLeash()
    {
        Sfx.Stop(loopSource);
        if (frozenFloat != null) frozenFloat.enabled = true;
        frozenFloat = null;
        leashedBaby = null;
        leashEscapeTime = -1f;
        autoPull = false;
    }

    bool NearGenerator(Transform baby)
    {
        if (generators == null)
            generators = FindObjectsByType<BabyGhostGenerator>(FindObjectsSortMode.None);

        foreach (var g in generators)
        {
            if (g != null && g.IsNear(baby.position, noEscapeNearGenerator))
                return true;
        }
        return false;
    }

    // The carried ghost tears free: it runs off slowly in a random direction and must be caught again
    void BreakFree()
    {
        var ghost = GetCatchable(leashedBaby);
        Vector3 ghostPosition = leashedBaby.position + Vector3.up * bodyCenterOffset;
        EndLeash();
        PlayOneShot(escapeVfx, ghostPosition);

        if (ghost != null && ghost.ConsumeEscape())
        {
            StartEscape(ghost, escapeStunTime);
            ShowMessage(escapePrompt, 2f);
            Debug.Log("[GhostGun] " + ghost.name + " broke free (" + ghost.escapesLeft + " more time(s))");
            StartCoroutine(KickRoutine());
        }
    }

    // ===================== VFX =====================

    // A VFX that sits in the scene (not a prefab asset) must not play on its own
    void HideVfxTemplates()
    {
        foreach (var template in new[] { beamVfx, muzzleVfx, targetVfx, hookVfx, escapeVfx, overheatVfx })
            if (template != null && template.scene.IsValid()) template.SetActive(false);
    }

    void PlayOneShot(GameObject template, Vector3 position)
    {
        if (template == null) return;

        var fx = Instantiate(template, position, template.transform.rotation);
        fx.SetActive(true);
        Destroy(fx, Mathf.Max(0.5f, oneShotLifetime));
    }

    private GameObject muzzleOrbInstance;
    private float muzzleOrbScale = 1f;
    private float muzzleSpin;
    private Light muzzleGlow;
    private bool muzzleWasOn;
    private Vector3 muzzleLocal;          // where the orb sits, in the gun's own space: fixed

    // The old orb, small: it sits in the space between the barrel and the start of the electric effect, spins and flickers, with a blue light
    void BuildMuzzleOrb()
    {
        muzzleOrbInstance = Instantiate(muzzleOrb);
        muzzleOrbInstance.name = "Muzzle Orb";
        muzzleOrbInstance.transform.localScale = Vector3.one;

        foreach (var c in muzzleOrbInstance.GetComponentsInChildren<Collider>(true)) Destroy(c);

        var glow = Resources.Load<Material>("Orb/Orb_Glow");
        var shapes = new System.Collections.Generic.List<Renderer>();
        foreach (var r in muzzleOrbInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            // the model has a grey sphere beside the glowing shapes: it is not wanted
            bool sphere = false;
            for (Transform t = r.transform; t != null && t != muzzleOrbInstance.transform; t = t.parent)
                if (t.name.StartsWith("Icosphere") || t.name == "Object_8") sphere = true;
            if (sphere) { DestroyImmediate(r.gameObject); continue; }

            if (glow != null)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = glow;
                r.sharedMaterials = mats;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            shapes.Add(r);
        }

        bool any = false;
        Bounds box = new Bounds();
        foreach (var r in shapes)
        {
            if (!any) { box = r.bounds; any = true; }
            else box.Encapsulate(r.bounds);
        }
        float size = any ? Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)) : 0f;
        if (size > 0.0001f) muzzleOrbScale = muzzleOrbSize / (size * 0.45f);     // the glow picture has soft empty edges

        muzzleOrbInstance.SetActive(false);
    }

    void UpdateMuzzleGap(bool on, Vector3 muzzle0, Vector3 start, Vector3 end)
    {
        if (!fillMuzzleGap)
        {
            if (muzzleOrbInstance != null) muzzleOrbInstance.SetActive(false);
            if (muzzleGlow != null) muzzleGlow.enabled = false;
            return;
        }

        if (muzzleOrb != null && muzzleOrbInstance == null) BuildMuzzleOrb();

        if (muzzleGlow == null)
        {
            var lightGo = new GameObject("MuzzleGlow");
            lightGo.transform.SetParent(transform, false);
            muzzleGlow = lightGo.AddComponent<Light>();
            muzzleGlow.type = LightType.Point;
            muzzleGlow.range = 2.4f;
            muzzleGlow.shadows = LightShadows.None;
        }

        if (muzzleOrbInstance != null && muzzleOrbInstance.activeSelf != on) muzzleOrbInstance.SetActive(on);
        muzzleGlow.enabled = on;
        if (!on) { muzzleWasOn = false; return; }

        // The place is read when the beam starts and then stays fixed on the gun (it moves with the gun, not with the aim)
        Transform anchor = gunObject != null ? gunObject.transform : transform;
        if (!muzzleWasOn)
        {
            Vector3 forward = playerCamera != null ? playerCamera.transform.forward : anchor.forward;
            muzzleLocal = anchor.InverseTransformPoint(muzzle0 + forward * muzzleOrbForward);
        }
        muzzleWasOn = true;
        Vector3 spot = anchor.TransformPoint(muzzleLocal);

        // the colour follows the heat, like the beam
        float hot = Mathf.SmoothStep(0f, 1f, coolingDown ? 1f : HeatPercent);
        Color color = Color.Lerp(normalBeamColor, hotBeamColor, hot);
        muzzleGlow.color = color;
        muzzleGlow.intensity = Random.Range(1.4f, 3.2f);
        muzzleGlow.transform.position = spot;

        if (muzzleOrbInstance != null)
        {
            muzzleSpin += 260f * Time.deltaTime;
            float flicker = 1f + 0.18f * Mathf.Sin(Time.time * 22f) + Random.Range(-0.06f, 0.06f);
            muzzleOrbInstance.transform.SetPositionAndRotation(spot, anchor.rotation * Quaternion.Euler(0f, 0f, muzzleSpin));
            muzzleOrbInstance.transform.localScale = Vector3.one * (muzzleOrbScale * flicker);
        }
    }

    // The muzzle and target VFX follow the beam while it is on
    void UpdateVfx(Transform target)
    {
        bool on = target != null && target.gameObject.activeInHierarchy;

        if (muzzleVfx != null && muzzleVfxInstance == null) muzzleVfxInstance = MakeVfx(muzzleVfx);
        if (targetVfx != null && targetVfxInstance == null) targetVfxInstance = MakeVfx(targetVfx);

        Vector3 end = on ? target.position + Vector3.up * bodyCenterOffset : Vector3.zero;

        // the effects start a little in front of the gun, on the way to the target
        Vector3 muzzle0 = on ? MuzzlePoint : Vector3.zero;
        Vector3 start = muzzle0;
        if (on)
        {
            Vector3 along = end - muzzle0;
            float length = along.magnitude;
            if (length > 0.01f) start = muzzle0 + along / length * Mathf.Min(vfxStartForward, length * 0.5f);
        }

        UpdateBeamVfx(on, start, end);
        UpdateMuzzleGap(on, muzzle0, start, end);

        if (muzzleVfxInstance != null)
        {
            if (on)
            {
                muzzleVfxInstance.transform.position = start;
                if ((end - start).sqrMagnitude > 0.0001f)
                    muzzleVfxInstance.transform.rotation = Quaternion.LookRotation(end - start);
            }
            if (muzzleVfxInstance.activeSelf != on) muzzleVfxInstance.SetActive(on);
        }

        if (targetVfxInstance != null)
        {
            if (on) targetVfxInstance.transform.position = end;
            if (targetVfxInstance.activeSelf != on) targetVfxInstance.SetActive(on);
        }
    }

    // The pull effect: copies of the particle prefab all along the beam, redder as the gun heats up
    void UpdateBeamVfx(bool on, Vector3 start, Vector3 end)
    {
        if (beamVfx == null)
        {
            foreach (var c in beamCopies)
                if (c.go != null && c.go.activeSelf) c.go.SetActive(false);
            return;
        }

        int wanted = 0;
        if (on)
        {
            float distance = Vector3.Distance(start, end);
            wanted = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(0.1f, beamVfxSpacing)) + 1, 2, Mathf.Max(2, beamVfxMaxCopies));
        }

        while (beamCopies.Count < wanted)
            beamCopies.Add(CreateBeamCopy());

        Vector3 direction = end - start;
        Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        float heatTint = Mathf.SmoothStep(0f, 1f, coolingDown ? 1f : HeatPercent);

        for (int i = 0; i < beamCopies.Count; i++)
        {
            BeamCopy copy = beamCopies[i];
            if (copy.go == null) continue;

            bool active = i < wanted;
            if (active)
            {
                // small at the start of the pull, full size at 100% (and while the ghost is on the leash)
                float charge = leashedBaby != null ? 1f : Mathf.Clamp01(effectCharge);
                copy.go.transform.localScale = beamVfx.transform.localScale * beamVfxScale * Mathf.Lerp(0.35f, 1f, charge);

                float t = wanted > 1 ? (float)i / (wanted - 1) : 0f;
                Vector3 position = Vector3.Lerp(start, end, t);
                if (i > 0 && i < wanted - 1 && beamVfxWobble > 0f)
                    position += Random.insideUnitSphere * beamVfxWobble;

                copy.go.transform.SetPositionAndRotation(position, rotation);
                TintBeamCopy(copy, heatTint);
            }

            if (copy.go.activeSelf != active) copy.go.SetActive(active);
        }
    }

    BeamCopy CreateBeamCopy()
    {
        var go = Instantiate(beamVfx);
        go.transform.localScale = beamVfx.transform.localScale * beamVfxScale;
        go.SetActive(false);

        var copy = new BeamCopy { go = go, systems = go.GetComponentsInChildren<ParticleSystem>(true) };
        copy.originalColors = new ParticleSystem.MinMaxGradient[copy.systems.Length];
        copy.baseColors = new Color[copy.systems.Length];

        for (int i = 0; i < copy.systems.Length; i++)
        {
            var start = copy.systems[i].main.startColor;
            copy.originalColors[i] = start;
            copy.baseColors[i] = start.mode == ParticleSystemGradientMode.Color ? start.color : start.colorMax;
        }
        return copy;
    }

    // 0 = the prefab's own colors, 1 = Hot Beam Color
    void TintBeamCopy(BeamCopy copy, float tint)
    {
        if (Mathf.Abs(tint - copy.tint) < 0.02f && !(tint == 0f && copy.tint != 0f)) return;
        copy.tint = tint;

        for (int i = 0; i < copy.systems.Length; i++)
        {
            var main = copy.systems[i].main;

            if (tint <= 0.01f)
            {
                main.startColor = copy.originalColors[i];
            }
            else
            {
                Color c = Color.Lerp(copy.baseColors[i], hotBeamColor, tint);
                c.a = copy.baseColors[i].a;
                main.startColor = c;
            }
        }
    }

    GameObject MakeVfx(GameObject template)
    {
        var fx = Instantiate(template);
        fx.SetActive(false);
        return fx;
    }

    void StartEscape(CatchableGhost ghost, float stun)
    {
        ghost.Escape(stun, fleeTime, fleeSpeed, playerRoot, fleeMinDistance, fleeMaxDistance);
    }

    // 0 = calm, 1 = about to break free
    float Struggle()
    {
        if (leashEscapeTime <= 0f) return 0f;
        float warn = Mathf.Max(0.1f, escapeWarnTime);
        return Mathf.Clamp01((Time.time - (leashEscapeTime - warn)) / warn);
    }

    // The baby is drawn to the center of the view, with a delay: it trails behind when you move or turn
    void HoldInView(Transform baby)
    {
        Transform cam = playerCamera.transform;
        holdDistance = Mathf.MoveTowards(holdDistance, holdTargetDistance, settleSpeed * Time.deltaTime);

        // Pressed against a wall instead of disappearing into it
        float distance = holdDistance;
        int count = Physics.RaycastNonAlloc(cam.position, cam.forward, wallHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (wallHits[i].transform.IsChildOf(playerRoot) || wallHits[i].transform.IsChildOf(baby)) continue;
            distance = Mathf.Min(distance, Mathf.Max(0.8f, wallHits[i].distance - 0.4f));
        }

        Vector3 center = cam.position + cam.forward * distance;

        // Looking up must not lift the ghost to the ceiling: keep it within a band above the floor
        // (not near a generator: a machine mounted high must stay reachable)
        float floorY;
        if (!NearGenerator(baby) && CatchableGhost.TryGetGround(playerRoot.position + Vector3.up * 0.5f, playerRoot, baby, out floorY))
            center.y = Mathf.Clamp(center.y, floorY + carryMinHeight, floorY + carryMaxHeight);
        holdPosition = Vector3.SmoothDamp(holdPosition, center, ref holdVelocity, Mathf.Max(0.01f, followDelay));

        // The electric shock makes it shake
        Vector3 shock = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (shockJitter * (1f + 3f * Struggle()));
        baby.position = holdPosition + shock;
    }

    IEnumerator KickRoutine()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, kickTime))
        {
            kick = new Vector3(0f, 0f, -recoil * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        kick = Vector3.zero;
    }

    void LateUpdate()
    {
        if (followCameraLook && holder != null && playerCamera != null)
        {
            // Keep the gun's pose relative to the camera so it tilts with Y look,
            // floating gently up and down
            Transform cam = playerCamera.transform;
            float wave = Mathf.Sin(Time.time * bobSpeed * Mathf.PI);
            Vector3 floatOffset = Vector3.up * (wave * bobHeight);
            Quaternion floatTilt = Quaternion.Euler(wave * bobTilt, 0f, 0f);

            // Pull the gun back so it never pokes through a wall in front of you
            float maxPush = Mathf.Max(0f, holderLocalPos.z - minGunDistance);
            wallPush = Mathf.MoveTowards(wallPush, Mathf.Min(WallPenetration(cam), maxPush), wallPushSpeed * Time.deltaTime);
            Vector3 wallOffset = Vector3.back * wallPush;

            holder.SetPositionAndRotation(
                cam.position + cam.rotation * (holderLocalPos + floatOffset + kick + shakeOffset + wallOffset),
                cam.rotation * floatTilt * holderLocalRot);
        }

        Transform beamTarget = leashedBaby != null ? leashedBaby : (charging ? pullTarget : null);
        UpdateBeam(held ? beamTarget : null);
        UpdateVfx(held ? beamTarget : null);
    }

    // The lightning between the gun and the baby
    void BuildBeam()
    {
        Material material = beamMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader);
                material.color = new Color(0.4f, 0.85f, 1f, 1f);
            }
            else
            {
                Debug.LogWarning("[GhostGun] No beam material. Run Tools > Ghost > Setup Baby Tag And Generator.");
            }
        }

        if (material != null)
        {
            if (material.HasProperty("_BaseColor")) normalBeamColor = material.GetColor("_BaseColor");
            else if (material.HasProperty("_Color")) normalBeamColor = material.GetColor("_Color");
        }
        beamBlock = new MaterialPropertyBlock();

        // Several lightning lines at once: the first is the main bolt, the others are thinner and wilder
        int lines = Mathf.Max(1, beamLines);
        var root = new GameObject("PullBeam");
        beams = new LineRenderer[lines];
        beamOffsets = new Vector2[lines][];
        beamTimers = new float[lines];

        for (int i = 0; i < lines; i++)
        {
            var go = new GameObject("Bolt " + (i + 1));
            go.transform.SetParent(root.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = Mathf.Max(2, beamSegments) + 1;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            if (material != null) line.material = material;

            beams[i] = line;
            beamOffsets[i] = new Vector2[line.positionCount];
            beamTimers[i] = Random.Range(0f, beamRefresh);
        }
    }

    void UpdateBeam(Transform target)
    {
        if (beams == null) return;

        if (!showLineBeam || beamVfx != null)
        {
            foreach (var l in beams) l.enabled = false;
            return;
        }

        bool visible = target != null && target.gameObject.activeInHierarchy;
        foreach (var line in beams)
            line.enabled = visible;
        if (!visible) return;

        // The hotter the gun, the redder the beam
        float hot = Mathf.SmoothStep(0f, 1f, coolingDown ? 1f : HeatPercent);
        Color beamColor = Color.Lerp(normalBeamColor, hotBeamColor, hot);
        beamBlock.SetColor("_BaseColor", beamColor);
        beamBlock.SetColor("_Color", beamColor);
        beamBlock.SetColor("_TintColor", beamColor);
        foreach (var line in beams)
        {
            line.SetPropertyBlock(beamBlock);
            line.startColor = beamColor;
            line.endColor = beamColor;
        }

        Vector3 start = MuzzlePoint;
        Vector3 end = target.position + Vector3.up * bodyCenterOffset;
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        Vector3 side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(direction, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, direction);

        for (int b = 0; b < beams.Length; b++)
        {
            LineRenderer line = beams[b];
            Vector2[] offsets = beamOffsets[b];
            bool main = b == 0;

            // Each line gets a new lightning shape every few hundredths of a second
            beamTimers[b] -= Time.deltaTime;
            if (beamTimers[b] <= 0f)
            {
                beamTimers[b] = beamRefresh * Random.Range(0.6f, 1.4f);
                for (int i = 0; i < offsets.Length; i++)
                    offsets[i] = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                line.widthMultiplier = beamWidth * (main ? 1f : 0.5f) * Random.Range(0.7f, 1.4f);
            }

            float jitter = beamJitter * (main ? 1f : 1.6f + 0.4f * b);
            int last = line.positionCount - 1;
            for (int i = 0; i <= last; i++)
            {
                float t = (float)i / last;
                float strength = Mathf.Sin(t * Mathf.PI) * jitter; // zero at both ends
                Vector3 point = Vector3.Lerp(start, end, t)
                    + side * (offsets[i].x * strength)
                    + up * (offsets[i].y * strength);
                line.SetPosition(i, point);
            }
        }
    }

    // How far the gun would stick into whatever is in front of the camera
    float WallPenetration(Transform cam)
    {
        int count = Physics.RaycastNonAlloc(cam.position, cam.forward, wallHits, gunReach, ~0, QueryTriggerInteraction.Ignore);

        float nearest = gunReach;
        for (int i = 0; i < count; i++)
        {
            if (wallHits[i].transform.IsChildOf(playerRoot)) continue;
            if (wallHits[i].distance < nearest) nearest = wallHits[i].distance;
        }
        return gunReach - nearest;
    }

    void SetupFollow()
    {
        if (!followCameraLook || gunObject == null || playerCamera == null) return;

        holder = gunObject.transform;
        var hand = FindDeep(playerRoot, "Gun Hand");
        if (hand != null && gunObject.transform.IsChildOf(hand.transform))
            holder = hand.transform;

        Transform cam = playerCamera.transform;
        Quaternion inverse = Quaternion.Inverse(cam.rotation);
        holderLocalPos = inverse * (holder.position - cam.position);
        holderLocalRot = inverse * holder.rotation;
    }

    // The inventory puts the gun away while another item is held
    public void HolsterGun()
    {
        if (held) SetHeld(false);
    }

    public bool IsGunHeld => held;

    // Public so a mobile UI button can call it too
    public void ToggleGun()
    {
        SetHeld(!held);
    }

    // Only the renderers are switched, never the object itself: this script may live on the gun.
    // The hotbar (key 1) calls it.
    public void SetHeld(bool value)
    {
        held = value;
        if (heatSlider != null) heatSlider.gameObject.SetActive(value);
        if (gunRenderers != null)
        {
            foreach (var r in gunRenderers)
                if (r != null) r.enabled = value;
        }
    }

    // The visible baby closest to the crosshair, or null
    Transform FindAimedBaby()
    {
        if (playerCamera == null) return null;

        Transform best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < babies.Count; i++)
        {
            if (!IsVisible(i)) continue;

            // a weak gun can not catch a Mirage: it runs away
            var mirage = babies[i].GetComponent<MirageGhost>();
            if (mirage != null && (mirage.IsRunning || beamPower <= mirageRunsAwayBelowPower)) continue;

            // an Orb that runs (the gun is hot) can not be aimed at
            var runner = babies[i].GetComponent<OrbRunner>();
            if (runner != null && runner.IsRunning) continue;

            float score = AimScore(babies[i]);
            if (score >= 0f && score < bestScore)
            {
                bestScore = score;
                best = babies[i];
            }
        }
        return best;
    }

    bool IsVisible(Transform baby)
    {
        int index = babies.IndexOf(baby);
        return index >= 0 && IsVisible(index);
    }

    // Shown in the world right now (hidden babies can't be aimed at)
    bool IsVisible(int index)
    {
        Transform baby = babies[index];
        if (baby == null || !baby.gameObject.activeInHierarchy) return false;

        foreach (var r in babyRenderers[index])
        {
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    // 0 = dead center, 1 = edge of the aim area, -1 = not aimed at
    float AimScore(Transform baby)
    {
        Transform cam = playerCamera.transform;
        Vector3 origin = cam.position;
        Vector3 to = baby.position + Vector3.up * bodyCenterOffset - origin;

        float along = Vector3.Dot(to, cam.forward);
        if (along <= 0f || along > aimDistance) return -1f;

        float lateral = (to - cam.forward * along).magnitude;
        float allowed = aimRadius + along * aimSpread;
        if (lateral > allowed) return -1f;

        // Blocked by a wall/door?
        var hits = Physics.RaycastAll(origin, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(baby) || h.transform.IsChildOf(playerRoot)) continue;
            return -1f;
        }
        return lateral / allowed;
    }

    static GameObject FindDeep(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t.gameObject;
        return null;
    }
}
