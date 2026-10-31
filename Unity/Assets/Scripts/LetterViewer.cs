using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reads a letter: the picture flies up from the bottom (where the backpack is), unfolds in the middle of the screen and floats a little.
// Click anywhere, tap, or press Esc / Enter / Space / E / F / B to fold it away again.
// The game waits while a letter is open. Opened from the backpack (Inventory.OnSlotClicked) with LetterViewer.Show(sprite).
// It makes itself the first time it is needed.
public class LetterViewer : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    static LetterViewer instance;

    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    const float OpenTime = 0.95f;
    const float CloseTime = 0.34f;
    const float DimAlpha = 0.86f;

    enum Phase { Hidden, Opening, Open, Closing }

    private Phase phase = Phase.Hidden;
    private float phaseTime;
    private float openedAt;

    private GameObject root;
    private Image dim;
    private RectTransform sheet;
    private CanvasGroup sheetGroup;
    private RectTransform shadowRt;
    private Image paper, shadow;
    private CanvasGroup hintGroup;
    private TMP_Text hint;

    private float previousTimeScale = 1f;
    private bool pausedByUs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        IsOpen = false;
        instance = null;
    }

    public static void Show(Sprite page)
    {
        if (page == null) return;

        if (instance == null)
        {
            var go = new GameObject("Letter Viewer (auto)");
            instance = go.AddComponent<LetterViewer>();
        }
        instance.Open(page);
    }

    bool Phone { get { return DeviceDetection.IsMobile(); } }

    void Awake()
    {
        instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (IsOpen) Finish();
        if (instance == this) instance = null;
    }

    // ---------------------------------------------------------------- the screen

    void Build()
    {
        var canvasGo = new GameObject("LetterCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();
        root = canvasGo;

        // the dark behind the letter: a click or a tap anywhere folds the letter away
        var dimRt = UiKit.NewRect("Dim", canvasGo.transform);
        UiKit.Stretch(dimRt);
        dim = dimRt.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.raycastTarget = true;
        var button = dimRt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = dim;
        button.onClick.AddListener(Close);

        // the sheet: the shadow and the paper move together
        sheet = UiKit.NewRect("Sheet", canvasGo.transform);
        sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 0.5f);
        sheet.pivot = new Vector2(0.5f, 0.5f);
        sheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();
        sheetGroup.blocksRaycasts = false;
        sheetGroup.interactable = false;

        shadowRt = UiKit.NewRect("Shadow", sheet);
        shadow = shadowRt.gameObject.AddComponent<Image>();
        shadow.color = new Color(0f, 0f, 0f, 0.55f);
        shadow.preserveAspect = true;
        shadow.raycastTarget = false;

        var paperRt = UiKit.NewRect("Paper", sheet);
        UiKit.Stretch(paperRt);
        paper = paperRt.gameObject.AddComponent<Image>();
        paper.preserveAspect = true;
        paper.raycastTarget = false;

        hint = UiKit.Label(canvasGo.transform, "Hint", Phone ? "Tap anywhere to close" : "Click anywhere or press Esc to close",
            UiKit.DmMedium, 30f, Bone, TextAlignmentOptions.Center, new Vector2(0f, -490f), new Vector2(1200f, 50f));
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hint.raycastTarget = false;
        hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        root.SetActive(false);
    }

    // ---------------------------------------------------------------- open / close

    void Open(Sprite page)
    {
        if (IsOpen) return;
        if (SettingsPanel.IsOpen || UpgradePanel.IsOpen || GuidePanel.IsOpen) return;
        if (Inventory.Instance != null) Inventory.Instance.SetOpen(false);

        // as big as the screen allows (1920 x 1080 reference), the shape of the picture
        float aspect = page.rect.width / Mathf.Max(1f, page.rect.height);
        float height = 920f;
        float width = height * aspect;
        if (width > 1560f) { width = 1560f; height = width / aspect; }
        sheet.sizeDelta = new Vector2(width, height);
        shadowRt.anchorMin = shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRt.sizeDelta = new Vector2(width, height);
        shadowRt.anchoredPosition = new Vector2(16f, -24f);
        paper.sprite = page;
        shadow.sprite = page;

        IsOpen = true;
        phase = Phase.Opening;
        phaseTime = 0f;
        openedAt = Time.unscaledTime;
        root.SetActive(true);
        Pose(0f);

        // the game waits, the mouse is free
        if (Time.timeScale > 0f)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByUs = true;
        }
        if (!Phone)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        PlayPaperSound();
    }

    public void Close()
    {
        if (!IsOpen || phase == Phase.Closing || phase == Phase.Hidden) return;

        phase = Phase.Closing;
        phaseTime = 0f;
        PlayPaperSound();
    }

    void Finish()
    {
        IsOpen = false;
        phase = Phase.Hidden;
        if (root != null) root.SetActive(false);

        if (pausedByUs)
        {
            Time.timeScale = previousTimeScale;
            pausedByUs = false;
        }
        if (!Phone)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // A rustle of paper if you add a clip named Paper to Resources/Sfx (optional)
    void PlayPaperSound()
    {
        var clip = Resources.Load<AudioClip>("Sfx/Paper");
        if (clip == null) return;

        Vector3 at = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, at, 0.8f);
    }

    // ---------------------------------------------------------------- the animation (real time: the game is paused)

    void Update()
    {
        if (!IsOpen) return;

        phaseTime += Time.unscaledDeltaTime;

        if (phase == Phase.Opening)
        {
            float t = Mathf.Clamp01(phaseTime / OpenTime);
            Pose(t);
            if (t >= 1f) phase = Phase.Open;
        }
        else if (phase == Phase.Open)
        {
            Idle();
            if (Time.unscaledTime - openedAt > 0.35f && ClosePressed()) Close();
        }
        else if (phase == Phase.Closing)
        {
            float c = Mathf.Clamp01(phaseTime / CloseTime);
            Fold(c);
            if (c >= 1f) Finish();
        }

        // nothing may lock the mouse again while the letter is open
        if (IsOpen && !Phone && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    static bool ClosePressed()
    {
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) ||
               Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.I);
    }

    static float EaseOutCubic(float x) { x = 1f - x; return 1f - x * x * x; }
    static float EaseInCubic(float x) { return x * x * x; }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    // t 0..1: the folded letter flies up from below (first half), then unfolds and settles (second half)
    void Pose(float t)
    {
        float y, scale, fold, rotation, alpha;

        if (t < 0.5f)
        {
            float a = EaseOutCubic(t / 0.5f);
            y = Mathf.LerpUnclamped(-560f, 0f, a);
            scale = Mathf.Lerp(0.16f, 0.8f, a);
            fold = 0.42f;                              // folded: narrow
            rotation = Mathf.Lerp(-16f, 3f, a);
            alpha = Mathf.Clamp01(t / 0.12f);
        }
        else
        {
            float b = (t - 0.5f) / 0.5f;
            y = 0f;
            scale = Mathf.Lerp(0.8f, 1f, EaseOutCubic(b));
            fold = Mathf.LerpUnclamped(0.42f, 1f, EaseOutBack(b));
            rotation = Mathf.Lerp(3f, 0f, EaseOutCubic(b));
            alpha = 1f;
        }

        sheet.anchoredPosition = new Vector2(0f, y);
        sheet.localRotation = Quaternion.Euler(0f, 0f, rotation);
        sheet.localScale = new Vector3(scale * fold, scale, 1f);
        sheetGroup.alpha = alpha;

        dim.color = new Color(0f, 0f, 0f, DimAlpha * EaseOutCubic(Mathf.Clamp01(t / 0.4f)));
        hintGroup.alpha = Mathf.Clamp01((t - 0.9f) / 0.1f) * 0.85f;
    }

    // the letter floats a little while it is read
    void Idle()
    {
        float time = Time.unscaledTime;
        sheet.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 1.3f) * 6f);
        sheet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.9f) * 0.45f);
        sheet.localScale = Vector3.one;
        sheetGroup.alpha = 1f;
        dim.color = new Color(0f, 0f, 0f, DimAlpha);
        hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, 0.85f, Time.unscaledDeltaTime * 3f);
    }

    // c 0..1: the letter folds up and drops back down
    void Fold(float c)
    {
        float e = EaseInCubic(c);
        sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -520f, e));
        sheet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 8f, e));
        sheet.localScale = new Vector3(Mathf.Lerp(1f, 0.16f * 0.5f, e), Mathf.Lerp(1f, 0.2f, e), 1f);
        sheetGroup.alpha = 1f - c * c;
        dim.color = new Color(0f, 0f, 0f, DimAlpha * (1f - c));
        hintGroup.alpha = Mathf.Clamp01(1f - c * 4f) * 0.85f;
    }
}
