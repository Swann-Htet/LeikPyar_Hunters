using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// The frame rate and the battery at the top of the screen, like in PUBG:   60 FPS   [battery] 85%
// The FPS is green when smooth, yellow when so-so, red when slow. The battery is red when low and blue while charging.
// Both can be switched off in the Settings. If the device does not tell its battery (desktop PC, iPhone Safari) it is hidden.
// Add it with Tools > Ghost > Setup Settings (it is placed in the HUD canvas where you can move it), or it builds itself.
public class SystemStatus : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void JS_BatteryInit();
    [DllImport("__Internal")] private static extern int JS_BatteryPercent();
    [DllImport("__Internal")] private static extern int JS_BatteryCharging();
#endif

    [Header("UI (built automatically if empty)")]
    public TMP_Text fpsText;
    public GameObject batteryGroup;
    public RectTransform batteryFill;
    public Image batteryFillImage;
    public TMP_Text batteryText;

    [Header("Colors")]
    public Color goodColor = new Color(0.92f, 0.88f, 0.80f, 0.85f);
    public Color okColor = new Color(0.92f, 0.88f, 0.80f, 0.85f);
    public Color badColor = new Color(0.93f, 0.14f, 0.18f, 1f);
    public Color chargingColor = new Color(0.92f, 0.88f, 0.80f, 0.85f);

    private float fps = 60f;
    private float nextText;
    private float nextBattery;
    private int percent = -1;
    private bool charging;

    void Start()
    {
        // the numbers use the HUD font of the new UI (Rajdhani), if it was set up
        var numbers = UiKit.DmSemi != null ? UiKit.DmSemi : UiKit.Numbers;
        if (numbers != null)
        {
            if (fpsText != null) fpsText.font = numbers;
            if (batteryText != null) batteryText.font = numbers;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        JS_BatteryInit();
#endif
        GameSettings.Changed += ApplyVisibility;
        ReadBattery();
        ApplyVisibility();
        ShowBattery();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= ApplyVisibility;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) fps = Mathf.Lerp(fps, 1f / dt, 0.08f);

        if (Time.unscaledTime >= nextText)
        {
            nextText = Time.unscaledTime + 0.25f;
            if (fpsText != null && fpsText.gameObject.activeSelf)
            {
                fpsText.text = Mathf.RoundToInt(fps) + " FPS";
                fpsText.color = fps >= 50f ? goodColor : fps >= 30f ? okColor : badColor;
            }
        }

        if (Time.unscaledTime >= nextBattery)
        {
            // a browser needs a moment to answer, so it is asked more often in the first seconds
            nextBattery = Time.unscaledTime + (Time.unscaledTime < 5f ? 0.5f : 5f);
            ReadBattery();
            ApplyVisibility();
            ShowBattery();
        }
    }

    void ApplyVisibility()
    {
        if (fpsText != null) fpsText.gameObject.SetActive(GameSettings.ShowFps);
        if (batteryGroup != null) batteryGroup.SetActive(GameSettings.ShowBattery && percent >= 0);
    }

    void ReadBattery()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        percent = JS_BatteryPercent();
        charging = JS_BatteryCharging() == 1;
#else
        float level = SystemInfo.batteryLevel;
        percent = level < 0f ? -1 : Mathf.RoundToInt(level * 100f);
        charging = SystemInfo.batteryStatus == BatteryStatus.Charging;
#endif
    }

    void ShowBattery()
    {
        if (percent < 0) return;

        Color color = charging ? chargingColor : percent > 40 ? goodColor : percent > 20 ? okColor : badColor;

        if (batteryText != null)
        {
            batteryText.text = percent + "%";
            batteryText.color = color;
        }
        if (batteryFillImage != null) batteryFillImage.color = color;
        if (batteryFill != null) batteryFill.anchorMax = new Vector2(Mathf.Clamp01(percent / 100f), 1f);
    }

    // ===================== BUILDING =====================

    // Makes the status bar under 'parent' (used by the setup tool, and by the script itself when nothing was built).
    // k = 1 for a 1920 wide canvas, 0.5 for a 960 wide one
    public static SystemStatus Build(Transform parent, TMP_FontAsset font, float k)
    {
        var bar = new GameObject("StatusBar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);

        var rt = (RectTransform)bar.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(380f * k, 46f * k);
        rt.anchoredPosition = new Vector2(0f, -16f * k);

        var status = bar.AddComponent<SystemStatus>();
        BuildInto(bar.transform, font, k, status);
        return status;
    }

    static void BuildInto(Transform bar, TMP_FontAsset font, float k, SystemStatus status)
    {
        // FPS (left)
        status.fpsText = NewText(bar, "FpsText", "60 FPS", 32f * k, TextAlignmentOptions.MidlineRight, new Vector2(-110f * k, 0f), new Vector2(170f * k, 46f * k), font);

        // Battery (right)
        var group = new GameObject("Battery", typeof(RectTransform));
        group.transform.SetParent(bar, false);
        var groupRt = (RectTransform)group.transform;
        groupRt.anchorMin = groupRt.anchorMax = new Vector2(0.5f, 0.5f);
        groupRt.sizeDelta = new Vector2(190f * k, 46f * k);
        groupRt.anchoredPosition = new Vector2(70f * k, 0f);
        status.batteryGroup = group;

        var frame = NewImage(group.transform, "Frame", new Color(0.92f, 0.87f, 0.79f, 0.95f), new Vector2(-62f * k, 0f), new Vector2(50f * k, 24f * k));
        NewImage(group.transform, "Cap", new Color(0.92f, 0.87f, 0.79f, 0.95f), new Vector2(-32f * k, 0f), new Vector2(5f * k, 10f * k));

        var inner = NewImage(frame.transform, "Inner", new Color(0.05f, 0.03f, 0.04f, 1f), Vector2.zero, Vector2.zero);
        var innerRt = (RectTransform)inner.transform;
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(3f * k, 3f * k);
        innerRt.offsetMax = new Vector2(-3f * k, -3f * k);

        var fill = NewImage(inner.transform, "Fill", status.goodColor, Vector2.zero, Vector2.zero);
        var fillRt = (RectTransform)fill.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        status.batteryFill = fillRt;
        status.batteryFillImage = fill;

        status.batteryText = NewText(group.transform, "BatteryText", "100%", 32f * k, TextAlignmentOptions.MidlineLeft, new Vector2(38f * k, 0f), new Vector2(110f * k, 46f * k), font);
    }

    static TMP_Text NewText(Transform parent, string name, string text, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 box, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = position;

        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = Color.white;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        go.AddComponent<LocalizeIgnore>();
        return t;
    }

    static Image NewImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
