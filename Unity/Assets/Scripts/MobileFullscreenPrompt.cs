using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class WebFullscreen
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int JS_IsIOS();
    [DllImport("__Internal")] private static extern int JS_CanFullscreen();
    [DllImport("__Internal")] private static extern int JS_IsFullscreenOrStandalone();
    [DllImport("__Internal")] private static extern int JS_RequestFullscreen();
    [DllImport("__Internal")] private static extern void JS_ArmFullscreenOnTouch();
    [DllImport("__Internal")] private static extern void JS_ApplyMobileFixes();

    public static bool IsIOS() { return JS_IsIOS() == 1; }
    public static bool CanFullscreen() { return JS_CanFullscreen() == 1; }
    public static bool IsFullscreenOrStandalone() { return JS_IsFullscreenOrStandalone() == 1; }
    public static void RequestFullscreen() { JS_RequestFullscreen(); }
    public static void ArmFullscreenOnTouch() { JS_ArmFullscreenOnTouch(); }
    public static void ApplyMobileFixes() { JS_ApplyMobileFixes(); }
#else
    public static bool IsIOS() { return false; }
    public static bool CanFullscreen() { return false; }
    public static bool IsFullscreenOrStandalone() { return true; }
    public static void RequestFullscreen() { }
    public static void ArmFullscreenOnTouch() { }
    public static void ApplyMobileFixes() { }
#endif
}

// Created automatically in WebGL builds on phones/tablets (no scene setup needed).
// Shows an alert asking the player to go fullscreen (hides the browser URL bar), with a little moving picture of HOW to do it:
//   Android (and iPad): tap the button, the browser bar goes away (FullscreenGuide).
//   iPhone: Safari can not do that for a website, so the picture shows "aA" > "Hide Toolbar" (and the words tell about "Add to Home Screen").
// To look at it in the Editor: press Play, then Tools > Ghost > Preview The Full Screen Guide (Android / iPhone).
public class MobileFullscreenPrompt : MonoBehaviour
{
    private static bool created;

    private GameObject canvasObject;
    private float previousTimeScale = 1f;
    private bool canFullscreen;
    private bool ios;

    // the Editor preview
    private bool preview;
    private bool previewIos;

    public static void Preview(bool iosStyle)
    {
        var go = new GameObject("MobileFullscreenPrompt (preview)");
        var prompt = go.AddComponent<MobileFullscreenPrompt>();
        prompt.preview = true;
        prompt.previewIos = iosStyle;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (created || !DeviceDetection.IsMobile())
            return;

        created = true;
        var go = new GameObject("MobileFullscreenPrompt");
        DontDestroyOnLoad(go);
        go.AddComponent<MobileFullscreenPrompt>();
    }
#endif

    void Start()
    {
        WebFullscreen.ApplyMobileFixes();

        if (!preview && WebFullscreen.IsFullscreenOrStandalone())
        {
            Destroy(gameObject);
            return;
        }

        if (preview)
        {
            ios = previewIos;
            canFullscreen = !previewIos;
        }
        else
        {
            ios = WebFullscreen.IsIOS();
            canFullscreen = WebFullscreen.CanFullscreen();
            if (canFullscreen)
                WebFullscreen.ArmFullscreenOnTouch();
        }

        BuildUI();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    void Confirm()
    {
        if (canFullscreen)
            WebFullscreen.RequestFullscreen();

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (canvasObject != null)
            Destroy(canvasObject);
        Destroy(gameObject);
    }

    void BuildUI()
    {
        canvasObject = new GameObject("FullscreenPromptCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // Dark full-screen panel (also blocks touches on the game behind it)
        var panel = MakeRect("Panel", canvasObject.transform, Vector2.zero, Vector2.one);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.9f);

        // what to say: Android (and iPad) can go full screen by a tap, an iPhone can only hide the Safari toolbar
        string title, body, button;
        if (canFullscreen)
        {
            title = "Play in Fullscreen";
            body = "Tap the button to hide the browser bar and play fullscreen.\nTurn your phone sideways (landscape) for the best view.";
            button = "Play Fullscreen";
        }
        else if (ios)
        {
            title = "Hide the Safari toolbar";
            body = "Tap aA in the address bar, then tap Hide Toolbar.\nFor no toolbar at all: tap Share, choose \"Add to Home Screen\", then open the game from the new icon.";
            button = "Continue";
        }
        else
        {
            title = "Add to Home Screen for Fullscreen";
            body = "This browser cannot hide the URL bar by itself.\nTap the Share button, choose \"Add to Home Screen\", then open the game from the new icon.";
            button = "Continue";
        }

        bool landscape = Screen.width >= Screen.height;
        bool guide = canFullscreen || ios;

        // where everything goes (fractions of the screen)
        Vector2 titleMin, titleMax, bodyMin, bodyMax, buttonMin, buttonMax, guideMin, guideMax;
        if (guide && landscape)
        {
            guideMin = new Vector2(0.03f, 0.14f); guideMax = new Vector2(0.52f, 0.9f);
            titleMin = new Vector2(0.55f, 0.68f); titleMax = new Vector2(0.97f, 0.9f);
            bodyMin = new Vector2(0.55f, 0.3f); bodyMax = new Vector2(0.97f, 0.68f);
            buttonMin = new Vector2(0.6f, 0.1f); buttonMax = new Vector2(0.94f, 0.27f);
        }
        else if (guide)
        {
            guideMin = new Vector2(0.04f, 0.5f); guideMax = new Vector2(0.96f, 0.8f);
            titleMin = new Vector2(0.06f, 0.8f); titleMax = new Vector2(0.94f, 0.92f);
            bodyMin = new Vector2(0.06f, 0.24f); bodyMax = new Vector2(0.94f, 0.5f);
            buttonMin = new Vector2(0.2f, 0.08f); buttonMax = new Vector2(0.8f, 0.2f);
        }
        else
        {
            guideMin = guideMax = Vector2.zero;
            titleMin = new Vector2(0.08f, 0.62f); titleMax = new Vector2(0.92f, 0.85f);
            bodyMin = new Vector2(0.08f, 0.34f); bodyMax = new Vector2(0.92f, 0.62f);
            buttonMin = new Vector2(0.3f, 0.12f); buttonMax = new Vector2(0.7f, 0.28f);
        }

        if (guide)
        {
            var area = MakeRect("Guide Area", panel, guideMin, guideMax);
            var holder = new GameObject("Guide", typeof(RectTransform));
            holder.transform.SetParent(area, false);
            var rt = (RectTransform)holder.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var animation = holder.AddComponent<FullscreenGuide>();
            animation.ios = !canFullscreen && ios;
            animation.fitTo = area;
        }

        MakeText("Title", panel, title, 64, FontStyles.Bold, titleMin, titleMax, guide && landscape ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
        MakeText("Body", panel, body, guide ? 36 : 40, FontStyles.Normal, bodyMin, bodyMax, guide && landscape ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center);

        var btnRect = MakeRect("Button", panel, buttonMin, buttonMax);
        var btnImage = btnRect.gameObject.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.65f, 0.35f, 1f);
        var btn = btnRect.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(Confirm);

        MakeText("Label", btnRect, button, 48, FontStyles.Bold, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void MakeText(string name, Transform parent, string text, float size,
        FontStyles style, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
    {
        var rt = MakeRect(name, parent, anchorMin, anchorMax);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.enableAutoSizing = true;        // a long text gets smaller instead of running out of its place
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.45f;
        t.fontStyle = style;
        t.color = Color.white;
        t.alignment = alignment;
        t.raycastTarget = false;
    }
}
