using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The Upgrade Panel (key U). The player sees his points ("Total Point 3") and browses the upgradable items
// one by one with the arrows (Gun, EM, MEP, Radio...). Clicking + (or Enter) spends 1 point on the item shown:
// it goes up by a random 1-3% (set per item in UpgradeSystem).
// Build it with Tools > Ghost > Setup Upgrades And Drop Table, then restyle it in the scene as you like.
public class UpgradePanel : MonoBehaviour
{
    public static UpgradePanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    [Header("Keys")]
    public KeyCode toggleKey = KeyCode.U;
    public KeyCode previousKey = KeyCode.LeftArrow;
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode upgradeKey = KeyCode.Return;
    public bool closeOnEscape = true;
    [Tooltip("On phones an UPGRADE button opens the panel")]
    public bool showMobileButton = true;

    [Header("Look")]
    [Tooltip("e.g. MetalMania-Regular SDF. Empty = the TMP default font")]
    public TMP_FontAsset font;
    public Color panelColor = new Color(0.05f, 0.05f, 0.07f, 0.96f);
    public Color accentColor = new Color(0.9f, 0.14f, 0.23f, 1f);

    [Header("Texts")]
    public string pointsFormat = "Total Point {0}";
    public string noPointText = "No points. Check a Faraday Cage on the Drop Table to earn one";
    public string hintText = "<   >  browse        Enter or +  upgrade        U  close";

    [Header("UI (built automatically if empty)")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text pointsText;
    public TMP_Text percentText;
    public TMP_Text levelText;
    public TMP_Text resultText;
    public TMP_Text letterText;
    public Image iconImage;
    public RectTransform barFill;
    public Button previousButton;
    public Button nextButton;
    public Button upgradeButton;

    // The shop: after the upgrades there is one more page, the Inhaler (the injector that heals you). It costs points.
    public const int InhalerCost = 5;
    const string InhalerName = "Bio-cellular Injector";

    private readonly List<UpgradeSystem.Upgrade> items = new List<UpgradeSystem.Upgrade>();
    bool OnShopPage { get { return index == items.Count; } }
    private int index;
    private bool open;
    private float resultUntil;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (themedUI) DiscardOldPanel();
        if (panelRoot == null) BuildUI();

        if (previousButton != null) previousButton.onClick.AddListener(() => Navigate(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Navigate(1));
        if (upgradeButton != null) upgradeButton.onClick.AddListener(UpgradeSelected);

        ApplyFont();
        BuildMobileButton();

        UpgradeSystem.Changed += Refresh;

        open = false;
        panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        UpgradeSystem.Changed -= Refresh;
        if (Instance == this) Instance = null;
    }

    [Header("Look")]
    [Tooltip("On: the screen is built in the game's own style (dark plate, red edge, bone text) and replaces the one saved in the scene")]
    public bool themedUI = true;

    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.78f, 0.10f, 0.14f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    [Header("Only at the NPC")]
    [Tooltip("On: the upgrade screen opens only when the player stands near the NPC (the NPC is in the house from 7 AM to 4 PM)")]
    public bool onlyAtNpc = true;
    [Tooltip("How close to the NPC the player must be (meters)")]
    public float npcReach = 5.5f;

    // The NPC is in the house and the player is close to her
    public static bool NearNpc(Transform player, float reach)
    {
        var npc = NpcWorker.Instance;
        if (npc == null || !npc.IsPresent || player == null) return false;
        return Vector3.Distance(player.position, npc.transform.position) <= reach;
    }

    // The player is close to the NPC and looks towards her
    bool LookingAtNpc()
    {
        var npc = NpcWorker.Instance;
        var cam = Camera.main;
        if (npc == null || !npc.IsPresent || cam == null) return false;

        Vector3 to = npc.transform.position + Vector3.up - cam.transform.position;
        return to.magnitude <= Mathf.Min(npcReach, 4.5f) && Vector3.Angle(cam.transform.forward, to) <= 40f;
    }

    public bool CanOpenHere()
    {
        if (!onlyAtNpc) return true;

        var player = FindFirstObjectByType<PlayerController>();
        return NearNpc(player != null ? player.transform : null, npcReach);
    }

    private float noticeUntil;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !SettingsPanel.IsOpen)
        {
            if (!open && !CanOpenHere()) noticeUntil = Time.time + 2.2f;
            else Toggle();
        }
        else if (open && closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            SetOpen(false);

        if (!open && Time.time < noticeUntil)
            InteractPrompt.Request("Upgrade at the NPC (7 AM - 4 PM)");
        else if (!open && onlyAtNpc && LookingAtNpc() && !SettingsPanel.IsOpen)
        {
            InteractPrompt.Request("[U] Upgrade");
            if (MobileInput.GetInteract()) Toggle();     // on a phone the interact button opens it
        }

        if (!open) return;

        if (Input.GetKeyDown(previousKey)) Navigate(-1);
        if (Input.GetKeyDown(nextKey)) Navigate(1);
        if (Input.GetKeyDown(upgradeKey) || Input.GetKeyDown(KeyCode.KeypadEnter)) UpgradeSelected();

        if (resultText != null && resultUntil > 0f && Time.time >= resultUntil)
        {
            resultUntil = 0f;
            resultText.text = "";
        }
    }

    public void Toggle()
    {
        SetOpen(!open);
    }

    public void SetOpen(bool value)
    {
        if (open == value || panelRoot == null) return;
        if (value && !CanOpenHere()) return;

        open = value;

        // One panel at a time
        if (value && Inventory.Instance != null) Inventory.Instance.SetOpen(false);

        panelRoot.SetActive(value);
        if (value) Refresh();

        // The mouse is free while the panel is open (phones have no mouse)
        if (!DeviceDetection.IsMobile())
        {
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }
    }

    // ===================== BROWSING =====================

    public void Navigate(int step)
    {
        int pages = items.Count + 1;      // the upgrades and the Inhaler

        index = (index + step + pages) % pages;
        if (resultText != null) { resultText.text = ""; resultUntil = 0f; }
        Refresh();
    }

    // Spends 1 point on the item that is shown
    public void UpgradeSelected()
    {
        var system = UpgradeSystem.Instance;
        if (system == null) return;

        if (OnShopPage)
        {
            BuyInhaler(system);
            return;
        }
        if (items.Count == 0) return;

        string result = system.Spend(items[index].id);
        ShowResult(result ?? noPointText);
        Refresh();
    }

    // 5 points = 1 Inhaler (the hotbar's injector, key 3)
    void BuyInhaler(UpgradeSystem system)
    {
        var hotbar = Hotbar.Instance;
        if (hotbar == null) { ShowResult("No hotbar"); return; }

        if (hotbar.injectors >= hotbar.maxInjectors)
        {
            ShowResult("You can not carry more Inhalers");
        }
        else if (!system.TrySpendPoints(InhalerCost))
        {
            ShowResult("Not enough points: an Inhaler costs " + InhalerCost);
        }
        else
        {
            hotbar.AddInjector(1);
            ShowResult("Bought 1 Inhaler");
        }
        Refresh();
    }

    void ShowResult(string text)
    {
        if (resultText == null) return;

        resultText.text = text;
        resultUntil = Time.time + 4f;
    }

    // Everything on the screen is redrawn from the UpgradeSystem
    void Refresh()
    {
        var system = UpgradeSystem.Instance;
        if (system == null || panelRoot == null) return;

        // Only the items the player may upgrade
        items.Clear();
        foreach (var u in system.upgrades)
            if (u != null && u.gainsFromPoints) items.Add(u);

        if (pointsText != null) pointsText.text = string.Format(pointsFormat, system.Points);

        index = Mathf.Clamp(index, 0, items.Count);      // the last page is the shop
        SetButtonLabel(OnShopPage ? "BUY" : "UPGRADE");

        if (OnShopPage)
        {
            ShowShop(system);
            return;
        }

        var item = items[index];

        if (titleText != null) titleText.text = item.displayName;

        if (iconImage != null)
        {
            iconImage.enabled = item.icon != null;
            iconImage.sprite = item.icon;
        }
        if (letterText != null)
        {
            letterText.gameObject.SetActive(item.icon == null);
            letterText.text = string.IsNullOrEmpty(item.displayName) ? "" : item.displayName.Substring(0, 1).ToUpperInvariant();
        }

        // the MEP is a number a night (3 to 6): it costs 10, 20, 30 points to go up
        bool mep = item.id == UpgradeSystem.MepId;
        int mepCount = system.MepCount;
        int mepCost = system.MepNextCost;
        bool mepFull = mepCount >= system.mepMax;

        float fraction = item.maxPercent > 0f ? Mathf.Clamp01(item.percent / item.maxPercent) : 0f;
        if (mep) fraction = Mathf.Clamp01((mepCount - system.mepStart) / (float)Mathf.Max(1, system.mepMax - system.mepStart));
        if (barFill != null) barFill.anchorMax = new Vector2(fraction, 1f);
        if (percentText != null)
            percentText.text = mep ? mepCount + " a night" : item.percent.ToString("0.#") + " / " + item.maxPercent.ToString("0") + " %";

        if (levelText != null)
        {
            int levels = item.levelThresholds != null ? item.levelThresholds.Length : 0;
            if (mep) levelText.text = mepFull ? "MOST" : "NEXT: " + (mepCount + 1) + " A NIGHT = " + mepCost + " POINTS";
            else levelText.text = levels > 0 ? "LEVEL " + item.level + " / " + levels : "";
        }

        if (upgradeButton != null)
            upgradeButton.interactable = mep ? (!mepFull && system.Points >= mepCost) : system.Points > 0 && item.percent < item.maxPercent;

        // the shop is one more page: the arrows always work
        if (previousButton != null) previousButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
    }

    void SetButtonLabel(string text)
    {
        if (upgradeButton == null) return;
        var label = upgradeButton.GetComponentInChildren<TMP_Text>();
        if (label != null && label.text != text) label.text = text;
    }

    // The page of the Inhaler: its picture, how many you have, the price
    void ShowShop(UpgradeSystem system)
    {
        var hotbar = Hotbar.Instance;
        int have = hotbar != null ? hotbar.injectors : 0;
        int most = hotbar != null ? hotbar.maxInjectors : 0;

        if (titleText != null) titleText.text = InhalerName;

        var picture = Resources.Load<Sprite>("UpgradeIcons/Injector");
        if (iconImage != null)
        {
            iconImage.enabled = picture != null;
            iconImage.sprite = picture;
        }
        if (letterText != null)
        {
            letterText.gameObject.SetActive(picture == null);
            letterText.text = "I";
        }

        if (barFill != null) barFill.anchorMax = new Vector2(most > 0 ? Mathf.Clamp01(have / (float)most) : 0f, 1f);
        if (percentText != null) percentText.text = "You have " + have + " / " + most;
        if (levelText != null) levelText.text = "PRICE " + InhalerCost + " POINTS";

        if (upgradeButton != null)
            upgradeButton.interactable = system.Points >= InhalerCost && have < most;

        if (previousButton != null) previousButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
    }

    // ===================== UI BUILDING =====================

    // The screen saved in the scene (the old look) is thrown away: the themed one is built instead
    void DiscardOldPanel()
    {
        if (panelRoot == null) return;

        var canvas = panelRoot.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.gameObject.name == "UpgradeCanvas") Destroy(canvas.gameObject);
        else Destroy(panelRoot);
        panelRoot = null;
    }

    void ApplyFont()
    {
        if (themedUI || font == null || panelRoot == null) return;

        foreach (var text in panelRoot.GetComponentsInChildren<TMP_Text>(true))
            text.font = font;
    }

    // Builds the screen (a canvas named "UpgradeCanvas"): a dark plate with a red edge, the picture of the item on the left, its name, level
    // and bar on the right, the UPGRADE button under them. Called automatically, or by the setup tool
    public void BuildUI()
    {
        var canvasGO = new GameObject("UpgradeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.AddComponent<HorrorSkinIgnore>();

        // a dark veil over the game (it also stops clicks reaching it)
        panelRoot = NewChild(canvasGO.transform, "Panel", typeof(Image));
        var veilRt = (RectTransform)panelRoot.transform;
        veilRt.anchorMin = Vector2.zero;
        veilRt.anchorMax = Vector2.one;
        veilRt.offsetMin = veilRt.offsetMax = Vector2.zero;
        var veil = panelRoot.GetComponent<Image>();
        veil.color = new Color(0f, 0f, 0f, 0.62f);
        veil.raycastTarget = true;

        var heading = UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi;

        // the plate
        var plate = HudStyler.MakePlate(panelRoot.transform, "Upgrades", 24f, new Vector2(1000f, 600f));
        var root = plate.transform;

        var title = UiKit.Label(root, "Heading", "UPGRADES", heading, 40f, Blood, TextAlignmentOptions.MidlineLeft, new Vector2(-300f, 244f), new Vector2(460f, 60f));
        title.characterSpacing = 6f;

        pointsText = UiKit.Label(root, "Points", "Total Point 0", UiKit.DmSemi, 30f, Bone, TextAlignmentOptions.MidlineRight, new Vector2(215f, 244f), new Vector2(420f, 50f));

        UiKit.Shape(root, "Line", UiKit.Round, Edge, 2f, new Vector2(0f, 196f), new Vector2(940f, 2f));

        var close = ThemeButton(root, "Close", "", new Vector2(448f, 246f), new Vector2(52f, 52f));
        UiKit.IconImage(close.transform, "Icon", "x", Bone, Vector2.zero, 26f);
        close.onClick.AddListener(() => SetOpen(false));

        // the picture of the item, in a frame
        var frame = UiKit.Shape(root, "Picture Frame", UiKit.Round, new Color(0f, 0f, 0f, 0.45f), 18f, new Vector2(-290f, 28f), new Vector2(300f, 300f));
        var frameEdge = UiKit.Shape(frame.transform, "Edge", UiKit.RoundOutline, Edge, 18f, Vector2.zero, new Vector2(300f, 300f));
        UiKit.Stretch(frameEdge.rectTransform);

        iconImage = UiKit.IconImage(frame.transform, "Icon", "", Color.white, Vector2.zero, 250f);
        iconImage.enabled = false;
        letterText = UiKit.Label(frame.transform, "Letter", "G", heading, 150f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(280f, 280f));

        // the arrows under the picture
        previousButton = ThemeButton(root, "Previous", "", new Vector2(-370f, -168f), new Vector2(64f, 64f));
        UiKit.IconImage(previousButton.transform, "Icon", "chevron-left", Bone, Vector2.zero, 34f);
        nextButton = ThemeButton(root, "Next", "", new Vector2(-210f, -168f), new Vector2(64f, 64f));
        UiKit.IconImage(nextButton.transform, "Icon", "chevron-right", Bone, Vector2.zero, 34f);

        // the name, the level, the percent and the bar
        // the names are long ("Molecular Encapsulation Phial - MEP"): DM Sans, and the size shrinks to fit. Myanmar names get the Myanmar font by themselves
        titleText = UiKit.Label(root, "Name", "Proton Emitter Gun", UiKit.DmSemi, 50f, Bone, TextAlignmentOptions.MidlineLeft, new Vector2(190f, 112f), new Vector2(520f, 76f));
        titleText.enableWordWrapping = false;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMax = 50f;
        titleText.fontSizeMin = 20f;
        levelText = UiKit.Label(root, "Level", "", UiKit.DmSemi, 28f, Blood, TextAlignmentOptions.MidlineLeft, new Vector2(190f, 54f), new Vector2(520f, 40f));
        percentText = UiKit.Label(root, "Percent", "10 / 100 %", UiKit.DmMedium, 30f, Bone, TextAlignmentOptions.MidlineLeft, new Vector2(190f, 6f), new Vector2(520f, 40f));

        var bar = UiKit.Shape(root, "Bar", UiKit.Round, new Color(0f, 0f, 0f, 0.55f), 8f, new Vector2(190f, -40f), new Vector2(520f, 16f));
        var fill = UiKit.NewRect("Fill", bar.transform);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0.1f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = UiKit.Round;
        fillImage.type = Image.Type.Sliced;
        fillImage.pixelsPerUnitMultiplier = UiKit.Border9 / 8f;
        fillImage.color = Blood;
        fillImage.raycastTarget = false;
        barFill = fill;

        // the UPGRADE button (spends 1 point)
        upgradeButton = ThemeButton(root, "Upgrade", "UPGRADE", new Vector2(190f, -118f), new Vector2(320f, 74f));
        var buttonLabel = upgradeButton.GetComponentInChildren<TMP_Text>();
        if (buttonLabel != null) buttonLabel.fontSize = 30f;
        var buttonEdge = upgradeButton.transform.Find("Edge");
        if (buttonEdge != null) buttonEdge.GetComponent<Image>().color = Blood;

        resultText = UiKit.Label(root, "Result", "", UiKit.DmSemi, 28f, Blood, TextAlignmentOptions.MidlineLeft, new Vector2(190f, -188f), new Vector2(520f, 40f));

        var hint = UiKit.Label(root, "Hint", hintText, UiKit.DmMedium, 24f, new Color(Bone.r, Bone.g, Bone.b, 0.55f), TextAlignmentOptions.Center,
            new Vector2(0f, -266f), new Vector2(940f, 36f));
        hint.raycastTarget = false;

        panelRoot.SetActive(false);
    }

    // A round dark button with a red edge. 'label' may be empty (an icon is put on it)
    Button ThemeButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
    {
        float radius = size.y * 0.5f;
        var plate = UiKit.Shape(parent, name, UiKit.Round, new Color(0.08f, 0.07f, 0.08f, 1f), radius, position, size);
        plate.raycastTarget = true;
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, radius, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.55f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        if (!string.IsNullOrEmpty(label))
        {
            var text = UiKit.Label(plate.transform, "Label", label, UiKit.DmSemi, size.y * 0.4f, Bone, TextAlignmentOptions.Center, Vector2.zero, size);
            text.raycastTarget = false;
        }
        return button;
    }

    // An UPGRADE button for phones
    void BuildMobileButton()
    {
        if (!showMobileButton || !DeviceDetection.IsMobile() || panelRoot == null) return;

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        var button = MakeButton(canvas.transform, "UpgradeOpenButton", "UPGRADE", Vector2.zero, new Vector2(150f, 100f), new Color(0f, 0f, 0f, 0.55f), Color.white);
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30f, -550f);
        button.GetComponentInChildren<TMP_Text>().fontSize = 24f;
        button.onClick.AddListener(Toggle);
    }

    GameObject NewChild(Transform parent, string name, params System.Type[] extra)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var type in extra) go.AddComponent(type);
        return go;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions alignment, FontStyles style, Vector2 position, Vector2 boxSize)
    {
        var go = NewChild(parent, name);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = alignment;
        t.fontStyle = style;
        t.color = Color.white;
        t.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = boxSize;
        rt.anchoredPosition = position;
        return t;
    }

    Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, Color labelColor)
    {
        var go = NewChild(parent, name, typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.color = color;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var text = MakeText(go.transform, "Label", label, 56f, TextAlignmentOptions.Center, FontStyles.Bold, Vector2.zero, size);
        text.color = labelColor;
        var textRt = (RectTransform)text.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        return button;
    }
}
