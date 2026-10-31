using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The HUD of the game scene, drawn as one family when the game starts (nothing to set up).
//   Colours: charcoal plates, blood red, bone white. Nothing else.
//   Shapes:  angled plates (two cut corners) with a thin dark-red edge and a little scratched texture.
//   Text:    the game font (MetalMania) for the clock and the warnings, DM Sans for sentences.
//
//   Top left     Health: a heart medallion and a segmented bar with the number. It throbs red when health is low.
//   Top right    Clock plate, and the settings gear on its own round plate under it.
//   Top centre   FPS and battery (small, bone coloured; red only when slow / low).
//   Bottom right Heat of the gun (only while the gun is out): a flame medallion and a bar. Bone, turns red when hot.
//   Also         the action button, the joystick and the phone buttons get the same dark plates and thin rings.
// Your own objects (HealthBar, Heat, Clock) keep working: they are made invisible and this HUD reads their values.
public class HudStyler : MonoBehaviour
{
    public static TMP_FontAsset GameFont { get; private set; }

    public static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 1f);
    public static readonly Color Blood = new Color(0.70f, 0.08f, 0.12f, 1f);
    public static readonly Color BloodHot = new Color(0.93f, 0.14f, 0.18f, 1f);
    public static readonly Color PlateColor = new Color(0.04f, 0.04f, 0.045f, 0.74f);
    public static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);

    private class Bar
    {
        public RectTransform root;
        public RectTransform fill;
        public Image fillImage;
        public Image glow;
        public Image medal;
        public Image icon;
        public TMP_Text value;
        public float width;
    }

    private Canvas hud;
    private PlayerHealth health;
    private GhostGun gun;
    private Bar healthBar;
    private Bar heatBar;
    private Bar batteryBar;
    private GhostDevice scanner;
    private TMP_Text clockSource;
    private TMP_Text clockLabel;
    private string lastClock;
    private TMP_Text dayLabel;
    private GameObject daySeparator;
    private int lastDay = 1;
    private bool gearDone;
    private bool buttonsDone;
    private bool backpackDone;
    private float startedAt;

    private static bool hooked;

    // The built game starts in the intro scene and loads the game scene later, so this must also run on every scene load
    // (before, the HUD was only styled when the game scene was the first one: in the editor, not on the web page)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!hooked)
        {
            hooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Create();
        }
        Create();
    }

    static void Create()
    {
        // only in the game scene (the one with the player's health)
        if (FindFirstObjectByType<PlayerHealth>() == null) return;
        if (FindFirstObjectByType<HudStyler>() != null) return;

        new GameObject("HudStyler").AddComponent<HudStyler>();
    }

    // A phone browser's address bar covers the top edge of the page: the top items sit a little lower there
    static float TopInset { get { return DeviceDetection.IsMobile() ? 28f : 0f; } }

    void Start()
    {
        startedAt = Time.unscaledTime;
        health = FindFirstObjectByType<PlayerHealth>();
        gun = GhostGun.Instance;

        var clock = FindFirstObjectByType<WallClockController>();
        if (clock != null && clock.timeText != null)
        {
            clockSource = clock.timeText;
            GameFont = clockSource.font;   // MetalMania: the font of the game
        }

        BuildCanvas();

        if (health != null && health.healthSlider != null)
        {
            Hide(health.healthSlider.gameObject);
            healthBar = MakeBar("Health", "heart-pulse", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -52f - TopInset), 470f, 40f, Blood);
        }

        if (gun != null && gun.heatSlider != null)
        {
            Hide(gun.heatSlider.gameObject);   // it stays active: the gun switches it on and off with the gun
            heatBar = MakeBar("Heat", "Heat", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 78f), 300f, 34f, Bone);
        }

        // the scanner's battery: a bar like the gun's heat bar, just above it (only while the scanner is out)
        scanner = FindFirstObjectByType<GhostDevice>();
        if (scanner != null)
            batteryBar = MakeBar("Battery", "GunBattery", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-440f, 110f), 300f, 34f, Bone);

        if (clockSource != null)
        {
            Hide(clockSource.gameObject);
            if (clockSource.GetComponent<LocalizeIgnore>() == null) clockSource.gameObject.AddComponent<LocalizeIgnore>();
            BuildClock();
        }

        StyleStatus();
        StyleAction();
        StyleJoystick();

        new GameObject("LowHealthWarning").AddComponent<LowHealthWarning>();
    }

    void Update()
    {
        // The settings gear and the phone buttons are made a moment after the scene starts
        if (!gearDone && SettingsPanel.Instance != null && SettingsPanel.Instance.gearButton != null)
        {
            StyleGear(SettingsPanel.Instance.gearButton);
            gearDone = true;
        }

        if (!buttonsDone && Time.unscaledTime - startedAt > 1.5f)
        {
            StylePhoneButtons();
            buttonsDone = true;
        }

        // the backpack is styled once the Inventory has finished setting itself up
        if (!backpackDone && Time.unscaledTime - startedAt > 0.6f && Inventory.Instance != null && Inventory.Instance.panelRoot != null)
        {
            StyleBackpack();
            backpackDone = true;
        }

        if (clockSource != null && clockLabel != null && clockSource.text != lastClock)
        {
            lastClock = clockSource.text;
            clockLabel.text = lastClock;
        }

        // DAY 1, DAY 2...: one more at every dawn. Not in the training (it has no days).
        if (dayLabel != null)
        {
            bool showDay = !TutorialMode.Active;
            if (dayLabel.gameObject.activeSelf != showDay) { dayLabel.gameObject.SetActive(showDay); daySeparator.SetActive(showDay); }

            int day = AgentStats.Nights + 1;
            if (showDay && day != lastDay)
            {
                lastDay = day;
                dayLabel.text = "DAY " + day;
            }
        }

        // the scanner's bar only shows while the scanner is out
        if (batteryBar != null)
            batteryBar.root.gameObject.SetActive(scanner != null && scanner.IsShown);

        // the gun's bar only shows while the gun is out
        if (heatBar != null && gun != null)
            heatBar.root.gameObject.SetActive(gun.heatSlider != null && gun.heatSlider.gameObject.activeInHierarchy);
    }

    void LateUpdate()
    {
        if (healthBar != null && health != null)
        {
            float share = Mathf.Clamp01(health.currentHealth / (float)Mathf.Max(1, health.maxHealth));
            float rate = share <= 0.15f ? 6.5f : 3.6f;
            float pulse = share <= 0.3f && !health.IsDead ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * rate) : 0f;

            SetFill(healthBar, share);
            healthBar.fillImage.color = Color.Lerp(Blood, BloodHot, pulse);
            SetAlpha(healthBar.glow, 0.3f * pulse);
            healthBar.medal.rectTransform.localScale = Vector3.one * (1f + 0.1f * pulse);
            healthBar.icon.color = Color.Lerp(Bone, BloodHot, pulse);
            healthBar.value.text = Mathf.CeilToInt(health.currentHealth).ToString();
        }

        if (batteryBar != null && scanner != null)
        {
            float b = Mathf.Clamp01(scanner.Battery01);
            bool low = b < 0.25f;
            float pulse = low && scanner.IsPowered ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;

            SetFill(batteryBar, b);
            batteryBar.fillImage.color = Color.Lerp(Bone, BloodHot, Mathf.Max(low ? 0.75f : 0f, pulse));
            SetAlpha(batteryBar.glow, 0.3f * pulse);
            batteryBar.icon.color = Color.Lerp(Bone, BloodHot, low ? Mathf.Max(0.6f, pulse) : 0f);
        }

        if (heatBar != null && gun != null && gun.heatSlider != null)
        {
            float h = Mathf.Clamp01(gun.heatSlider.value);
            bool hot = gun.HeatPercent > 0.8f || gun.IsCoolingDown;
            float pulse = hot ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;

            SetFill(heatBar, h);
            heatBar.fillImage.color = Color.Lerp(Bone, BloodHot, Mathf.Max(Mathf.Clamp01((gun.HeatPercent - 0.45f) * 1.8f), pulse * 0.8f));
            SetAlpha(heatBar.glow, 0.3f * pulse);
            heatBar.icon.color = Color.Lerp(Bone, BloodHot, Mathf.Max(gun.HeatPercent * 0.7f, pulse));
        }
    }

    // ---------------------------------------------------------------- canvas and plates

    void BuildCanvas()
    {
        var go = new GameObject("HudCanvas");
        go.transform.SetParent(transform, false);

        hud = go.AddComponent<Canvas>();
        hud.renderMode = RenderMode.ScreenSpaceOverlay;
        hud.sortingOrder = 70;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();     // the settings gear lives here now: it can be clicked
        go.AddComponent<HorrorSkinIgnore>();
    }

    // Makes your own object invisible (it keeps running, so the game logic that uses it is not affected)
    static void Hide(GameObject go)
    {
        var group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // An angled dark plate with a thin dark-red edge and a little scratched texture
    public static Image MakePlate(Transform parent, string name, float radius, Vector2 size)
    {
        var plate = UiKit.Shape(parent, name, UiKit.Cut, PlateColor, radius, Vector2.zero, size);

        var grunge = UiKit.NewRect("Grunge", plate.transform);
        UiKit.Stretch(grunge);
        grunge.offsetMin = new Vector2(6f, 6f);
        grunge.offsetMax = new Vector2(-6f, -6f);
        var grungeImage = grunge.gameObject.AddComponent<Image>();
        grungeImage.sprite = UiKit.Grunge;
        grungeImage.color = new Color(1f, 0.9f, 0.85f, 0.06f);
        grungeImage.raycastTarget = false;

        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, Edge, radius, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);
        return plate;
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    // ---------------------------------------------------------------- bars

    Bar MakeBar(string name, string iconName, Vector2 anchor, Vector2 pivot, Vector2 position, float width, float height, Color fillColor)
    {
        var bar = new Bar { width = width };

        var plate = MakePlate(hud.transform, name, height * 0.5f, new Vector2(width, height));
        bar.root = plate.rectTransform;
        bar.root.anchorMin = bar.root.anchorMax = anchor;
        bar.root.pivot = pivot;
        bar.root.anchoredPosition = position;

        // a red glow that throbs when the warning is on
        var glowRt = UiKit.NewRect("Glow", bar.root);
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = new Vector2(-18f, -10f);
        glowRt.offsetMax = new Vector2(18f, 10f);
        bar.glow = glowRt.gameObject.AddComponent<Image>();
        bar.glow.sprite = UiKit.Glow;
        bar.glow.type = Image.Type.Sliced;
        bar.glow.pixelsPerUnitMultiplier = 1f;
        bar.glow.color = new Color(0.9f, 0.1f, 0.12f, 0f);
        bar.glow.raycastTarget = false;

        // the fill, with 10 thin dividers over it
        var area = UiKit.NewRect("Fill Area", bar.root);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(height + 6f, 6f);
        area.offsetMax = new Vector2(-8f, -6f);

        var fill = UiKit.NewRect("Fill", area);
        bar.fill = fill;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        bar.fillImage = fill.gameObject.AddComponent<Image>();
        bar.fillImage.sprite = UiKit.Cut;
        bar.fillImage.type = Image.Type.Sliced;
        bar.fillImage.pixelsPerUnitMultiplier = UiKit.Border9 / ((height - 12f) * 0.5f);
        bar.fillImage.color = fillColor;
        bar.fillImage.raycastTarget = false;

        for (int i = 1; i < 10; i++)
        {
            var tick = UiKit.NewRect("Tick", area);
            tick.anchorMin = tick.anchorMax = new Vector2(i / 10f, 0.5f);
            tick.sizeDelta = new Vector2(2f, height - 12f);
            var tickImage = tick.gameObject.AddComponent<Image>();
            tickImage.color = new Color(0f, 0f, 0f, 0.4f);
            tickImage.raycastTarget = false;
        }

        // the number
        bar.value = UiKit.Label(bar.root, "Value", "", UiKit.DmSemi != null ? UiKit.DmSemi : GameFont, 22f,
            new Color(Bone.r, Bone.g, Bone.b, 0.9f), TextAlignmentOptions.MidlineRight, new Vector2(0f, 0f), new Vector2(width - 20f, height));
        bar.value.gameObject.SetActive(false);   // no number on the bars
        bar.value.gameObject.AddComponent<LocalizeIgnore>();

        // the medallion that says what the bar is
        float medal = height + 34f;
        var disc = UiKit.Shape(bar.root, "Medallion", UiKit.Circle, new Color(0.03f, 0.03f, 0.035f, 0.92f), 0f, new Vector2(-width * 0.5f + 8f, 0f), new Vector2(medal, medal));
        disc.type = Image.Type.Simple;
        bar.medal = disc;

        var ring = UiKit.NewRect("Ring", disc.transform);
        UiKit.Stretch(ring);
        var ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = UiKit.Ring;
        ringImage.color = new Color(Edge.r + 0.2f, Edge.g + 0.04f, Edge.b + 0.05f, 0.95f);
        ringImage.raycastTarget = false;

        bar.icon = UiKit.IconImage(disc.transform, "Icon", iconName, Bone, Vector2.zero, medal * 0.5f);
        return bar;
    }

    static void SetFill(Bar bar, float share)
    {
        bar.fill.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
    }

    // ---------------------------------------------------------------- clock and gear

    void BuildClock()
    {
        var plate = MakePlate(hud.transform, "Clock", 18f, new Vector2(240f, 64f));
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-48f, -38f - TopInset);

        var layout = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 26, 8, 8);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = plate.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;   // the plate is 64 high, whatever the font's line height

        // the plate's own decoration must not take part in the layout
        foreach (Transform child in plate.transform)
            child.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // the day of the run: DAY 1, DAY 2... (a new day starts at dawn, when the night is survived). It sits left of the clock.
        var dayRt = UiKit.NewRect("Day", plate.transform);
        dayLabel = dayRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (GameFont != null) dayLabel.font = GameFont;
        dayLabel.fontSize = 36f;
        dayLabel.color = new Color(0.86f, 0.12f, 0.16f, 1f);
        dayLabel.alignment = TextAlignmentOptions.Midline;
        dayLabel.raycastTarget = false;
        dayLabel.overflowMode = TextOverflowModes.Overflow;
        dayLabel.characterSpacing = 3f;
        dayLabel.text = "DAY 1";

        var sepRt = UiKit.NewRect("Day Separator", plate.transform);
        var sep = sepRt.gameObject.AddComponent<Image>();
        sep.color = new Color(Bone.r, Bone.g, Bone.b, 0.35f);
        sep.raycastTarget = false;
        var sepLayout = sepRt.gameObject.AddComponent<LayoutElement>();
        sepLayout.preferredWidth = 2f;
        sepLayout.preferredHeight = 34f;
        daySeparator = sepRt.gameObject;
        lastDay = 1;

        var iconRt = UiKit.NewRect("Icon", plate.transform);
        var icon = iconRt.gameObject.AddComponent<Image>();
        icon.sprite = UiKit.Icon("Time") != null ? UiKit.Icon("Time") : UiKit.Icon("clock");
        icon.preserveAspect = true;
        icon.color = new Color(Bone.r, Bone.g, Bone.b, 0.8f);
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;
        var iconLayout = iconRt.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 30f;
        iconLayout.preferredHeight = 30f;

        var textRt = UiKit.NewRect("Time", plate.transform);
        clockLabel = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (GameFont != null) clockLabel.font = GameFont;
        clockLabel.fontSize = 36f;
        clockLabel.color = Bone;
        clockLabel.alignment = TextAlignmentOptions.Midline;
        clockLabel.raycastTarget = false;
        clockLabel.overflowMode = TextOverflowModes.Overflow;
        clockLabel.text = clockSource != null ? clockSource.text : "";
        lastClock = clockLabel.text;
    }

    // The gear: a small round plate under the clock
    void StyleGear(Button gear)
    {
        // The gear sits in the scene's own canvas (a fixed pixel size), the "?" button in a canvas that scales with the screen: they never
        // matched. The gear moves into the HUD canvas (same scale as the clock and the "?"), and both round plates are centred on the
        // same line: the centre of the "?" plate is 174 from the right edge, the gear's is 84, both 153 from the top.
        var rt = (RectTransform)gear.transform;
        rt.SetParent(hud.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(46f, 46f);
        rt.anchoredPosition = new Vector2(-84f, -153f - TopInset);

        // the new gear picture (UI FINAL > Setting)
        var gearImage = gear.GetComponent<Image>();
        var gearSprite = UiKit.Icon("Setting");
        if (gearImage != null && gearSprite != null) gearImage.sprite = gearSprite;

        var colors = gear.colors;
        colors.normalColor = new Color(Bone.r, Bone.g, Bone.b, 0.85f);
        colors.highlightedColor = BloodHot;
        colors.selectedColor = colors.normalColor;
        gear.colors = colors;

        // the round plate behind it (a sibling just before the gear, so it is drawn under it)
        var backRt = UiKit.NewRect("GearBack", rt.parent);
        backRt.anchorMin = backRt.anchorMax = new Vector2(1f, 1f);
        backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.sizeDelta = new Vector2(70f, 70f);
        backRt.anchoredPosition = new Vector2(-84f, -153f - TopInset);
        backRt.SetSiblingIndex(rt.GetSiblingIndex());

        var back = backRt.gameObject.AddComponent<Image>();
        back.sprite = UiKit.Circle;
        back.color = PlateColor;
        back.raycastTarget = false;

        var ring = UiKit.NewRect("Ring", backRt);
        UiKit.Stretch(ring);
        var ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = UiKit.Ring;
        ringImage.color = Edge;
        ringImage.raycastTarget = false;
    }

    // ---------------------------------------------------------------- top centre

    // The heat bar sits bottom right on a PC, bottom center on a phone (the GUN and JUMP buttons are on the right there)
    public void PlaceHeat(bool phone)
    {
        if (batteryBar != null)
        {
            var b = batteryBar.root;
            // above the hotbar on a PC, above the left of the heat bar on a phone
            b.anchorMin = b.anchorMax = new Vector2(0.5f, 0f);
            if (phone)
            {
                b.pivot = new Vector2(1f, 0f);
                b.anchoredPosition = new Vector2(-300f, 140f);
            }
            else
            {
                // centred above the hotbar (the scanner is slot 2); the round icon sticks out on the left, so a little to the right
                b.pivot = new Vector2(0.5f, 0f);
                b.anchoredPosition = new Vector2(34f, 152f);
            }
        }

        if (heatBar == null) return;

        var rt = heatBar.root as RectTransform;
        if (rt == null) return;

        if (phone)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 140f);   // above the hotbar
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-330f, 78f);
        }
    }

    void StyleStatus()
    {
        var status = FindFirstObjectByType<SystemStatus>();
        if (status == null) return;

        // one calm colour for the numbers: bone. Red only when the frame rate is bad or the battery is low
        status.goodColor = new Color(Bone.r, Bone.g, Bone.b, 0.85f);
        status.okColor = status.goodColor;
        status.chargingColor = status.goodColor;
        status.badColor = BloodHot;

        var font = UiKit.DmMedium;
        if (font != null)
        {
            if (status.fpsText != null) status.fpsText.font = font;
            if (status.batteryText != null) status.batteryText.font = font;
        }
        var statusRt = status.transform as RectTransform;
        if (statusRt != null && TopInset > 0f) statusRt.anchoredPosition += new Vector2(0f, -TopInset);

        if (status.fpsText != null) status.fpsText.fontSize = 28f;
        if (status.batteryText != null) status.batteryText.fontSize = 28f;
    }

    // ---------------------------------------------------------------- the action button and the joystick

    void StyleAction()
    {
        var input = FindFirstObjectByType<MobileInput>(FindObjectsInactive.Include);
        if (input == null) return;

        var go = input.gameObject;
        if (go.GetComponent<HorrorSkinIgnore>() == null) go.AddComponent<HorrorSkinIgnore>();

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(156f, 156f);
        rt.anchoredPosition = new Vector2(835f, -382f);

        var image = go.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = UiKit.Circle;
            image.type = Image.Type.Simple;
            image.color = new Color(0.04f, 0.04f, 0.045f, 0.66f);
        }

        var button = go.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;
        }

        // the old letter is replaced by a clean icon
        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.gameObject.SetActive(false);

        var ringRt = UiKit.NewRect("Ring", rt);
        UiKit.Stretch(ringRt);
        var ring = ringRt.gameObject.AddComponent<Image>();
        ring.sprite = UiKit.Ring;
        ring.color = Edge;                       // the same dark red ring as the GUN and JUMP buttons
        ring.raycastTarget = false;

        UiKit.IconImage(rt, "Icon", "hand", new Color(Bone.r, Bone.g, Bone.b, 0.92f), Vector2.zero, 70f);
    }

    void StyleJoystick()
    {
        var joystick = FindFirstObjectByType<Joystick>(FindObjectsInactive.Include);
        if (joystick == null) return;

        RectTransform background = joystick.transform.Find("Background") as RectTransform;
        if (background == null && joystick.transform.childCount > 0) background = joystick.transform.GetChild(0) as RectTransform;
        if (background == null) return;

        var back = background.GetComponent<Image>();
        if (back != null)
        {
            back.sprite = UiKit.Circle;
            back.type = Image.Type.Simple;
            back.color = new Color(0.03f, 0.03f, 0.035f, 0.4f);
        }

        var ringRt = UiKit.NewRect("Ring", background);
        UiKit.Stretch(ringRt);
        var ring = ringRt.gameObject.AddComponent<Image>();
        ring.sprite = UiKit.Ring;
        ring.color = new Color(Bone.r, Bone.g, Bone.b, 0.4f);
        ring.raycastTarget = false;
        ringRt.SetAsFirstSibling();

        var handle = background.Find("Handle");
        if (handle != null)
        {
            var handleImage = handle.GetComponent<Image>();
            if (handleImage != null)
            {
                handleImage.sprite = UiKit.Circle;
                handleImage.type = Image.Type.Simple;
                handleImage.color = new Color(Bone.r, Bone.g, Bone.b, 0.5f);
            }
        }
    }

    // ---------------------------------------------------------------- the backpack

    // The backpack (B) in the same family as the HUD: a dark angled plate, MetalMania title in blood red,
    // dark slots with a thin dark-red edge (blood red while the item is in your hand), a plate for the tooltip.
    void StyleBackpack()
    {
        var inv = Inventory.Instance;

        var canvas = inv.panelRoot.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.GetComponent<HorrorSkinIgnore>() == null) canvas.gameObject.AddComponent<HorrorSkinIgnore>();

        // the panel: a plate instead of the big picture
        var panelRt = (RectTransform)inv.panelRoot.transform;
        panelRt.sizeDelta = new Vector2(1300f, 760f);

        var panel = inv.panelRoot.GetComponent<Image>();
        if (panel != null)
        {
            panel.sprite = UiKit.Cut;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = UiKit.Border9 / 34f;
            panel.color = new Color(0.035f, 0.035f, 0.04f, 0.93f);
        }
        AddDecor(panelRt, 34f);

        // the title
        var title = panelRt.Find("Title") as RectTransform;
        if (title != null)
        {
            title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 0.5f);
            title.sizeDelta = new Vector2(800f, 100f);
            title.anchoredPosition = new Vector2(0f, -84f);

            var text = title.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 90f;
                text.color = new Color(0.78f, 0.1f, 0.14f, 1f);
            }
        }

        var line = UiKit.Shape(panelRt, "TitleLine", UiKit.Round, Blood, 2f, new Vector2(0f, 330f - 126f), new Vector2(240f, 3f));
        line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        line.rectTransform.anchoredPosition = new Vector2(0f, -150f);

        var slotsRt = panelRt.Find("SLOTS") as RectTransform;
        if (slotsRt != null) slotsRt.anchoredPosition = new Vector2(0f, -50f);

        // the slots
        if (inv.slots != null)
        {
            foreach (var slot in inv.slots)
            {
                if (slot == null) continue;

                var rootImage = slot.GetComponent<Image>();
                if (rootImage != null)
                {
                    rootImage.sprite = UiKit.Cut;
                    rootImage.type = Image.Type.Sliced;
                    rootImage.pixelsPerUnitMultiplier = UiKit.Border9 / 18f;
                    rootImage.color = new Color(0.02f, 0.02f, 0.025f, 0.88f);
                }

                if (slot.fill != null)
                {
                    slot.fill.sprite = UiKit.Cut;
                    slot.fill.type = Image.Type.Sliced;
                    slot.fill.pixelsPerUnitMultiplier = UiKit.Border9 / 18f;
                }

                // a thin edge of our own; the old frame (if it is a separate picture) is switched off
                var oldBorder = slot.border;
                if (oldBorder != null && oldBorder.gameObject != slot.gameObject) oldBorder.enabled = false;

                var edgeTransform = slot.transform.Find("Edge");
                Image edge = edgeTransform != null ? edgeTransform.GetComponent<Image>() : null;
                if (edge == null)
                {
                    edge = UiKit.Shape(slot.transform, "Edge", UiKit.CutOutline, Edge, 18f, Vector2.zero, Vector2.zero);
                    UiKit.Stretch(edge.rectTransform);
                    edge.rectTransform.SetSiblingIndex(0);
                }

                slot.border = edge;
                slot.useStyleColors = true;
                slot.showBorder = true;
                slot.emptyColor = new Color(0f, 0f, 0f, 0f);
                slot.fullColor = new Color(0.16f, 0.1f, 0.1f, 0.4f);
                slot.borderNormal = Edge;
                slot.borderEquipped = BloodHot;     // the item in your hand

                if (slot.countText != null) slot.countText.color = Bone;
                if (slot.letter != null) slot.letter.color = Bone;
            }
        }

        // the tooltip
        if (inv.tooltipRoot != null)
        {
            var tipImage = inv.tooltipRoot.GetComponent<Image>();
            if (tipImage != null)
            {
                tipImage.sprite = UiKit.Cut;
                tipImage.type = Image.Type.Sliced;
                tipImage.pixelsPerUnitMultiplier = UiKit.Border9 / 16f;
                tipImage.color = new Color(0.035f, 0.035f, 0.04f, 0.95f);
            }
            AddDecor((RectTransform)inv.tooltipRoot.transform, 16f);

            foreach (var text in inv.tooltipRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                bool description = text.name == "Description";
                text.color = description ? new Color(Bone.r, Bone.g, Bone.b, 0.78f) : Bone;
                if (description && UiKit.DmMedium != null) text.font = UiKit.DmMedium;
            }
        }

        inv.ApplyStyle(false);
    }

    // a little scratched texture and a thin dark-red edge, drawn behind everything else on the plate
    static void AddDecor(RectTransform plate, float radius)
    {
        if (plate.Find("Decor Edge") != null) return;

        var grunge = UiKit.NewRect("Decor Grunge", plate);
        UiKit.Stretch(grunge);
        grunge.offsetMin = new Vector2(10f, 10f);
        grunge.offsetMax = new Vector2(-10f, -10f);
        var grungeImage = grunge.gameObject.AddComponent<Image>();
        grungeImage.sprite = UiKit.Grunge;
        grungeImage.color = new Color(1f, 0.9f, 0.85f, 0.06f);
        grungeImage.raycastTarget = false;
        grunge.SetSiblingIndex(0);

        var edge = UiKit.Shape(plate, "Decor Edge", UiKit.CutOutline, Edge, radius, Vector2.zero, Vector2.zero);
        UiKit.Stretch(edge.rectTransform);
        edge.rectTransform.SetSiblingIndex(1);
    }

    // BAG, DROP, TORCH and UPGRADE (phones): the same dark plate with a thin edge, bone text
    void StylePhoneButtons()
    {
        string[] names = { "BagButton", "DropButton", "TorchButton", "UpgradeOpenButton" };

        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (System.Array.IndexOf(names, button.name) < 0) continue;

            var image = button.GetComponent<Image>();
            if (image == null) continue;

            if (button.GetComponent<HorrorSkinIgnore>() == null) button.gameObject.AddComponent<HorrorSkinIgnore>();

            var rt = (RectTransform)button.transform;

            image.sprite = UiKit.Round;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = UiKit.Border9 / 16f;
            image.color = PlateColor;

            var edge = UiKit.Shape(rt, "Edge", UiKit.RoundOutline, Edge, 16f, Vector2.zero, rt.sizeDelta);
            UiKit.Stretch(edge.rectTransform);

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = Bone;
                if (UiKit.DmSemi != null) label.font = UiKit.DmSemi;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;
        }
    }
}
