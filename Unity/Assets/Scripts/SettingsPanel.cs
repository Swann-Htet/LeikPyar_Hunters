using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Settings: a gear in the top right corner (or key P / Esc). The game is paused while it is open.
//   Mouse and touch sensitivity, invert Y, field of view, brightness, volume, graphics quality,
//   fullscreen, FPS and battery, language, and a list of the controls. Everything is saved on the device (GameSettings).
// The look: frosted glass like iOS. The panel takes 80% of the screen width and height, the game behind it is blurred
// and tinted dark, there are no outlines or shadows, and the accents are blood red (see UiKit).
// The whole panel is built by this script when the game starts and scales itself to fit any screen.
// Add it with Tools > Ghost > Setup Settings.
public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    [Header("Keys")]
    public KeyCode toggleKey = KeyCode.P;
    [Tooltip("Esc opens the settings (and closes them)")]
    public bool useEscape = true;

    [Header("Gear button (top right corner, in 1920 x 1080 units)")]
    [Tooltip("The gear button in your canvas (Tools > Ghost > Setup Settings makes it). Empty = the game draws one")]
    public Button gearButton;
    [Tooltip("Your picture (Assets/ICONS/setting.png). Empty = a gear drawn by the game")]
    public Sprite gearIcon;
    public Vector2 gearPosition = new Vector2(-40f, -150f);
    public float gearSize = 90f;

    [Header("Glass")]
    [Tooltip("How much of the screen the panel takes (0.8 = 80% of the width and of the height)")]
    [Range(0.5f, 1f)] public float screenShare = 0.8f;
    [Tooltip("Blur the game behind the panel (frosted glass)")]
    public bool blurBackground = true;
    [Tooltip("The near-black colour laid over the blurred picture. Lower alpha = more see-through")]
    public Color glassColor = new Color(0.012f, 0.012f, 0.016f, 0.7f);
    [Tooltip("How bright the blurred picture is (0 = black, 1 = as it is)")]
    [Range(0f, 1f)] public float blurDim = 0.22f;
    [Tooltip("A thin dark-red edge on the panel, like the HUD plates")]
    public bool showEdge = true;

    [Header("Look")]
    [Tooltip("The font of the SETTINGS title: MetalMania-Regular SDF. Also used for all text if the Inter fonts are missing")]
    public TMP_FontAsset font;

    // the content is drawn at this size and scaled to fit inside the glass
    private static readonly Vector2 DesignSize = new Vector2(1640f, 940f);

    // dark see-through plates, like the HUD
    private static readonly Color Frost = new Color(0f, 0f, 0f, 0.30f);          // cards, tiles
    private static readonly Color FrostStrong = new Color(0f, 0f, 0f, 0.42f);    // selectors
    private static readonly Color TabOn = new Color(0.62f, 0.07f, 0.12f, 0.75f);
    private static readonly Color ButtonDark = new Color(0.07f, 0.07f, 0.08f, 0.78f);
    private static readonly Color ButtonDarkHover = new Color(0.15f, 0.15f, 0.16f, 0.92f);
    private static readonly Color ButtonDarkPressed = new Color(0.25f, 0.25f, 0.27f, 0.95f);
    private static readonly Color CloseRed = new Color(0.5f, 0.06f, 0.1f, 0.88f);
    private static readonly Color CloseRedHover = new Color(0.72f, 0.1f, 0.14f, 0.97f);
    private static readonly Color CloseRedPressed = new Color(0.4f, 0.04f, 0.07f, 1f);

    private class SliderRow
    {
        public Slider slider;
        public TMP_Text value;
        public UiGradient fill;
        public Func<float> get;
        public Func<float, string> format;
    }

    private class ToggleRow
    {
        public UiSwitch sw;
        public TMP_Text label;
        public Func<bool> get;
    }

    private class Tab
    {
        public Image fill;
        public Image icon;
        public TMP_Text label;
        public GameObject page;
        public float t;
        public float target;
    }

    private readonly List<SliderRow> sliderRows = new List<SliderRow>();
    private readonly List<ToggleRow> toggleRows = new List<ToggleRow>();
    private readonly List<Tab> tabs = new List<Tab>();

    private Canvas canvas;
    private GameObject panelRoot;
    private CanvasGroup group;
    private RectTransform animRoot;
    private RectTransform frame;
    private RawImage blurImage;
    private RenderTexture blurTexture;
    private TMP_Text qualityValue;
    private TMP_Text languageValue;
    private TMP_Text textSizeValue;
    private Image brightnessOverlay;
    private GameObject menuButton;
    private TMP_Text menuLabel;
    private bool menuArmed;
    private Camera playerCam;
    private float defaultFov = 60f;

    private bool open;
    private bool pausedByUs;
    private bool hasPlayer;
    private bool otherOpenLastFrame;
    private float previousTimeScale = 1f;
    private float nextPoll;
    private new float animation;

    private TMP_FontAsset Body => UiKit.Body != null ? UiKit.Body : font;
    private TMP_FontAsset Strong => UiKit.Strong != null ? UiKit.Strong : font;
    // The title uses the game font (MetalMania, set by Tools > Ghost > Setup Settings); Cinzel only if it is missing
    private TMP_FontAsset Heading => font != null ? font : UiKit.Heading;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BuildCanvas();
        BuildBrightness();
        BuildGear();
        BuildPanel();

        var player = FindFirstObjectByType<PlayerController>();
        hasPlayer = player != null;
        if (menuButton != null) menuButton.SetActive(hasPlayer);      // there is no game to leave in the intro menu
        if (hasPlayer && FindFirstObjectByType<SystemStatus>() == null)
            SystemStatus.Build(canvas.transform, UiKit.Numbers, 1f);
        if (player != null && player.playerCamera != null) playerCam = player.playerCamera.GetComponent<Camera>();
        if (playerCam == null) playerCam = Camera.main;
        if (playerCam != null) defaultFov = playerCam.fieldOfView;

        GameSettings.Changed += OnSettingsChanged;
        Loc.Changed += RefreshLanguage;

        ApplyAll();
        Refresh(true);
        SelectTab(0, true);
        group.alpha = 0f;
        panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        GameSettings.Changed -= OnSettingsChanged;
        Loc.Changed -= RefreshLanguage;
        if (pausedByUs) Time.timeScale = 1f;
        if (blurTexture != null) blurTexture.Release();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        bool otherOpen = Inventory.BackpackOpen || UpgradePanel.IsOpen || LetterViewer.IsOpen;

        if (Input.GetKeyDown(toggleKey) && !otherOpen)
        {
            Toggle();
        }
        else if (useEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            // Esc that just closed the backpack or the upgrades must not open the settings too
            if (open) SetOpen(false);
            else if (!otherOpen && !otherOpenLastFrame) SetOpen(true);
        }

        if (open && Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 0.4f;
            RefreshToggles(false);   // fullscreen can change by itself (F11, Esc)
        }

        // Nothing may lock the mouse again while the panel is open
        if (open && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Animate();
    }

    void LateUpdate()
    {
        otherOpenLastFrame = Inventory.BackpackOpen || UpgradePanel.IsOpen || LetterViewer.IsOpen;
    }

    public void Toggle()
    {
        SetOpen(!open);
    }

    public void SetOpen(bool value)
    {
        if (open == value || panelRoot == null) return;
        open = value;

        if (value)
        {
            // One panel at a time
            if (Inventory.Instance != null) Inventory.Instance.SetOpen(false);
            if (UpgradePanel.Instance != null) UpgradePanel.Instance.SetOpen(false);

            // The game waits (not in the intro menu: there is nothing to pause there)
            if (hasPlayer)
            {
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                pausedByUs = true;
            }

            StartCoroutine(ShowRoutine());
        }
        else
        {
            if (pausedByUs)
            {
                Time.timeScale = previousTimeScale;
                pausedByUs = false;
            }
            GameSettings.Save();
            // the panel fades out and switches itself off (see Animate)
        }

        // The mouse is free while the panel is open (it is locked again when it closes, but not on a phone)
        if (value)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!DeviceDetection.IsMobile())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // The picture of the game is taken first (without the panel), then blurred, then the panel appears over it
    IEnumerator ShowRoutine()
    {
        if (!panelRoot.activeSelf && blurBackground)
        {
            yield return new WaitForEndOfFrame();
            if (!open) yield break;
            CaptureBlur();
        }

        panelRoot.SetActive(true);
        Refresh(true);
        SelectTab(0, true);
    }

    // The screen is shrunk step by step (each step averages 2x2 pixels), which blurs it. It is only done once per opening
    void CaptureBlur()
    {
        int w = Screen.width;
        int h = Screen.height;
        if (w < 64 || h < 64 || blurImage == null) return;

        Texture2D shot = null;
        RenderTexture t4 = null, t8 = null, t16 = null;

        try
        {
            shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            shot.Apply(false);

            int fw = Mathf.Max(16, w / 8);
            int fh = Mathf.Max(9, h / 8);
            if (blurTexture == null || blurTexture.width != fw || blurTexture.height != fh)
            {
                if (blurTexture != null) blurTexture.Release();
                blurTexture = new RenderTexture(fw, fh, 0) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }

            t4 = RenderTexture.GetTemporary(Mathf.Max(8, w / 4), Mathf.Max(8, h / 4));
            t8 = RenderTexture.GetTemporary(Mathf.Max(8, w / 8), Mathf.Max(8, h / 8));
            t16 = RenderTexture.GetTemporary(Mathf.Max(8, w / 16), Mathf.Max(8, h / 16));
            t4.filterMode = t8.filterMode = t16.filterMode = FilterMode.Bilinear;

            Graphics.Blit(shot, t4);
            Graphics.Blit(t4, t8);
            Graphics.Blit(t8, t16);
            Graphics.Blit(t16, blurTexture);

            blurImage.texture = blurTexture;
            blurImage.enabled = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Settings] The blurred background could not be made: " + e.Message);
            blurImage.enabled = false;
        }
        finally
        {
            if (t4 != null) RenderTexture.ReleaseTemporary(t4);
            if (t8 != null) RenderTexture.ReleaseTemporary(t8);
            if (t16 != null) RenderTexture.ReleaseTemporary(t16);
            if (shot != null) Destroy(shot);
        }
    }

    // ===================== ANIMATION =====================

    // Opening: a short fade with a small zoom. Tabs ease between their states. Works while the game is paused.
    void Animate()
    {
        float dt = Time.unscaledDeltaTime;

        if (panelRoot != null && panelRoot.activeSelf)
        {
            animation = Mathf.MoveTowards(animation, open ? 1f : 0f, dt / 0.2f);
            float e = animation * animation * (3f - 2f * animation);

            group.alpha = e;
            group.interactable = open;
            group.blocksRaycasts = open;
            animRoot.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, e);

            if (!open && animation <= 0f) panelRoot.SetActive(false);
        }

        foreach (var tab in tabs)
        {
            tab.t = Mathf.MoveTowards(tab.t, tab.target, dt / 0.2f);
            ApplyTab(tab);
        }
    }

    static void ApplyTab(Tab tab)
    {
        float e = tab.t * tab.t * (3f - 2f * tab.t);

        Color fill = TabOn;
        fill.a = TabOn.a * e;
        tab.fill.color = fill;

        tab.label.color = Color.Lerp(UiKit.TextDim, UiKit.Text, e);
        if (tab.icon != null) tab.icon.color = Color.Lerp(UiKit.IconColor, UiKit.Crimson, e);
    }

    // ===================== APPLY =====================

    void OnSettingsChanged()
    {
        ApplyAll();
    }

    void ApplyAll()
    {
        ApplyFov();
        ApplyBrightness();
    }

    void ApplyFov()
    {
        if (playerCam == null) return;

        float fov = GameSettings.FieldOfView;
        if (fov > 0f) playerCam.fieldOfView = fov;
    }

    // Below 1: a black layer over the picture. Above 1: a light veil
    void ApplyBrightness()
    {
        if (brightnessOverlay == null) return;

        float b = GameSettings.Brightness;
        Color c;
        if (b < 1f) c = new Color(0f, 0f, 0f, Mathf.Clamp01((1f - b) * 1.2f));
        else c = new Color(1f, 1f, 1f, Mathf.Clamp01((b - 1f) * 0.35f));

        brightnessOverlay.color = c;
        brightnessOverlay.enabled = c.a > 0.001f;
    }

    // ===================== REFRESH =====================

    void Refresh(bool instant)
    {
        foreach (var row in sliderRows)
        {
            float v = row.get();
            row.slider.SetValueWithoutNotify(v);
            UpdateSliderLook(row, v);
        }

        RefreshToggles(instant);

        if (qualityValue != null)
        {
            string[] names = QualitySettings.names;
            int q = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, names.Length - 1));
            qualityValue.text = names.Length > 0 ? names[q] : "-";
        }

        RefreshLanguage();
    }

    static void UpdateSliderLook(SliderRow row, float v)
    {
        row.value.text = row.format(v);

        float span = Mathf.Max(0.0001f, row.slider.maxValue - row.slider.minValue);
        float t = Mathf.Clamp01((v - row.slider.minValue) / span);
        row.fill.Set(UiKit.Blood, Color.Lerp(UiKit.Blood, UiKit.Crimson, t));
    }

    void RefreshToggles(bool instant)
    {
        foreach (var row in toggleRows)
        {
            bool on = row.get();
            row.sw.Set(on, instant);
            row.label.text = on ? "ON" : "OFF";
        }
    }

    // The language row shows the name of the language in use, written in that language
    void RefreshLanguage()
    {
        if (textSizeValue != null) textSizeValue.text = Mathf.RoundToInt(Loc.TextSize * 100f) + "%";

        if (languageValue == null) return;

        if (Loc.IsBurmese && Loc.BurmeseFont != null && Loc.Raw("@language_name").Length > 0)
        {
            languageValue.font = Loc.BurmeseFont;
            languageValue.text = Loc.Raw("@language_name");
        }
        else
        {
            if (Strong != null) languageValue.font = Strong;
            languageValue.text = "ENGLISH";
        }
    }

    void SelectTab(int index, bool instant)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i].target = i == index ? 1f : 0f;
            if (instant) tabs[i].t = tabs[i].target;
            if (tabs[i].page != null) tabs[i].page.SetActive(i == index);
            ApplyTab(tabs[i]);
        }
    }

    // ===================== BUILDING THE UI =====================

    void BuildCanvas()
    {
        var go = new GameObject("SettingsCanvas");
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<HorrorSkinIgnore>();   // this menu has its own look
    }

    // The brightness layer is under every other canvas, so the HUD keeps its own look
    void BuildBrightness()
    {
        var go = new GameObject("BrightnessCanvas");
        go.transform.SetParent(transform, false);

        var bc = go.AddComponent<Canvas>();
        bc.renderMode = RenderMode.ScreenSpaceOverlay;
        bc.sortingOrder = -10;

        var layer = UiKit.NewRect("Overlay", go.transform);
        UiKit.Stretch(layer);
        brightnessOverlay = layer.gameObject.AddComponent<Image>();
        brightnessOverlay.raycastTarget = false;
        brightnessOverlay.color = new Color(0f, 0f, 0f, 0f);
        brightnessOverlay.enabled = false;
    }

    void BuildGear()
    {
        if (gearButton == null)
            gearButton = BuildGearButton(canvas.transform, gearIcon, gearPosition, gearSize);

        gearButton.onClick.AddListener(Toggle);
    }

    // The gear button (used by the setup tool too, so it shows in your canvas)
    public static Button BuildGearButton(Transform parent, Sprite icon, Vector2 position, float size)
    {
        var go = new GameObject("SettingsButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = position;

        var image = go.AddComponent<Image>();
        image.sprite = icon != null ? icon : MakeGearSprite();
        image.preserveAspect = true;
        image.color = Color.white;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = new Color(0.8f, 0.8f, 0.82f, 0.9f);
        colors.highlightedColor = UiKit.Crimson;
        colors.pressedColor = UiKit.Blood;
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        return button;
    }

    void BuildPanel()
    {
        panelRoot = UiKit.NewRect("Panel", canvas.transform).gameObject;
        UiKit.Stretch((RectTransform)panelRoot.transform);
        group = panelRoot.AddComponent<CanvasGroup>();

        // A light veil over the game that also stops clicks reaching it
        var backdrop = UiKit.NewRect("Backdrop", panelRoot.transform);
        UiKit.Stretch(backdrop);
        var dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.32f);
        dim.raycastTarget = true;

        // The glass takes 'screenShare' of the screen's width and height (80%), in the middle
        animRoot = UiKit.NewRect("Glass Area", panelRoot.transform);
        float margin = (1f - screenShare) * 0.5f;
        animRoot.anchorMin = new Vector2(margin, margin);
        animRoot.anchorMax = new Vector2(1f - margin, 1f - margin);
        animRoot.offsetMin = animRoot.offsetMax = Vector2.zero;

        BuildGlass(animRoot);

        // The content is drawn at a fixed size and scaled to fit inside the glass
        frame = UiKit.NewRect("Content", animRoot);
        UiKit.Place(frame, Vector2.zero, DesignSize);
        var fit = frame.gameObject.AddComponent<UiFitScale>();
        fit.designSize = DesignSize;
        fit.screenShare = 0.96f;

        // header
        UiKit.IconImage(frame, "TitleIcon", "settings", UiKit.IconColor, new Vector2(-768f, 392f), 38f);

        var titleShadow = UiKit.Label(frame, "Title Glow", "SETTINGS", Heading, 58f, UiKit.Blood, TextAlignmentOptions.MidlineLeft, new Vector2(-457f, 389f), new Vector2(520f, 80f));
        titleShadow.characterSpacing = 6f;
        titleShadow.color = new Color(UiKit.Blood.r, UiKit.Blood.g, UiKit.Blood.b, 0.7f);
        var title = UiKit.Label(frame, "Title", "SETTINGS", Heading, 58f, UiKit.Text, TextAlignmentOptions.MidlineLeft, new Vector2(-460f, 392f), new Vector2(520f, 80f));
        title.characterSpacing = 6f;

        UiKit.Shape(frame, "TitleLine", UiKit.Round, UiKit.Blood, 2f, new Vector2(-680f, 346f), new Vector2(80f, 3f));

        // tabs and their pages
        var gamePage = UiKit.NewRect("GamePage", frame);
        UiKit.Stretch(gamePage);
        var controlsPage = UiKit.NewRect("ControlsPage", frame);
        UiKit.Stretch(controlsPage);

        MakeTab(0, "GAME", "gamepad-2", -400f, gamePage.gameObject);
        MakeTab(1, "CONTROLS", "sliders-horizontal", 400f, controlsPage.gameObject);

        BuildGamePage(gamePage);
        BuildControlsPage(controlsPage);

        // bottom buttons
        ActionButton("Reset", "rotate-ccw", "RESET", new Vector2(-520f, -385f), false, ResetAll);
        menuLabel = ActionButton("MainMenu", "power", "MAIN MENU", new Vector2(0f, -385f), false, LeaveToMenu);
        menuButton = menuLabel.transform.parent.parent.gameObject;
        ActionButton("Close", "x", "CLOSE", new Vector2(520f, -385f), true, () => SetOpen(false));
    }

    // MAIN MENU: a web game can not use Esc, so the settings have the way back. The first tap asks, the second (within 3 seconds) leaves.
    void LeaveToMenu()
    {
        if (!menuArmed)
        {
            menuArmed = true;
            menuLabel.text = "TAP AGAIN TO LEAVE";
            StartCoroutine(DisarmMenu());
            return;
        }

        menuArmed = false;
        GameSettings.Save();
        pausedByUs = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        TutorialMode.Requested = false;
        SceneManager.LoadScene(TutorialMode.MenuSceneName);
    }

    IEnumerator DisarmMenu()
    {
        yield return new WaitForSecondsRealtime(3f);
        menuArmed = false;
        if (menuLabel != null) menuLabel.text = "MAIN MENU";
    }

    // Frosted glass: the blurred game, a dark tint, a soft sheen on the top. No outline, no shadow
    void BuildGlass(RectTransform parent)
    {
        var glass = UiKit.Shape(parent, "Glass", UiKit.Cut, Color.white, 40f, Vector2.zero, Vector2.zero);
        UiKit.Stretch(glass.rectTransform);
        glass.raycastTarget = true;

        // everything inside is cut to the rounded shape
        var mask = glass.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var blur = UiKit.NewRect("Blur", glass.transform);
        UiKit.Stretch(blur);
        blurImage = blur.gameObject.AddComponent<RawImage>();
        blurImage.uvRect = new Rect((1f - screenShare) * 0.5f, (1f - screenShare) * 0.5f, screenShare, screenShare);   // the part of the screen behind the glass
        blurImage.color = new Color(blurDim, blurDim, blurDim, 1f);
        blurImage.raycastTarget = false;
        blurImage.enabled = false;

        var tint = UiKit.NewRect("Tint", glass.transform);
        UiKit.Stretch(tint);
        var tintImage = tint.gameObject.AddComponent<Image>();
        tintImage.color = glassColor;
        tintImage.raycastTarget = false;

        if (showEdge)
        {
            var edge = UiKit.Shape(glass.transform, "Edge", UiKit.CutOutline, new Color(0.35f, 0.063f, 0.11f, 0.95f), 40f, Vector2.zero, Vector2.zero);
            UiKit.Stretch(edge.rectTransform);
        }

        var sheen = UiKit.NewRect("Sheen", glass.transform);
        UiKit.Stretch(sheen);
        var sheenImage = sheen.gameObject.AddComponent<Image>();
        sheenImage.sprite = UiKit.VerticalFade;
        sheenImage.color = new Color(1f, 1f, 1f, 0.045f);
        sheenImage.raycastTarget = false;
    }

    // ---------------- tabs ----------------

    void MakeTab(int index, string text, string iconName, float x, GameObject page)
    {
        var size = new Vector2(740f, 70f);
        var rt = UiKit.NewRect("Tab " + text, frame);
        UiKit.Place(rt, new Vector2(x, 290f), size);

        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectTab(index, false));

        // frosted pill; the open tab is tinted blood red
        UiKit.Shape(rt, "Base", UiKit.Cut, Frost, 28f, Vector2.zero, size);
        var fill = UiKit.Shape(rt, "Fill", UiKit.Cut, TabOn, 28f, Vector2.zero, size);

        IconLabel(rt, iconName, text, Strong, 28f, UiKit.TextDim, 24f, Vector2.zero, size, out Image icon, out TMP_Text label);
        label.characterSpacing = 5f;

        var hover = rt.gameObject.AddComponent<UiHover>();
        hover.hoverScale = 1.01f;
        hover.pressedScale = 0.985f;
        hover.glowHover = 0f;

        tabs.Add(new Tab { fill = fill, icon = icon, label = label, page = page });
    }

    // An icon with a text next to it, centred (the layout group keeps them centred in any language)
    void IconLabel(Transform parent, string iconName, string text, TMP_FontAsset textFont, float size, Color color, float iconSize,
        Vector2 position, Vector2 box, out Image icon, out TMP_Text label)
    {
        var content = UiKit.NewRect("Content", parent);
        UiKit.Place(content, position, box);

        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var iconRt = UiKit.NewRect("Icon", content);
        icon = iconRt.gameObject.AddComponent<Image>();
        icon.sprite = UiKit.Icon(iconName);
        icon.preserveAspect = true;
        icon.color = color;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;
        var iconLayout = iconRt.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = iconSize;
        iconLayout.preferredHeight = iconSize;

        var labelRt = UiKit.NewRect("Label", content);
        label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (textFont != null) label.font = textFont;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Midline;
    }

    // ---------------- buttons ----------------

    TMP_Text ActionButton(string name, string iconName, string text, Vector2 position, bool primary, Action onClick)
    {
        var size = new Vector2(440f, 70f);
        var rt = UiKit.NewRect(name, frame);
        UiKit.Place(rt, position, size);

        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());

        // glass buttons, no border: RESET is frosted, CLOSE is tinted blood red
        Color normal = primary ? CloseRed : ButtonDark;
        Color hover = primary ? CloseRedHover : ButtonDarkHover;
        Color pressed = primary ? CloseRedPressed : ButtonDarkPressed;

        var fill = UiKit.Shape(rt, "Fill", UiKit.Cut, normal, 28f, Vector2.zero, size);

        var effect = rt.gameObject.AddComponent<UiHover>();
        effect.target = fill;
        effect.normal = normal;
        effect.hover = hover;
        effect.pressed = pressed;

        IconLabel(rt, iconName, text, Strong, 28f, UiKit.Text, 28f, Vector2.zero, size, out Image icon, out TMP_Text label);
        label.characterSpacing = 5f;
        if (primary && icon != null) icon.color = Color.white;
        return label;
    }

    // A small arrow button (previous / next)
    void ChevronButton(Transform parent, string iconName, Vector2 position, Action onClick)
    {
        var rt = UiKit.NewRect("Chevron " + iconName, parent);
        UiKit.Place(rt, position, new Vector2(56f, 44f));

        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());

        var icon = UiKit.IconImage(rt, "Icon", iconName, UiKit.IconColor, Vector2.zero, 24f);

        var hover = rt.gameObject.AddComponent<UiHover>();
        hover.target = icon;
        hover.normal = UiKit.IconColor;
        hover.hover = UiKit.Crimson;
        hover.pressed = UiKit.Blood;
        hover.hoverScale = 1.12f;
    }

    // ---------------- the Game page ----------------

    void BuildGamePage(Transform page)
    {
        var left = Card(page, "Left Card", -400f);
        var right = Card(page, "Right Card", 400f);

        // left column
        AddSlider(left, 0, "mouse", "Mouse Sensitivity", GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
            () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, Percent);

        AddSlider(left, 1, "hand", "Touch Sensitivity", GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
            () => GameSettings.TouchSensitivity, v => GameSettings.TouchSensitivity = v, Percent);

        AddToggle(left, 2, "refresh-cw", "Invert Look (Y)", () => GameSettings.InvertY, () => GameSettings.InvertY = !GameSettings.InvertY);

        AddSlider(left, 3, "eye", "Field of View", GameSettings.MinFov, GameSettings.MaxFov,
            () => GameSettings.FieldOfView > 0f ? GameSettings.FieldOfView : Mathf.Clamp(defaultFov, GameSettings.MinFov, GameSettings.MaxFov),
            v => GameSettings.FieldOfView = v, v => Mathf.RoundToInt(v).ToString());

        AddSlider(left, 4, "sun", "Brightness", GameSettings.MinBrightness, GameSettings.MaxBrightness,
            () => GameSettings.Brightness, v => GameSettings.Brightness = v, Percent);

        AddSlider(left, 5, "volume-2", "Volume", 0f, 1f, () => GameSettings.Volume, v => GameSettings.Volume = v, Percent);

        // right column
        AddQuality(right, 0);

        AddToggle(right, 1, "maximize", "Fullscreen", () => Screen.fullScreen, () => Screen.fullScreen = !Screen.fullScreen);

        AddToggle(right, 2, "activity", "Show FPS", () => GameSettings.ShowFps, () => GameSettings.ShowFps = !GameSettings.ShowFps);

        AddToggle(right, 3, "battery-charging", "Show Battery", () => GameSettings.ShowBattery, () => GameSettings.ShowBattery = !GameSettings.ShowBattery);

        AddLanguage(right, 4);

        AddTextSize(right, 5);
    }

    // A plain frosted card (780 x 530): no border, no shadow
    Transform Card(Transform parent, string name, float x)
    {
        var card = UiKit.Shape(parent, name, UiKit.Cut, Frost, 22f, new Vector2(x, -30f), new Vector2(780f, 530f));
        return card.transform;
    }

    // The centre of row i inside a card
    static float RowY(int i)
    {
        return 209f - 84f * i;
    }

    void RowFrame(Transform card, int index, string iconName, string label, float labelOffset)
    {
        float y = RowY(index);

        var tile = UiKit.Shape(card, "Icon Tile", UiKit.Cut, new Color(0f, 0f, 0f, 0.4f), 12f, new Vector2(-335f, y), new Vector2(56f, 56f));
        UiKit.IconImage(tile.transform, "Icon", iconName, UiKit.IconColor, Vector2.zero, 26f);

        UiKit.Label(card, label + " Label", label, Body, 27f, UiKit.Text, TextAlignmentOptions.MidlineLeft, new Vector2(10f, y + labelOffset), new Vector2(600f, 40f));
    }

    void AddSlider(Transform card, int index, string iconName, string label, float min, float max, Func<float> get, Action<float> set,
        Func<float, string> format)
    {
        float y = RowY(index);
        RowFrame(card, index, iconName, label, 22f);

        var slider = MakeSlider(card, new Vector2(-50f, y - 14f), 480f, min, max, out UiGradient gradient);

        var value = UiKit.Label(card, label + " Value", "", Strong, 26f, UiKit.Text, TextAlignmentOptions.MidlineRight, new Vector2(285f, y - 2f), new Vector2(130f, 44f));

        var row = new SliderRow { slider = slider, value = value, fill = gradient, get = get, format = format };
        sliderRows.Add(row);

        slider.onValueChanged.AddListener(v =>
        {
            set(v);
            UpdateSliderLook(row, v);
        });
    }

    void AddToggle(Transform card, int index, string iconName, string label, Func<bool> get, Action flip)
    {
        float y = RowY(index);
        RowFrame(card, index, iconName, label, 0f);

        UiSwitch sw = MakeSwitch(card, new Vector2(290f, y), () =>
        {
            flip();
            RefreshToggles(false);
        }, out TMP_Text text);

        toggleRows.Add(new ToggleRow { sw = sw, label = text, get = get });
    }

    void AddQuality(Transform card, int index)
    {
        float y = RowY(index);
        RowFrame(card, index, "monitor", "Graphics Quality", 22f);
        qualityValue = MakeSelector(card, y, () => ChangeQuality(-1), () => ChangeQuality(1));
    }

    void AddLanguage(Transform card, int index)
    {
        float y = RowY(index);
        RowFrame(card, index, "globe", "Language", 22f);
        languageValue = MakeSelector(card, y, ToggleLanguage, ToggleLanguage);
        languageValue.gameObject.AddComponent<LocalizeIgnore>();   // it shows the language's own name
    }

    // The size of the Burmese text: smaller / bigger, shown in percent
    void AddTextSize(Transform card, int index)
    {
        float y = RowY(index);
        RowFrame(card, index, "keyboard", "Text Size", 22f);
        textSizeValue = MakeSelector(card, y, () => ChangeTextSize(-1), () => ChangeTextSize(1));
        textSizeValue.gameObject.AddComponent<LocalizeIgnore>();
        RefreshLanguage();
    }

    void ChangeTextSize(int step)
    {
        Loc.StepTextSize(step);
        RefreshLanguage();
    }

    // "<  value  >" in a frosted pill. Returns the value text
    TMP_Text MakeSelector(Transform card, float y, Action previous, Action next)
    {
        var size = new Vector2(640f, 48f);
        var body = UiKit.Shape(card, "Selector", UiKit.Round, FrostStrong, 24f, new Vector2(30f, y - 18f), size);

        ChevronButton(body.transform, "chevron-left", new Vector2(-size.x * 0.5f + 40f, 0f), previous);
        ChevronButton(body.transform, "chevron-right", new Vector2(size.x * 0.5f - 40f, 0f), next);

        return UiKit.Label(body.transform, "Value", "", Strong, 26f, UiKit.Text, TextAlignmentOptions.Center, Vector2.zero, new Vector2(440f, 48f));
    }

    // ---------------- the Controls page ----------------

    void BuildControlsPage(Transform page)
    {
        var left = Card(page, "Left Card", -400f);
        var right = Card(page, "Right Card", 400f);

        string[] first =
        {
            "WASD  -  Move",
            "Shift  -  Run",
            "Ctrl  -  Walk slowly",
            "Space  -  Jump",
            "Mouse  -  Look around",
            "Left Click  -  Pull ghost with the gun",
            "1 2 3  -  Gun / Scanner / Injector",
        };
        string[] second =
        {
            "E  -  Take / Interact",
            "F  -  Open door / Put / Fill",
            "B  -  Backpack",
            "U  -  Upgrades",
            "T  -  Torch",
            "O  -  Scanner on / off",
            "P  -  Settings",
        };

        for (int i = 0; i < first.Length; i++) ControlLine(left, i, first[i]);
        for (int i = 0; i < second.Length; i++) ControlLine(right, i, second[i]);
    }

    void ControlLine(Transform card, int i, string text)
    {
        float y = 207f - 72f * i;

        var dot = UiKit.NewRect("Dot", card);
        UiKit.Place(dot, new Vector2(-340f, y), new Vector2(9f, 9f));
        var dotImage = dot.gameObject.AddComponent<Image>();
        dotImage.sprite = UiKit.Circle;
        dotImage.color = UiKit.Blood;
        dotImage.raycastTarget = false;

        UiKit.Label(card, "Control " + i, text, Body, 28f, UiKit.Text, TextAlignmentOptions.MidlineLeft, new Vector2(10f, y), new Vector2(660f, 56f));
    }

    // ---------------- controls ----------------

    static string Percent(float v)
    {
        return Mathf.RoundToInt(v * 100f) + "%";
    }

    UiSwitch MakeSwitch(Transform parent, Vector2 position, Action onClick, out TMP_Text text)
    {
        var size = new Vector2(170f, 50f);
        var rt = UiKit.NewRect("Switch", parent);
        UiKit.Place(rt, position, size);

        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());

        // frosted grey when OFF, blood red when ON
        var track = UiKit.Shape(rt, "Track", UiKit.Round, UiKit.Track, 25f, Vector2.zero, size);

        text = UiKit.Label(rt, "Label", "OFF", Strong, 22f, UiKit.TextDim, TextAlignmentOptions.Center, new Vector2(28f, 0f), new Vector2(80f, 50f));

        var thumbRt = UiKit.NewRect("Thumb", rt);
        UiKit.Place(thumbRt, new Vector2(-60f, 0f), new Vector2(38f, 38f));
        var thumb = thumbRt.gameObject.AddComponent<Image>();
        thumb.sprite = UiKit.Circle;
        thumb.color = new Color(0.95f, 0.95f, 0.97f, 1f);
        thumb.raycastTarget = false;

        var sw = rt.gameObject.AddComponent<UiSwitch>();
        sw.track = track;
        sw.glow = null;
        sw.thumb = thumbRt;
        sw.label = text;
        sw.onColor = UiKit.Blood;
        sw.offColor = new Color(1f, 1f, 1f, 0.16f);
        sw.onLabelColor = UiKit.Text;
        sw.offLabelColor = new Color(0.72f, 0.75f, 0.8f, 1f);
        sw.travel = 60f;
        sw.labelTravel = 26f;

        var hover = rt.gameObject.AddComponent<UiHover>();
        hover.hoverScale = 1.03f;
        hover.pressedScale = 0.97f;
        return sw;
    }

    Slider MakeSlider(Transform parent, Vector2 position, float width, float min, float max, out UiGradient gradient)
    {
        var rt = UiKit.NewRect("Slider", parent);
        UiKit.Place(rt, position, new Vector2(width, 44f));

        // a tall invisible area, so the slider is easy to grab
        var hitRt = UiKit.NewRect("Hit Area", rt);
        UiKit.Stretch(hitRt);
        var hit = hitRt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;

        // the track: frosted grey
        var track = UiKit.NewRect("Track", rt);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 6f);
        track.anchoredPosition = Vector2.zero;
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = UiKit.Round;
        trackImage.type = Image.Type.Sliced;
        trackImage.pixelsPerUnitMultiplier = UiKit.Border9 / 3f;
        trackImage.color = new Color(1f, 1f, 1f, 0.18f);
        trackImage.raycastTarget = false;

        // the filled part: a thin blood-red line (a little brighter as the value grows)
        var fillArea = UiKit.NewRect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-22f, 6f);
        fillArea.anchoredPosition = Vector2.zero;

        var fill = UiKit.NewRect("Fill", fillArea);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.sizeDelta = new Vector2(11f, 0f);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = UiKit.Round;
        fillImage.type = Image.Type.Sliced;
        fillImage.pixelsPerUnitMultiplier = UiKit.Border9 / 3f;
        fillImage.color = Color.white;
        fillImage.raycastTarget = false;
        gradient = fill.gameObject.AddComponent<UiGradient>();
        gradient.Set(UiKit.Blood, UiKit.Blood);

        // the thumb: a small white disc
        var slideArea = UiKit.NewRect("Handle Slide Area", rt);
        slideArea.anchorMin = new Vector2(0f, 0.5f);
        slideArea.anchorMax = new Vector2(1f, 0.5f);
        slideArea.sizeDelta = new Vector2(-20f, 20f);   // 20 high: the thumb fills it
        slideArea.anchoredPosition = Vector2.zero;

        var handle = UiKit.NewRect("Handle", slideArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(20f, 0f);        // 20 wide x 20 high = a circle
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = UiKit.Circle;
        handleImage.color = new Color(0.95f, 0.95f, 0.97f, 1f);

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        return slider;
    }

    // ---------------- actions ----------------

    void ChangeQuality(int step)
    {
        int count = QualitySettings.names.Length;
        if (count <= 1) return;

        int q = (QualitySettings.GetQualityLevel() + step + count) % count;
        GameSettings.Quality = q;
        Refresh(false);
    }

    void ToggleLanguage()
    {
        Loc.Set(Loc.IsBurmese ? Loc.Language.English : Loc.Language.Burmese);
        Refresh(false);
    }

    void ResetAll()
    {
        GameSettings.ResetAll();

        // back to the camera's own field of view
        if (playerCam != null) playerCam.fieldOfView = defaultFov;

        ApplyAll();
        Refresh(false);
    }

    // A white gear drawn by code (used when no picture is given)
    static Sprite MakeGearSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                float teeth = Mathf.SmoothStep(-0.25f, 0.25f, Mathf.Cos(angle * 8f));
                float outer = 0.62f + 0.22f * teeth;
                float hole = 0.28f;

                float edge = 1f / size * 2f;
                float a = Mathf.Clamp01((outer - r) / edge) * Mathf.Clamp01((r - hole) / edge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
