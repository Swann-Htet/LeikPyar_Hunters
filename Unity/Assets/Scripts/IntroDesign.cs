using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The look of the intro menu (the new design): the butterfly logo, LEIKPYAR HUNTERS with a cold glow, SPECTRAL AGENTS in orange, and the
// five entries (START NEW GAME, TUTORIAL, LEADERBOARD, CREDITS, QUIT) centred in a column on the left. The language text is at the top
// right (plain, no box) and "Version 1.1" is at the bottom right.
// It makes itself in the intro scene (nothing to set up): the old title, tagline and entries of the horror menu are switched off and this
// takes their place. The old buttons stay in the scene, hidden, so nothing else breaks. It is built inside the 'Menu' object, so the
// start of the game (SceneChanger) hides it together with the rest.
// To change something: the numbers of the layout are in Build(), the colours are just below.
public class IntroDesign : MonoBehaviour
{
    [Tooltip("The number shown at the bottom right")]
    public string version = "1.1";

    static readonly Color Ice = new Color(0.55f, 0.88f, 1f, 1f);          // the cold glow of the title and of an entry under the pointer
    static readonly Color White = new Color(0.97f, 0.98f, 1f, 1f);
    static readonly Color Amber = new Color(0.96f, 0.47f, 0.2f, 1f);      // SPECTRAL AGENTS

    // the old things of the horror menu that this design replaces
    static readonly string[] OldNames = { "Title", "TitleGlow", "Tagline", "Divider", "Start", "Tutorial", "LoadGame", "Credits", "Quit", "Toast" };

    private IntroMenu menu;
    private RectTransform container;
    private RectTransform canvasRect;
    private RectTransform column;
    private CanvasGroup columnGroup;
    private float k = 1f;
    private float lastHeight = -1f;
    private float shownAt;


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
        if (FindFirstObjectByType<IntroMenu>() == null || FindFirstObjectByType<IntroDesign>() != null) return;
        new GameObject("Intro Design (auto)").AddComponent<IntroDesign>();
    }

    void Start()
    {
        menu = FindFirstObjectByType<IntroMenu>();
        if (menu == null) { enabled = false; return; }

        container = menu.changer != null && menu.changer.startButton != null ? menu.changer.startButton.transform as RectTransform : null;
        if (container == null) container = FindNamed(null, "Menu") as RectTransform;
        var canvas = container != null ? container.GetComponentInParent<Canvas>() : null;
        if (canvas == null) { enabled = false; return; }
        canvas = canvas.rootCanvas;
        canvasRect = (RectTransform)canvas.transform;

        // every size below is written for a canvas 1920 wide
        var scaler = canvas.GetComponent<CanvasScaler>();
        k = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution.x > 100f
            ? scaler.referenceResolution.x / 1920f : 1f;

        shownAt = Time.unscaledTime;

        HideOld();
        Build();
        StartCoroutine(Tidy());
    }

    // ---------------------------------------------------------------- the old menu goes

    void HideOld()
    {
        // the whole 'Menu' object now covers the screen (it has no picture, so nothing is drawn): the new column sits inside it
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = container.offsetMax = Vector2.zero;
        container.pivot = new Vector2(0.5f, 0.5f);
        container.localScale = Vector3.one;
        if (container.GetComponent<HorrorSkinIgnore>() == null) container.gameObject.AddComponent<HorrorSkinIgnore>();

        foreach (var name in OldNames)
        {
            var old = FindNamed(container, name);
            if (old != null) old.gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------- the new design

    float S(float v) { return v * k; }

    void Build()
    {
        column = UiKit.NewRect("Intro Design", container);
        column.anchorMin = column.anchorMax = new Vector2(0.17f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(S(700f), S(900f));
        column.anchoredPosition = Vector2.zero;
        columnGroup = column.gameObject.AddComponent<CanvasGroup>();
        columnGroup.alpha = 0f;

        // the butterfly (its picture has a little light of its own)
        var logoRt = UiKit.NewRect("Logo", column);
        UiKit.Place(logoRt, new Vector2(0f, S(348f)), new Vector2(S(124f), S(124f)));
        var logo = logoRt.gameObject.AddComponent<RawImage>();
        logo.texture = Resources.Load<Texture2D>("Brand/logo");
        logo.raycastTarget = false;
        logo.enabled = logo.texture != null;

        // LEIKPYAR / HUNTERS on two lines: white letters on a thin dark shadow (no glow: it is easier to read)
        TitleLine("LEIKPYAR", S(240f));
        TitleLine("HUNTERS", S(172f));

        // a line with a diamond, SPECTRAL AGENTS (it keeps its first font, the squared Rajdhani), and the same line again in orange
        Divider(S(128f), new Color(Ice.r, Ice.g, Ice.b, 0.9f));
        var sub = Brand(column, "Subtitle", "SPECTRAL AGENTS", 34f, Amber, new Vector2(0f, S(90f)), new Vector2(S(640f), S(50f)), 7f, UiKit.Numbers);
        sub.fontStyle = FontStyles.Bold;
        Divider(S(52f), new Color(Amber.r, Amber.g, Amber.b, 0.85f));

        // the five entries
        string[] names = { "START NEW GAME", "TUTORIAL", "LEADERBOARD", "CREDITS", "QUIT" };
        UnityEngine.Events.UnityAction[] actions = { menu.StartNewGame, menu.StartTutorial, menu.LoadGame, menu.OpenCredits, menu.Quit };
        for (int i = 0; i < names.Length; i++)
            Entry(names[i], actions[i], S(-18f - i * 70f), i == 0);

        // the small message under the menu (QUIT on the web)
        var toast = UiKit.Label(column, "Toast", "", UiKit.DmMedium, S(28f), new Color(1f, 0.62f, 0.38f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, S(-350f)), new Vector2(S(640f), S(44f)));
        toast.raycastTarget = false;
        menu.toast = toast;

        // "Version 1.1" at the bottom right
        var ver = UiKit.Label(container, "Version", "Version " + version, UiKit.DmMedium, S(30f), new Color(White.r, White.g, White.b, 0.92f),
            TextAlignmentOptions.BottomRight, Vector2.zero, new Vector2(S(420f), S(44f)));
        var vr = ver.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0f);
        vr.pivot = new Vector2(1f, 0f);
        vr.anchoredPosition = new Vector2(-S(34f), S(14f));
        ver.raycastTarget = false;
        ver.gameObject.AddComponent<LocalizeIgnore>();
    }

    // The font of the game's name: Audiowide (Resources/UIFonts/Audiowide-Regular.ttf, free, SIL Open Font License). The game makes the
    // TextMeshPro font from the file when it starts; if the file is missing the squared Rajdhani is used.
    static TMP_FontAsset titleFont;

    static TMP_FontAsset TitleFont()
    {
        if (titleFont != null) return titleFont;

        var file = Resources.Load<Font>("UIFonts/Audiowide-Regular");
        if (file != null) titleFont = TMP_FontAsset.CreateFontAsset(file);
        if (titleFont == null) titleFont = UiKit.Numbers;
        return titleFont;
    }

    // A thin line that fades out at both ends, with a small diamond in the middle
    void Divider(float y, Color color)
    {
        float length = 200f, gap = 14f;
        for (int side = -1; side <= 1; side += 2)
        {
            var rt = UiKit.NewRect(side < 0 ? "Divider Left" : "Divider Right", column);
            UiKit.Place(rt, new Vector2(side * S(gap + length * 0.5f), y), new Vector2(S(length), Mathf.Max(1.5f, S(2.5f))));
            rt.localScale = new Vector3(-side, 1f, 1f);        // the left line is the right one turned round: it fades towards the outside
            var line = rt.gameObject.AddComponent<Image>();
            line.sprite = FadeSprite();
            line.color = color;
            line.raycastTarget = false;
        }

        var diamond = UiKit.NewRect("Divider Diamond", column);
        UiKit.Place(diamond, new Vector2(0f, y), new Vector2(S(12f), S(12f)));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var image = diamond.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static Sprite fadeSprite;

    // 128 x 1 pixels: clear on the left, solid on the right
    static Sprite FadeSprite()
    {
        if (fadeSprite != null) return fadeSprite;

        var texture = new Texture2D(128, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int x = 0; x < 128; x++)
            texture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(x / 127f, 1.3f)));
        texture.Apply(false);
        fadeSprite = Sprite.Create(texture, new Rect(0f, 0f, 128f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return fadeSprite;
    }

    // one line of the title: a dark shadow, then the white letters
    void TitleLine(string text, float y)
    {
        Brand(column, "Shadow " + text, text, 62f, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, y - S(3f)), new Vector2(S(560f), S(80f)), 6f, TitleFont());
        Brand(column, "Title " + text, text, 62f, White, new Vector2(0f, y), new Vector2(S(560f), S(80f)), 6f, TitleFont());
    }

    // a text of the game's name: it is never translated, never wraps (it gets smaller if it is too wide)
    TMP_Text Brand(Transform parent, string name, string text, float size, Color color, Vector2 position, Vector2 box, float spacing, TMP_FontAsset font)
    {
        var t = UiKit.Label(parent, name, text, font, S(size), color, TextAlignmentOptions.Center, position, box);
        t.characterSpacing = spacing;
        t.enableAutoSizing = true;
        t.fontSizeMax = S(size);
        t.fontSizeMin = S(size * 0.4f);
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.gameObject.AddComponent<LocalizeIgnore>();
        return t;
    }

    void Entry(string text, UnityEngine.Events.UnityAction action, float y, bool first)
    {
        var rt = UiKit.NewRect("Entry " + text, column);
        UiKit.Place(rt, new Vector2(0f, y), new Vector2(S(560f), S(66f)));

        // see-through, but it takes the clicks
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.AddListener(action);

        var label = UiKit.Label(rt, "Label", text, first ? UiKit.Strong : UiKit.DmMedium, S(first ? 38f : 34f), White, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(S(560f), S(66f)));
        label.characterSpacing = 3f;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        // a thin line under the text that grows under the pointer
        var line = UiKit.Shape(rt, "Line", UiKit.Round, new Color(Ice.r, Ice.g, Ice.b, 0f), 1.5f, new Vector2(0f, -S(26f)), new Vector2(S(300f), Mathf.Max(2f, S(3f))));
        line.raycastTarget = false;
        line.rectTransform.localScale = new Vector3(0f, 1f, 1f);

        var entry = rt.gameObject.AddComponent<IntroEntry>();
        entry.label = label;
        entry.line = line;
        entry.hoverColor = Ice;
        entry.normalColor = White;
    }

    // ---------------------------------------------------------------- the things other scripts make a moment later

    IEnumerator Tidy()
    {
        for (int i = 0; i < 4; i++) yield return null;

        // the TUTORIAL button that TutorialMenuButton copies from LOAD GAME is a copy of a hidden object (hidden too), but make sure
        var old = FindNamed(container, "Tutorial");
        if (old != null) old.gameObject.SetActive(false);

        // the hint of the old menu
        var hint = FindNamed(canvasRect, "Hint");
        if (hint != null) hint.gameObject.SetActive(false);

        StyleLanguage();

        // A- 100% A+ : bottom left, under the menu (the bottom right belongs to the version and the saved game)
        var size = FindNamed(null, "Text Size") as RectTransform;
        if (size != null)
        {
            size.anchorMin = size.anchorMax = new Vector2(0.17f, 0f);   // under the menu column
            size.pivot = new Vector2(0.5f, 0f);
            size.anchoredPosition = new Vector2(0f, 40f);
        }

        // the saved game: a little higher, so the version stays free
        var card = FindNamed(null, "Continue") as RectTransform;
        if (card != null) card.anchoredPosition = new Vector2(-40f, 76f);
    }

    // The language text at the top right: plain text, no box
    void StyleLanguage()
    {
        var language = FindNamed(canvasRect, "LanguageButton") as RectTransform;
        if (language == null) return;

        language.anchorMin = language.anchorMax = new Vector2(1f, 1f);
        language.pivot = new Vector2(1f, 1f);
        language.anchoredPosition = new Vector2(-S(44f), -S(28f));
        language.sizeDelta = new Vector2(S(340f), S(84f));

        var image = language.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;     // otherwise the invisible area takes no clicks
        }

        var label = language.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            label.enableAutoSizing = false;
            label.fontSize = S(46f);
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.color = White;
            label.characterSpacing = 0f;
            label.enableWordWrapping = false;
        }
    }

    // ---------------------------------------------------------------- life

    void Update()
    {
        if (column == null) return;

        // it fades in
        columnGroup.alpha = Mathf.Clamp01((Time.unscaledTime - shownAt - 0.15f) / 0.8f);

        // a short screen (a wide phone): the column gets smaller so all of it is seen
        float height = canvasRect.rect.height;
        if (!Mathf.Approximately(height, lastHeight))
        {
            lastHeight = height;
            float fit = Mathf.Min(1f, height / S(880f));
            column.localScale = new Vector3(fit, fit, 1f);
        }
    }

    // ---------------------------------------------------------------- helpers

    static Transform FindNamed(Transform root, string name)
    {
        if (root != null)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }
}

// One entry of the new intro menu: it turns ice blue, grows a little and shows a thin line under the pointer
public class IntroEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text label;
    public Image line;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.cyan;

    private float amount;
    private bool hover;

    void OnEnable()
    {
        hover = false;
        amount = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) { hover = true; }
    public void OnPointerExit(PointerEventData eventData) { hover = false; }

    void Update()
    {
        amount = Mathf.MoveTowards(amount, hover ? 1f : 0f, Time.unscaledDeltaTime * 7f);
        Apply();
    }

    void Apply()
    {
        if (label != null)
        {
            label.color = Color.Lerp(normalColor, hoverColor, amount);
            label.rectTransform.localScale = Vector3.one * (1f + 0.07f * amount);
        }

        if (line != null)
        {
            line.rectTransform.localScale = new Vector3(amount, 1f, 1f);
            var c = hoverColor;
            c.a = 0.9f * amount;
            line.color = c;
        }
    }
}
