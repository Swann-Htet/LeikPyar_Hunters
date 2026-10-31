using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A looping picture that shows HOW to make the game full screen on a phone: a phone drawn as an outline, the page as grey "skeleton" blocks
// that shimmer, a finger that taps, and a caption for each step. It is shown by MobileFullscreenPrompt.
//   Android (ios = false): the finger taps the green "Play Fullscreen" button, the browser bar slides away and the page fills the screen.
//   iPhone  (ios = true) : Safari has no full screen button for websites. The finger taps "aA" in the address bar, then "Hide Toolbar",
//                          and the toolbar goes away.
// It is made in code, on a canvas of 640 x 400 units that is scaled to fit "Fit To". It runs on real time (the game waits while it is shown).
public class FullscreenGuide : MonoBehaviour
{
    public bool ios;
    [Tooltip("The picture is scaled to fit in this rectangle")]
    public RectTransform fitTo;

    const float W = 640f, H = 400f;

    static readonly Color PhoneColor = new Color(0.07f, 0.07f, 0.09f, 1f);
    static readonly Color EdgeColor = new Color(0.62f, 0.64f, 0.70f, 0.95f);
    static readonly Color ScreenColor = new Color(0.035f, 0.035f, 0.05f, 1f);
    static readonly Color BarColor = new Color(0.15f, 0.15f, 0.18f, 1f);
    static readonly Color PillColor = new Color(0.27f, 0.27f, 0.32f, 1f);
    static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f, 1f);
    static readonly Color Green = new Color(0.2f, 0.65f, 0.35f, 1f);
    static readonly Color GreenBright = new Color(0.35f, 0.9f, 0.5f, 1f);
    static readonly Color Blue = new Color(0.24f, 0.55f, 1f, 0.9f);

    private float cycle;
    private float startTime;

    private RectTransform root, pageRoot, bar, menu;
    private Image finger, ring, button, hotRow;
    private CanvasGroup menuGroup, badgeGroup;
    private TMP_Text caption;
    private string[] captions;

    private readonly System.Collections.Generic.List<Image> skeletons = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.List<float> skeletonAlpha = new System.Collections.Generic.List<float>();

    // Start (not Awake): the prompt sets "ios" right after it adds this component
    void Start()
    {
        root = (RectTransform)transform;
        root.sizeDelta = new Vector2(W, H);
        startTime = Time.unscaledTime;
        cycle = ios ? 11f : 8f;
        captions = ios
            ? new[] { "Tap aA in the address bar", "Tap Hide Toolbar", "Done: the toolbar is hidden" }
            : new[] { "Tap Play Fullscreen", "The browser bar goes away" };
        Build();
    }

    // ---------------------------------------------------------------- the picture

    void Skeleton(Transform parent, string name, Vector2 position, Vector2 size, float radius, float alpha)
    {
        var image = UiKit.Shape(parent, name, UiKit.Round, new Color(1f, 1f, 1f, alpha), radius, position, size);
        skeletons.Add(image);
        skeletonAlpha.Add(alpha);
    }

    void Build()
    {
        // the phone (a landscape phone: 640 x 320) with the screen inside it
        var phone = UiKit.Shape(transform, "Phone", UiKit.Round, PhoneColor, 44f, new Vector2(0f, 40f), new Vector2(640f, 320f));
        var screen = UiKit.Shape(phone.transform, "Screen", UiKit.Round, ScreenColor, 22f, Vector2.zero, new Vector2(600f, 288f));
        screen.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var edge = UiKit.Shape(phone.transform, "Edge", UiKit.RoundOutline, EdgeColor, 44f, Vector2.zero, new Vector2(640f, 320f));
        edge.raycastTarget = false;

        // the page: a picture and some lines (skeleton blocks); it moves up and grows a little when the bar goes away
        pageRoot = UiKit.NewRect("Page", screen.transform);
        UiKit.Place(pageRoot, Vector2.zero, new Vector2(600f, 288f));
        Skeleton(pageRoot, "Picture", new Vector2(-140f, -14f), new Vector2(280f, 150f), 14f, 0.11f);
        Skeleton(pageRoot, "Line 1", new Vector2(170f, 42f), new Vector2(200f, 24f), 8f, 0.15f);
        Skeleton(pageRoot, "Line 2", new Vector2(160f, 10f), new Vector2(180f, 14f), 7f, 0.10f);
        Skeleton(pageRoot, "Line 3", new Vector2(170f, -14f), new Vector2(200f, 14f), 7f, 0.10f);
        Skeleton(pageRoot, "Line 4", new Vector2(150f, -38f), new Vector2(140f, 14f), 7f, 0.08f);

        if (!ios)
        {
            // the green button of the prompt
            button = UiKit.Shape(pageRoot, "Button", UiKit.Round, Green, 20f, new Vector2(0f, -96f), new Vector2(216f, 42f));
            var label = UiKit.Label(button.transform, "Label", "Play Fullscreen", UiKit.DmSemi, 20f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(216f, 42f));
            label.raycastTarget = false;
        }

        // the browser bar (it is outside the screen when it is hidden)
        bar = UiKit.NewRect("Bar", screen.transform);
        UiKit.Place(bar, new Vector2(0f, 122f), new Vector2(600f, 44f));
        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = BarColor;
        barImage.raycastTarget = false;
        UiKit.Shape(bar, "Address", UiKit.Round, PillColor, 12f, new Vector2(ios ? 20f : 0f, 0f), new Vector2(ios ? 290f : 330f, 26f));
        if (ios)
        {
            UiKit.Shape(bar, "aA pill", UiKit.Round, PillColor, 12f, new Vector2(-250f, 0f), new Vector2(62f, 26f));
            var aa = UiKit.Label(bar, "aA", "aA", UiKit.DmSemi, 20f, Bone, TextAlignmentOptions.Center, new Vector2(-250f, 0f), new Vector2(62f, 26f));
            aa.raycastTarget = false;
            aa.gameObject.AddComponent<LocalizeIgnore>();
        }
        else
        {
            for (int i = -1; i <= 1; i++)
                UiKit.Shape(bar, "Dot", UiKit.Circle, new Color(0.75f, 0.75f, 0.8f, 1f), 3f, new Vector2(266f, i * 10f), new Vector2(6f, 6f)).type = Image.Type.Simple;
        }

        // the menu of "aA" (iPhone)
        if (ios)
        {
            var menuImage = UiKit.Shape(screen.transform, "Menu", UiKit.Round, new Color(0.17f, 0.17f, 0.21f, 0.98f), 16f, new Vector2(-165f, 38f), new Vector2(200f, 120f));
            menu = menuImage.rectTransform;
            menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
            menuGroup.alpha = 0f;

            Skeleton(menu, "Row 1", new Vector2(-30f, 36f), new Vector2(110f, 12f), 6f, 0.22f);
            hotRow = UiKit.Shape(menu, "Hot row", UiKit.Round, new Color(Blue.r, Blue.g, Blue.b, 0f), 10f, new Vector2(0f, 10f), new Vector2(184f, 26f));
            var hotLabel = UiKit.Label(menu, "Hide Toolbar", "Hide Toolbar", UiKit.DmSemi, 17f, Color.white, TextAlignmentOptions.Left, new Vector2(10f, 10f), new Vector2(170f, 26f));
            hotLabel.raycastTarget = false;
            hotLabel.gameObject.AddComponent<LocalizeIgnore>();
            Skeleton(menu, "Row 3", new Vector2(-20f, -16f), new Vector2(130f, 12f), 6f, 0.22f);
            Skeleton(menu, "Row 4", new Vector2(-40f, -42f), new Vector2(90f, 12f), 6f, 0.22f);
        }

        // the finger and the ring of a tap
        ring = UiKit.Shape(phone.transform, "Ring", UiKit.Ring, new Color(1f, 1f, 1f, 0f), 27f, Vector2.zero, new Vector2(54f, 54f));
        ring.type = Image.Type.Simple;
        finger = UiKit.Shape(phone.transform, "Finger", UiKit.Circle, new Color(1f, 1f, 1f, 0f), 27f, Vector2.zero, new Vector2(54f, 54f));
        finger.type = Image.Type.Simple;

        // "FULL SCREEN" when it is done
        var badge = UiKit.Shape(phone.transform, "Badge", UiKit.Round, new Color(0f, 0f, 0f, 0.62f), 28f, new Vector2(0f, 6f), new Vector2(300f, 58f));
        badgeGroup = badge.gameObject.AddComponent<CanvasGroup>();
        badgeGroup.alpha = 0f;
        var badgeText = UiKit.Label(badge.transform, "Text", "FULL SCREEN", UiKit.DmSemi, 30f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(300f, 58f));
        badgeText.raycastTarget = false;
        badgeText.characterSpacing = 4f;

        // the words of the step that is shown
        caption = UiKit.Label(transform, "Caption", captions[0], UiKit.DmSemi, 30f, Bone, TextAlignmentOptions.Center, new Vector2(0f, -172f), new Vector2(620f, 52f));
        caption.raycastTarget = false;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 18f;
        caption.fontSizeMax = 30f;
    }

    // ---------------------------------------------------------------- the story (real time, in a loop)

    // a value that moves smoothly through the keys: times[i] -> values[i]
    static float K(float t, float[] times, float[] values)
    {
        if (t <= times[0]) return values[0];
        for (int i = 1; i < times.Length; i++)
        {
            if (t >= times[i]) continue;
            float u = Mathf.InverseLerp(times[i - 1], times[i], t);
            u = u * u * (3f - 2f * u);
            return Mathf.Lerp(values[i - 1], values[i], u);
        }
        return values[values.Length - 1];
    }

    void LateUpdate()
    {
        // fit in the rectangle of the prompt
        if (fitTo != null)
        {
            Rect r = fitTo.rect;
            float s = Mathf.Max(0.05f, Mathf.Min(r.width / W, r.height / H));
            root.localScale = new Vector3(s, s, 1f);
            root.anchoredPosition = Vector2.zero;
        }

        float time = Time.unscaledTime;
        float t = (time - startTime) % cycle;

        // the grey blocks shimmer
        for (int i = 0; i < skeletons.Count; i++)
        {
            var c = skeletons[i].color;
            c.a = skeletonAlpha[i] * (0.82f + 0.32f * Mathf.Sin(time * 2.4f + i * 0.9f));
            skeletons[i].color = c;
        }

        float hide, fingerX, fingerY, fingerAlpha, tap, ringP;
        float badge;
        int step;

        if (!ios)
        {
            hide = K(t, new[] { 0f, 3.2f, 4.1f, 6.6f, 7.5f }, new[] { 0f, 0f, 1f, 1f, 0f });
            badge = K(t, new[] { 0f, 4.0f, 4.5f, 6.4f, 6.9f }, new[] { 0f, 0f, 1f, 1f, 0f });
            fingerX = K(t, new[] { 0f, 0.4f, 2.5f, 3.9f, 4.4f }, new[] { 210f, 210f, 10f, 10f, 150f });
            fingerY = K(t, new[] { 0f, 0.4f, 2.5f, 3.9f, 4.4f }, new[] { -150f, -150f, -104f, -104f, -150f });
            fingerAlpha = K(t, new[] { 0f, 0.4f, 3.9f, 4.4f }, new[] { 0f, 1f, 1f, 0f });
            tap = K(t, new[] { 2.8f, 3.0f, 3.3f }, new[] { 1f, 0.78f, 1f });
            ringP = Mathf.InverseLerp(3.0f, 3.9f, t);
            step = t < 3.3f ? 0 : 1;

            // the green button lights up when it is tapped
            float glow = K(t, new[] { 2.6f, 3.05f, 3.6f }, new[] { 0f, 1f, 0f });
            if (button != null) button.color = Color.Lerp(Green, GreenBright, glow);
        }
        else
        {
            hide = K(t, new[] { 0f, 5.0f, 5.9f, 9.7f, 10.6f }, new[] { 0f, 0f, 1f, 1f, 0f });
            badge = K(t, new[] { 0f, 5.8f, 6.3f, 9.4f, 9.9f }, new[] { 0f, 0f, 1f, 1f, 0f });
            fingerX = K(t, new[] { 0f, 0.3f, 2.0f, 3.0f, 4.2f, 4.9f, 5.5f }, new[] { 150f, 150f, -246f, -246f, -168f, -168f, -100f });
            fingerY = K(t, new[] { 0f, 0.3f, 2.0f, 3.0f, 4.2f, 4.9f, 5.5f }, new[] { -130f, -130f, 112f, 112f, 44f, 44f, -50f });
            fingerAlpha = K(t, new[] { 0f, 0.3f, 5.2f, 5.7f }, new[] { 0f, 1f, 1f, 0f });
            tap = K(t, new[] { 2.0f, 2.2f, 2.45f }, new[] { 1f, 0.78f, 1f }) * K(t, new[] { 4.5f, 4.7f, 4.95f }, new[] { 1f, 0.78f, 1f });
            float start = t < 3.5f ? 2.2f : 4.7f;
            ringP = Mathf.InverseLerp(start, start + 0.85f, t);
            step = t < 2.5f ? 0 : (t < 5.0f ? 1 : 2);

            float menuAlpha = K(t, new[] { 2.3f, 2.7f, 4.9f, 5.2f }, new[] { 0f, 1f, 1f, 0f });
            menuGroup.alpha = menuAlpha;
            menu.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, menuAlpha);
            var hot = hotRow.color;
            hot.a = Blue.a * K(t, new[] { 4.1f, 4.5f, 5.0f, 5.2f }, new[] { 0f, 1f, 1f, 0f });
            hotRow.color = hot;
        }

        // the bar goes away, the page fills the screen
        bar.anchoredPosition = new Vector2(0f, 122f + 44f * hide);
        pageRoot.anchoredPosition = new Vector2(0f, 22f * hide);
        pageRoot.localScale = Vector3.one * (1f + 0.05f * hide);
        badgeGroup.alpha = badge;

        // the finger
        var fingerRt = finger.rectTransform;
        fingerRt.anchoredPosition = new Vector2(fingerX, fingerY);
        fingerRt.localScale = Vector3.one * tap;
        finger.color = new Color(1f, 1f, 1f, 0.88f * fingerAlpha);

        var ringRt = ring.rectTransform;
        ringRt.anchoredPosition = new Vector2(fingerX, fingerY);
        float p = Mathf.Clamp01(ringP);
        bool ringOn = ringP > 0f && ringP < 1f;
        ringRt.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.4f, p);
        ring.color = new Color(1f, 1f, 1f, ringOn ? 0.7f * (1f - p) * fingerAlpha : 0f);

        // the caption of the step
        string text = captions[Mathf.Clamp(step, 0, captions.Length - 1)];
        if (caption.text != text) caption.text = text;
    }
}
