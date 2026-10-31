using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The hotbar at the bottom of the screen, like Minecraft: three slots.
//   1 = the gun, 2 = the scanner (radio), 3 = the injector (heals you).
// Press the key (or tap the slot) to take the thing out, press it again to put it away. Only one thing is in the hand at a time:
// taking one out puts the other away (and the backpack item too). The injector is used with the left click (or the hand button).
// It makes itself in the game scene.
public class Hotbar : MonoBehaviour
{
    public static Hotbar Instance { get; private set; }

    [Header("Look (canvas units)")]
    public float slotSize = 84f;
    [Tooltip("A phone's finger needs a bigger slot")]
    public float phoneSlotSize = 104f;
    public float spacing = 10f;
    public float bottom = 18f;

    [Header("Injector")]
    public int injectors = 3;
    public int maxInjectors = 5;
    public int healAmount = 50;
    public float useSeconds = 1.3f;
    [Tooltip("The size of the injector in the hand (its longest side, in metres)")]
    public float heldLengthUpright = 0.26f;
    public Vector3 heldPositionLow = new Vector3(0.2f, -0.145f, 0.5f);
    [Tooltip("How the injector is held: it stands up (the red cap on top) and its label faces the player. X = leans away from you, Y = turns towards you (degrees)")]
    public Vector2 heldPitchAndTurn = new Vector2(10f, -5f);
    [Tooltip("How much the top of the injector leans to the side, as it looks on the screen (degrees). Positive = the red cap leans towards the centre of the screen, negative = away from it")]
    public float heldLeanDegrees = -17f;

    // the pose of the injector in the hand (the lean is written as it looks: a little of the roll is taken by the turn and the pitch)
    Vector3 HeldAngles { get { return new Vector3(heldPitchAndTurn.x, heldPitchAndTurn.y, heldLeanDegrees + 4f); } }

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.72f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.86f, 0.12f, 0.16f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    // the pictures of UI FINAL (copied to Resources/UpgradeIcons): the number is drawn here, in the game's own style
    private static readonly string[] SpriteNames = { "UpgradeIcons/Gun", "UpgradeIcons/Radio", "UpgradeIcons/Injector" };
    private static readonly KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
    private static readonly KeyCode[] PadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3 };

    private class Slot
    {
        public RectTransform root;
        public Image picture;
        public Image edge;
        public TMP_Text count;
        public Image badge;
        public TMP_Text number;
    }

    private readonly Slot[] slots = new Slot[3];
    private CanvasGroup group;

    private GhostGun gun;
    private GhostDevice scanner;
    private PlayerHealth health;
    private Camera playerCamera;

    // 0 = nothing, 1..3 = the slot in the hand
    private int selected;
    public int Selected => selected;

    private Transform heldInjector;
    private float heldAt;
    private float usingUntil;
    private bool healed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoCreate();
    }

    static void AutoCreate()
    {
        if (FindFirstObjectByType<PlayerController>() == null || FindFirstObjectByType<Hotbar>() != null) return;
        new GameObject("Hotbar (auto)").AddComponent<Hotbar>();
    }

    void Start()
    {
        Instance = this;
        gun = GhostGun.Instance != null ? GhostGun.Instance : FindFirstObjectByType<GhostGun>();
        scanner = FindFirstObjectByType<GhostDevice>();
        health = FindFirstObjectByType<PlayerHealth>();
        playerCamera = Camera.main;

        Build();
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- the bar

    void Build()
    {
        var canvasGo = new GameObject("HotbarCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 68;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();
        group = canvasGo.AddComponent<CanvasGroup>();

        // a phone needs an event system to tap the slots; the game has one, but make sure
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        float size = DeviceDetection.IsMobile() ? phoneSlotSize : slotSize;
        float total = slots.Length * size + (slots.Length - 1) * spacing;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = new Slot();
            slots[i] = s;

            s.root = UiKit.NewRect("Slot " + (i + 1), canvasGo.transform);
            s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 0f);
            s.root.pivot = new Vector2(0.5f, 0f);
            s.root.sizeDelta = new Vector2(size, size);
            s.root.anchoredPosition = new Vector2(-total * 0.5f + size * 0.5f + i * (size + spacing), bottom);

            var back = s.root.gameObject.AddComponent<Image>();
            back.sprite = UiKit.Round;
            back.type = Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = UiKit.Border9 / 14f;
            back.color = Plate;

            var pic = UiKit.NewRect("Picture", s.root);
            UiKit.Stretch(pic);
            pic.offsetMin = new Vector2(6f, 6f);
            pic.offsetMax = new Vector2(-6f, -6f);
            s.picture = pic.gameObject.AddComponent<Image>();
            s.picture.sprite = Resources.Load<Sprite>(SpriteNames[i]);
            s.picture.preserveAspect = true;
            s.picture.raycastTarget = false;

            s.edge = UiKit.Shape(s.root, "Edge", UiKit.RoundOutline, Edge, 14f, Vector2.zero, new Vector2(size, size));
            UiKit.Stretch(s.edge.rectTransform);

            // the key number: the same little plate as the other keys of the HUD, on the bottom right corner
            s.number = HudKeys.CornerKey(s.root, (i + 1).ToString(), size * 0.38f, new Vector2(size * 0.055f, size * 0.11f));

            if (i == 2)
            {
                s.count = UiKit.Label(s.root, "Count", "", UiKit.DmSemi, size * 0.26f, Bone, TextAlignmentOptions.TopRight,
                    Vector2.zero, new Vector2(size - 10f, size - 6f));
                s.count.raycastTarget = false;
                s.count.gameObject.AddComponent<LocalizeIgnore>();
            }

            var button = s.root.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
            int index = i + 1;
            button.onClick.AddListener(() => Press(index));
        }
    }

    // ---------------------------------------------------------------- taking things out

    void Update()
    {
        bool typing = UiLock.Open;
        bool backpack = Inventory.Instance != null && Inventory.Instance.IsOpen;

        // the slots hide while a full panel is open
        if (group != null)
        {
            bool show = !UiLock.Open;
            group.alpha = show ? 1f : 0f;
            group.blocksRaycasts = show;
        }

        if (!typing && !backpack)
        {
            for (int i = 0; i < Keys.Length; i++)
                if (Input.GetKeyDown(Keys[i]) || Input.GetKeyDown(PadKeys[i])) Press(i + 1);
        }

        // something else put the thing away (the backpack item, the gun after a hit): follow it
        if (selected == 1 && (gun == null || !gun.IsHeld)) { selected = 0; PlayerSfx.Play("GunTakeOut"); }
        if (selected == 2 && (scanner == null || !scanner.IsShown)) { selected = 0; PlayerSfx.Play("ScannerTakeOut"); }

        UpdateInjector(typing || backpack);
        Refresh();
    }

    // The same press for a key and for a tap
    public void Press(int slot)
    {
        if (UiLock.Open) return;
        if (usingUntil > Time.time) return;   // the injector is working

        Select(selected == slot ? 0 : slot);
    }

    void Select(int slot)
    {
        // only one thing in the hand
        if (slot != 0 && Inventory.Instance != null) Inventory.Instance.Unequip();

        int before = selected;
        selected = slot;
        if (slot != 0 && slot != before) PlayerSfx.Play(slot == 1 ? "GunTakeOut" : slot == 2 ? "ScannerTakeOut" : "InjectorTakeOut");
        else if (slot == 0 && before == 1) PlayerSfx.Play("GunTakeOut");          // the gun is put away: the same sound
        else if (slot == 0 && before == 2) PlayerSfx.Play("ScannerTakeOut");      // the radar is put away: the same sound

        if (gun != null)
        {
            if (slot == 1 && !gun.IsHeld) gun.SetHeld(true);
            else if (slot != 1 && gun.IsHeld) gun.HolsterGun();
        }

        if (scanner != null) scanner.SetOut(slot == 2);

        ShowInjector(slot == 3);
    }

    // The backpack puts an item in the hand: the bar lets go of everything
    public void Clear()
    {
        if (selected != 0) Select(0);
    }

    // ---------------------------------------------------------------- the injector

    public void AddInjector(int amount = 1)
    {
        injectors = Mathf.Min(maxInjectors, injectors + amount);
    }

    void ShowInjector(bool value)
    {
        if (!value)
        {
            if (heldInjector != null) Destroy(heldInjector.gameObject);
            heldInjector = null;
            return;
        }

        if (heldInjector != null) return;

        var prefab = Resources.Load<GameObject>("Hotbar/InjectorHeld");
        if (prefab == null) return;

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        var pivot = new GameObject("Injector (hand)").transform;
        pivot.SetParent(playerCamera.transform, false);
        pivot.localPosition = heldPositionLow;
        pivot.localRotation = Quaternion.Euler(HeldAngles);

        var model = Instantiate(prefab, pivot, false).transform;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // The injector stands up: the red cap on top, the silver nozzle at the bottom, and the label (GCC MEDICAL BCR-01) faces the player.
        // In the model's own axes as Unity imports it (the fbx is mirrored on X): the red cap is on -X, the valve on +Z, the label on -Y.
        // So: the model's Y goes away from the camera (the label faces it), its -X (the cap) goes up, and its Z goes to the left.
        model.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.forward);

        // it is fitted to a hand size and centred on the pivot
        Bounds bounds;
        if (TryBounds(model, out bounds))
        {
            float longestNow = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            model.localScale = Vector3.one * (heldLengthUpright / Mathf.Max(0.0001f, longestNow));
            if (TryBounds(model, out bounds))
                model.position += pivot.position - bounds.center;
        }

        heldInjector = pivot;
        heldAt = Time.time;          // it comes up into the hand (see UpdateInjector)
    }

    static bool TryBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // The key poses of the USE move, added to the held pose (camera space: x right, y up, z forward; degrees)
    //            0 start      0.22 lifted and drawn back   0.40 aims     0.50 stab (health)   0.68 plunger    0.85 back      1 rest
    static readonly float[] UseTime = { 0f, 0.22f, 0.40f, 0.50f, 0.68f, 0.85f, 1f };
    static readonly Vector3[] UsePosition =
    {
        Vector3.zero, new Vector3(0.02f, 0.09f, -0.07f), new Vector3(-0.05f, 0.03f, -0.07f), new Vector3(-0.07f, -0.10f, -0.05f),
        new Vector3(-0.07f, -0.115f, -0.05f), new Vector3(-0.03f, -0.03f, -0.02f), Vector3.zero
    };
    static readonly Vector3[] UseRotation =
    {
        Vector3.zero, new Vector3(-12f, 0f, -10f), new Vector3(6f, 0f, -24f), new Vector3(14f, 0f, -30f),
        new Vector3(14f, 0f, -30f), new Vector3(6f, 0f, -12f), Vector3.zero
    };

    // The pose at time t (0..1): the key poses are joined smoothly
    static void SampleUse(float t, out Vector3 position, out Vector3 rotation)
    {
        int i = 0;
        while (i < UseTime.Length - 2 && t > UseTime[i + 1]) i++;

        float k = Mathf.InverseLerp(UseTime[i], UseTime[i + 1], t);
        k = k * k * (3f - 2f * k);
        position = Vector3.Lerp(UsePosition[i], UsePosition[i + 1], k);
        rotation = Vector3.Lerp(UseRotation[i], UseRotation[i + 1], k);
    }

    void UpdateInjector(bool blocked)
    {
        if (selected != 3 || heldInjector == null) return;

        float now = Time.time;

        // USE: lifted and drawn back, then it stabs down and inward (the health comes at the middle of the move), the plunger is pressed
        // (it shakes), and it comes back
        if (usingUntil > now)
        {
            float t = 1f - (usingUntil - now) / useSeconds;      // 0..1
            Vector3 movePosition, moveRotation;
            SampleUse(t, out movePosition, out moveRotation);

            float press = Mathf.Clamp01(1f - Mathf.Abs(t - 0.6f) / 0.12f);      // the plunger is pressed around 0.6
            float shake = Mathf.Sin(now * 80f) * press;
            movePosition += new Vector3(shake * 0.003f, -press * 0.012f, 0f);
            moveRotation += new Vector3(shake * 1.5f, 0f, shake * 1.5f);

            heldInjector.localPosition = heldPositionLow + movePosition;
            heldInjector.localRotation = Quaternion.Euler(HeldAngles + moveRotation);

            if (!healed && t >= 0.5f)
            {
                healed = true;
                if (health != null) health.Heal(healAmount);
                PlayerSfx.Voice("Revive");
            }
            return;
        }

        // IDLE: it breathes with the hand (a small slow float and sway). When it is taken out it rises from below
        Vector3 idlePosition = new Vector3(Mathf.Sin(now * 1.1f) * 0.003f, Mathf.Sin(now * 1.7f) * 0.005f, Mathf.Sin(now * 0.8f) * 0.002f);
        Vector3 idleRotation = new Vector3(Mathf.Sin(now * 1.2f + 1f) * 1.2f, Mathf.Sin(now * 0.9f) * 1.5f, Mathf.Sin(now * 1.0f + 2f) * 1.5f);

        float up = Mathf.Clamp01((now - heldAt) / 0.35f);
        float away = (1f - up) * (1f - up);                   // 1 = just taken out (low, tilted), 0 = in the hand
        idlePosition += new Vector3(0.03f, -0.22f, -0.04f) * away;
        idleRotation += new Vector3(35f, 0f, 0f) * away;

        heldInjector.localPosition = heldPositionLow + idlePosition;
        heldInjector.localRotation = Quaternion.Euler(HeldAngles + idleRotation);

        if (blocked) return;

        bool phone = DeviceDetection.IsMobile();
        bool use = phone ? MobileInput.GetInteract()
                         : Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked;
        if (!use) return;

        if (health == null || health.IsDead) return;
        if (injectors <= 0) { InteractPrompt.Request("No injectors left"); return; }
        if (health.currentHealth >= health.maxHealth) { InteractPrompt.Request("Health is full"); return; }

        injectors--;
        healed = false;
        usingUntil = now + useSeconds;
        PlayerSfx.Play("InjectorUse");
    }

    // ---------------------------------------------------------------- the look

    void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            bool on = selected == i + 1;
            s.edge.color = on ? Blood : Edge;
            s.picture.color = on ? Color.white : new Color(1f, 1f, 1f, 0.8f);
            s.number.color = on ? Blood : Bone;
            s.root.localScale = Vector3.one * (on ? 1.08f : 1f);
        }

        if (slots[2] != null && slots[2].count != null)
        {
            slots[2].count.text = injectors > 0 ? "x" + injectors : "0";
            if (slots[2].picture != null && injectors <= 0)
                slots[2].picture.color = new Color(1f, 1f, 1f, 0.3f);
        }
    }
}
