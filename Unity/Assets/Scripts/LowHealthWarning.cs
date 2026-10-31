using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The low health warning, lower left (above the bottom edge, near the joystick):
//   [!]  LOW HEALTH!
//        Health is critical.
//        Find a health pack or take cover.
// A dark see-through panel with a thin muted red border, a little scratched texture, and a slow heartbeat on the border
// (faster when health is almost gone). It appears when health drops to the threshold and goes away when the player heals.
public class LowHealthWarning : MonoBehaviour
{
    [Tooltip("Shown when health is at or below this share of the maximum (0.3 = 30%)")]
    [Range(0.05f, 0.9f)] public float threshold = 0.3f;
    [Tooltip("Below this share the heartbeat is faster")]
    [Range(0.02f, 0.5f)] public float critical = 0.15f;

    [Header("Position (canvas units, 1920 x 1080)")]
    public float leftMargin = 60f;
    [Tooltip("Distance of the panel from the bottom of the screen. 86 = about 8% of the screen height")]
    public float bottomMargin = 86f;

    private static readonly Color Red = new Color(0.78f, 0.1f, 0.14f, 1f);
    private static readonly Color OffWhite = new Color(0.92f, 0.88f, 0.80f, 1f);

    private PlayerHealth health;
    private CanvasGroup group;
    private RectTransform panel;
    private Image border;
    private Image icon;
    private float visible;

    void Start()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        Build();
        group.alpha = 0f;
    }

    void Update()
    {
        if (health == null || health.maxHealth <= 0) return;

        float share = health.currentHealth / (float)health.maxHealth;
        bool show = !health.IsDead && share <= threshold && !SettingsPanel.IsOpen;

        visible = Mathf.MoveTowards(visible, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        float e = visible * visible * (3f - 2f * visible);

        group.alpha = e;
        panel.anchoredPosition = new Vector2(leftMargin - 40f * (1f - e), bottomMargin);

        if (visible <= 0f) return;

        // a double beat, like a heart: strong, weak, pause
        float rate = share <= critical ? 1.9f : 1.1f;
        float phase = Time.unscaledTime * rate % 1f;
        float beat = Mathf.Max(Pulse(phase, 0.0f, 0.12f), 0.6f * Pulse(phase, 0.22f, 0.12f));

        border.color = new Color(Red.r, Red.g, Red.b, 0.35f + 0.55f * beat);
        icon.color = Color.Lerp(new Color(0.55f, 0.08f, 0.1f, 1f), new Color(0.95f, 0.2f, 0.2f, 1f), beat);
        icon.rectTransform.localScale = Vector3.one * (1f + 0.1f * beat);
    }

    static float Pulse(float phase, float start, float length)
    {
        float t = (phase - start) / length;
        if (t < 0f || t > 1f) return 0f;
        return Mathf.Sin(t * Mathf.PI);
    }

    void Build()
    {
        var canvasGo = new GameObject("LowHealthCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<HorrorSkinIgnore>();

        var size = new Vector2(580f, 132f);

        panel = UiKit.NewRect("Warning", canvasGo.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0f);
        panel.sizeDelta = size;
        panel.anchoredPosition = new Vector2(leftMargin, bottomMargin);

        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // dark, see-through
        var back = panel.gameObject.AddComponent<Image>();
        back.sprite = UiKit.Cut;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = UiKit.Border9 / 24f;
        back.color = HudStyler.PlateColor;
        back.raycastTarget = false;

        // scratched, worn look
        var grunge = UiKit.NewRect("Grunge", panel);
        UiKit.Stretch(grunge);
        grunge.offsetMin = new Vector2(8f, 8f);
        grunge.offsetMax = new Vector2(-8f, -8f);
        var grungeImage = grunge.gameObject.AddComponent<Image>();
        grungeImage.sprite = UiKit.Grunge;
        grungeImage.color = new Color(1f, 0.6f, 0.6f, 0.1f);
        grungeImage.raycastTarget = false;

        // the thin red border
        var line = UiKit.NewRect("Border", panel);
        UiKit.Stretch(line);
        border = line.gameObject.AddComponent<Image>();
        border.sprite = UiKit.CutOutline;
        border.type = Image.Type.Sliced;
        border.pixelsPerUnitMultiplier = UiKit.Border9 / 24f;
        border.color = new Color(Red.r, Red.g, Red.b, 0.6f);
        border.raycastTarget = false;

        // the warning triangle
        var iconRt = UiKit.NewRect("Icon", panel);
        UiKit.Place(iconRt, new Vector2(66f, size.y * 0.5f), new Vector2(64f, 64f));
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0f);
        iconRt.anchoredPosition = new Vector2(66f, size.y * 0.5f);
        icon = iconRt.gameObject.AddComponent<Image>();
        icon.sprite = UiKit.Icon("triangle-alert");
        icon.preserveAspect = true;
        icon.color = Red;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;

        // the words: DM Sans
        var semi = UiKit.DmSemi;
        var medium = UiKit.DmMedium;

        // the title uses the game font (MetalMania), like the clock
        var titleFont = HudStyler.GameFont != null ? HudStyler.GameFont : semi;
        var title = Text(panel, "Title", "LOW HEALTH!", titleFont, 38f, new Color(0.86f, 0.14f, 0.17f, 1f), new Vector2(124f, size.y - 38f), new Vector2(430f, 50f));
        title.characterSpacing = 2f;
        Text(panel, "Line 1", "Health is critical.", medium, 23f, OffWhite, new Vector2(124f, size.y - 72f), new Vector2(430f, 30f));
        Text(panel, "Line 2", "Find a health pack or take cover.", medium, 23f, new Color(OffWhite.r, OffWhite.g, OffWhite.b, 0.75f), new Vector2(124f, size.y - 100f), new Vector2(440f, 30f));
    }

    static TMP_Text Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Vector2 position, Vector2 box)
    {
        var rt = UiKit.NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = position;

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
