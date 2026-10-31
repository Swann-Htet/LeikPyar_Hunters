using UnityEngine;

// The ghost scanner. Add to the Player (Person).
//   R  takes the scanner out / puts it away       O  switches it on / off
// The scanner has no screen: its lights tell everything (the model's parts ON, OFF, DETECT and LEVEL ONE - FIVE):
//   OFF only         : the scanner is out but not switched on
//   ON               : switched on
//   DETECT           : the scanner points at the Solid ghost (the woman)
//   LEVEL ONE - FIVE : the closer the Solid ghost, the more lights: ONE-TWO green, THREE-FOUR yellow, FIVE red
// The range of the scanner depends on its level (Ranges: one entry per level), it cannot be adjusted by the player.
// Set it up with Tools > Ghost > Setup Scanner.
public class GhostDevice : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [Tooltip("The object that holds the scanner model. Child of the player named 'Device Hand'")]
    public GameObject deviceObject;
    public Camera playerCamera;
    public GhostSpawner spawner;
    [Tooltip("The ghost's range. Empty = the Box Collider on the ghost (it is only kept from blocking the player)")]
    public Collider ghostRange;

    [Header("Scanner")]
    [Tooltip("Takes the scanner out / puts it away")]
    public KeyCode toggleKey = KeyCode.R;
    [Tooltip("Switches the scanner on / off while it is out. When it is put away, this key takes it out and switches it on")]
    public KeyCode powerKey = KeyCode.O;
    public bool startHidden = true;
    [Tooltip("The scanner is on at the start of the game")]
    public bool startPowered = false;
    [Tooltip("The scanner tilts up/down with the camera (Y look)")]
    public bool followCameraLook = true;
    public float bobHeight = 0.01f;
    public float bobSpeed = 1.2f;

    [System.Serializable]
    public class ScanRange
    {
        public string name = "Medium";
        [Tooltip("DETECT lights up when she is closer than this (meters) and the scanner points at her")]
        public float detectDistance = 30f;
        [Tooltip("LEVEL: the distance (meters) at which ONE, TWO, THREE, FOUR, FIVE light up. ONE-TWO green, THREE-FOUR yellow, FIVE red")]
        public float[] levelDistances = { 30f, 22f, 15f, 10f, 5f };
    }

    [Header("Range: it depends on the scanner level (one entry per level, the first = level 1). The player cannot change it")]
    [Tooltip("Mock values: set your own")]
    public ScanRange[] ranges =
    {
        new ScanRange { name = "Level 1", detectDistance = 12f, levelDistances = new[] { 12f, 9f, 6f, 4f, 2f } },
        new ScanRange { name = "Level 2", detectDistance = 30f, levelDistances = new[] { 30f, 22f, 15f, 10f, 5f } },
        new ScanRange { name = "Level 3", detectDistance = 60f, levelDistances = new[] { 60f, 45f, 30f, 20f, 10f } },
    };

    [Header("Radius")]
    [Tooltip("All the distances above are multiplied by this: 2.2 = the scanner reaches 2.2 times as far, and the red light (FIVE) comes on farther from her")]
    public float radiusScale = 2.2f;

    [Header("Scanner level (decides the range and how long the battery lasts) and battery (the bar on the HUD)")]
    [Tooltip("The scanner's level: 1 = the first entry of Ranges and of Battery Seconds. It goes up with the upgrade below, or set it here")]
    public int scannerLevel = 1;
    [Tooltip("How many seconds the battery lasts while the scanner is ON, one value per level (first = level 1). Mock values: set your own")]
    public float[] batterySeconds = { 90f, 150f, 240f };
    [Tooltip("On: the battery is NOT charged during the night (not even when the scanner is off). At the first light of the morning it is full again")]
    public bool rechargeInMorning = true;
    [Tooltip("The scanner can only be switched on when the battery is at least this full (0 - 1)")]
    [Range(0f, 0.5f)] public float minPowerOnCharge = 0.05f;
    [Tooltip("Optional: the id of an upgrade (Upgrade System) whose level is the scanner's level, e.g. 'Radio'. Empty = Battery Level above")]
    public string levelUpgradeId = "";

    [Header("Lights")]
    [Tooltip("DETECT lights up when the scanner points this close to the Solid ghost (degrees from the center of the view)")]
    public float detectAngle = 28f;
    [Tooltip("On: walls in between stop DETECT. Off: pointing at her through a wall is enough")]
    public bool detectNeedsLineOfSight = false;

    private bool shown;
    private bool powered;
    private Transform playerRoot;
    private Renderer[] deviceRenderers;
    private Transform holder;
    private Vector3 holderLocalPos;
    private Quaternion holderLocalRot;

    private Renderer ledOn, ledOff, ledDetect;
    private readonly Renderer[] ledLevel = new Renderer[5];
    private Material ledGreen, ledYellow, ledRed, ledDark;
    private bool detecting;
    private float nextDetectCheck;
    private float hintUntil;
    private float battery = 1f;
    private WallClockController clock;
    private NightFog nightFog;
    private bool wasNight;
    private string batteryMessage;
    private float batteryMessageUntil;

    // 1 = full, 0 = empty
    public float Battery01 => battery;

    // The training: the battery never runs out (the clock stands still there, so the morning charge would never come)
    [System.NonSerialized] public bool freeBattery;

    // Full again at once (a new lesson)
    public void Recharge()
    {
        battery = 1f;
    }

    float CapacitySeconds
    {
        get
        {
            if (batterySeconds == null || batterySeconds.Length == 0) return 120f;
            return Mathf.Max(5f, batterySeconds[Mathf.Clamp(scannerLevel - 1, 0, batterySeconds.Length - 1)]);
        }
    }

    void ShowBatteryMessage(string text)
    {
        batteryMessage = text;
        batteryMessageUntil = Time.time + 2f;
    }

    ScanRange CurrentRange
    {
        get
        {
            if (ranges == null || ranges.Length == 0) return null;
            return ranges[Mathf.Clamp(scannerLevel - 1, 0, ranges.Length - 1)];
        }
    }

    public bool IsShown => shown;

    // The training's radar room: a plain model of the woman with no script at all. When it is set, the scanner reads it
    // (instead of the real ghost of the spawner).
    public static Transform OverrideGhost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOverride()
    {
        OverrideGhost = null;
    }
    public bool IsPowered => powered;

    void Start()
    {
        var pc = GetComponentInParent<PlayerController>();
        playerRoot = pc != null ? pc.transform : transform;

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) playerCamera = Camera.main;
        if (spawner == null) spawner = FindFirstObjectByType<GhostSpawner>();
        if (deviceObject == null) deviceObject = FindDeep(playerRoot, "Device Hand");

        if (deviceObject == null)
        {
            Debug.LogWarning("[GhostDevice] Scanner object not found. Run Tools > Ghost > Setup Scanner, or assign 'Device Hand' to Device Object.");
            enabled = false;
            return;
        }

        deviceRenderers = deviceObject.GetComponentsInChildren<Renderer>(true);

        SetupFollow();
        SetupLeds();
        powered = startPowered;
        SetShown(!startHidden);
        battery = 1f;
        clock = FindFirstObjectByType<WallClockController>();
        nightFog = FindFirstObjectByType<NightFog>();
        wasNight = IsNight();

        // The ghost's range must never block the player
        Transform ghost = spawner != null ? spawner.ghost : null;
        if (ghost == null)
        {
            var found = GameObject.Find("Womann@Sad Idle");
            if (found != null) ghost = found.transform;
        }
        Collider range = ghostRange != null ? ghostRange : (ghost != null ? ghost.GetComponent<BoxCollider>() : null);
        if (range != null) range.isTrigger = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(powerKey) || (shown && MobileInput.GetPower()))
        {
            bool wantOn = !shown || !powered;
            if (wantOn && battery < Mathf.Max(0.01f, minPowerOnCharge))
            {
                TakeOut();
                powered = false;
                ShowBatteryMessage("Scanner battery is too low");
            }
            else
            {
                if (!shown) TakeOut();
                powered = wantOn;
                PlayerSfx.Play("ScannerPower");         // O (or the phone button): the click of the scanner going on or off
            }
        }

        UpdateBattery();

        UpdateLeds();
    }

    void LateUpdate()
    {
        if (!followCameraLook || holder == null || playerCamera == null) return;

        // Keep the scanner's pose relative to the camera so it tilts with Y look
        Transform cam = playerCamera.transform;
        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * bobSpeed * Mathf.PI) * bobHeight);
        holder.SetPositionAndRotation(
            cam.position + cam.rotation * (holderLocalPos + bob),
            cam.rotation * holderLocalRot);
    }

    // ---------------------------------------------------------------- the battery

    void UpdateBattery()
    {
        // the level can come from an upgrade
        if (!string.IsNullOrEmpty(levelUpgradeId) && UpgradeSystem.Instance != null && UpgradeSystem.Instance.upgrades != null)
        {
            foreach (var u in UpgradeSystem.Instance.upgrades)
                if (u != null && u.id == levelUpgradeId) { if (u.level > 0) scannerLevel = u.level; break; }
        }

        if (freeBattery) battery = 1f;

        if (shown && powered && !freeBattery)
        {
            battery -= Time.deltaTime / CapacitySeconds;
            if (battery <= 0f)
            {
                battery = 0f;
                powered = false;
                PlayerSfx.Play("ScannerPower");
                ShowBatteryMessage("Scanner battery is empty");
            }
        }

        // the battery stays as it is all night, and is full again when the morning comes
        bool night = IsNight();
        if (wasNight && !night && rechargeInMorning && battery < 1f)
        {
            battery = 1f;
            ShowBatteryMessage("Morning: the scanner battery is charged");
        }
        wasNight = night;

        if (Time.time < batteryMessageUntil && !string.IsNullOrEmpty(batteryMessage))
            InteractPrompt.Request(batteryMessage);
        else if (shown && !powered && Time.time < hintUntil)
            InteractPrompt.Request("[" + powerKey + "] Open scanner to scan");   // the key gets its circle, like the other prompts
    }

    // The night hours of the NightFog (8 PM - 6 AM)
    bool IsNight()
    {
        if (clock == null) return false;

        float start = nightFog != null ? nightFog.fogStartHour : 20f;
        float end = nightFog != null ? nightFog.fogEndHour : 6f;
        float hour = clock.GameHour24;
        return start <= end ? hour >= start && hour < end : hour >= start || hour < end;
    }

    // ---------------------------------------------------------------- the scanner's lights

    void SetupLeds()
    {
        ledGreen = Resources.Load<Material>("Scanner/LED_Green");
        ledYellow = Resources.Load<Material>("Scanner/LED_Yellow");
        ledRed = Resources.Load<Material>("Scanner/LED_Red");
        ledDark = Resources.Load<Material>("Scanner/LED_Dark");

        ledOn = LedRenderer("ON");
        ledOff = LedRenderer("OFF");
        ledDetect = LedRenderer("DETECT");
        string[] names = { "ONE", "TWO", "THREE", "FOUR", "FIVE" };
        for (int i = 0; i < names.Length; i++) ledLevel[i] = LedRenderer(names[i]);

        if (ledOn == null && ledOff == null && ledDetect == null)
            Debug.LogWarning("[GhostDevice] The scanner's lights (ON, OFF, DETECT, ONE-FIVE) were not found under '" + deviceObject.name + "'.");
    }

    Renderer LedRenderer(string ledName)
    {
        foreach (var t in deviceObject.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != ledName) continue;
            var r = t.GetComponent<Renderer>();
            return r != null ? r : t.GetComponentInChildren<Renderer>(true);
        }
        return null;
    }

    static void SetLed(Renderer r, Material m)
    {
        if (r == null || m == null) return;

        var mats = r.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
        {
            bool isLedMaterial = mats[i] != null && (mats[i].name == "ON" || mats[i].name.StartsWith("LED_"));
            if (mats[i] != m && (mats.Length == 1 || isLedMaterial))
            {
                mats[i] = m;
                changed = true;
            }
        }
        if (changed) r.sharedMaterials = mats;
    }

    void UpdateLeds()
    {
        bool on = shown && powered;
        SetLed(ledOn, on ? ledGreen : ledDark);
        SetLed(ledOff, shown && !powered ? ledRed : ledDark);

        Transform ghost = OverrideGhost != null ? OverrideGhost : (spawner != null ? spawner.ghost : null);
        bool ghostShown = OverrideGhost != null || (spawner != null && spawner.GhostShown);
        bool ghostThere = on && ghostShown && ghost != null && playerCamera != null;
        float distance = ghostThere ? Vector3.Distance(playerRoot.position, ghost.position) : float.MaxValue;

        if (Time.time >= nextDetectCheck)
        {
            nextDetectCheck = Time.time + 0.1f;
            detecting = ghostThere && PointsAt(ghost, distance);
        }
        SetLed(ledDetect, ghostThere && detecting ? ledGreen : ledDark);

        int count = 0;
        var range = CurrentRange;
        if (ghostThere && range != null && range.levelDistances != null)
            foreach (float d in range.levelDistances)
                if (distance <= d * radiusScale) count++;

        for (int i = 0; i < ledLevel.Length; i++)
        {
            Material m = ledDark;
            if (i < count) m = i < 2 ? ledGreen : (i < 4 ? ledYellow : ledRed);
            SetLed(ledLevel[i], m);
        }
    }

    // The scanner points at the ghost, she is near, and no wall is in between
    bool PointsAt(Transform ghost, float distance)
    {
        var range = CurrentRange;
        if (range != null && distance > range.detectDistance * radiusScale) return false;

        Vector3 from = playerCamera.transform.position;
        Vector3 to = ghost.position + Vector3.up;
        if (Vector3.Angle(playerCamera.transform.forward, to - from) > detectAngle) return false;
        if (!detectNeedsLineOfSight) return true;

        var hits = Physics.RaycastAll(from, (to - from).normalized, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(ghost) || h.transform.IsChildOf(playerRoot)) continue;
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- show / hide

    void SetupFollow()
    {
        if (!followCameraLook || playerCamera == null) return;

        holder = deviceObject.transform;
        Transform cam = playerCamera.transform;
        Quaternion inverse = Quaternion.Inverse(cam.rotation);
        holderLocalPos = inverse * (holder.position - cam.position);
        holderLocalRot = inverse * holder.rotation;
    }

    // O with the scanner in the pocket takes it out through the hotbar (so the gun goes away)
    void TakeOut()
    {
        if (Hotbar.Instance != null) Hotbar.Instance.Press(2);
        if (!shown) SetShown(true);
    }

    // The hotbar (key 2) takes the scanner out and puts it away
    public void SetOut(bool value)
    {
        if (value != shown) SetShown(value);
    }

    // Only the renderers are switched, never the object itself
    void SetShown(bool value)
    {
        // taken out but not switched on: a hint for 5 seconds
        if (value && !shown && !powered) hintUntil = Time.time + 5f;

        shown = value;
        foreach (var r in deviceRenderers)
            if (r != null) r.enabled = value;
    }

    static GameObject FindDeep(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t.gameObject;
        return null;
    }
}
