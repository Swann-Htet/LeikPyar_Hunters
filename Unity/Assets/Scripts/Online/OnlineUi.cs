using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Small pieces for the account and leaderboard screens, in the game's own style (dark plates, a red edge, bone text, DM Sans).
public static class OnlineUi
{
    public static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    public static readonly Color Blood = new Color(0.82f, 0.10f, 0.15f, 1f);
    public static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.97f);
    public static readonly Color Dim = new Color(0.92f, 0.88f, 0.80f, 0.5f);
    public static readonly Color Amber = new Color(0.94f, 0.72f, 0.40f, 1f);
    public static readonly Color Ok = new Color(0.31f, 0.81f, 0.48f, 1f);
    public static readonly Color Brown = new Color(0.055f, 0.034f, 0.022f, 1f);

    // A full-screen canvas with a dark veil that blocks the clicks behind it. 'panel' is the veil (switch it on and off).
    public static GameObject MakeScreen(Transform parent, string name, int order, float veilAlpha)
    {
        var canvasGo = new GameObject(name + "Canvas");
        canvasGo.transform.SetParent(parent, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();

        var veil = UiKit.NewRect(name, canvasGo.transform);
        UiKit.Stretch(veil);
        var image = veil.gameObject.AddComponent<Image>();
        image.color = new Color(0.01f, 0.007f, 0.005f, veilAlpha);
        image.raycastTarget = true;
        return veil.gameObject;
    }

    public static TMP_Text Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 box, bool heading = false, bool ignoreLocalize = false)
    {
        var font = heading ? (UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi) : UiKit.DmSemi;
        var t = UiKit.Label(parent, "Text", text, font, size, color, align, pos, box);
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        if (ignoreLocalize) t.gameObject.AddComponent<LocalizeIgnore>();
        return t;
    }

    // A button: 'primary' is red and filled, the other is a dark cut-corner plate with a red edge
    public static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityAction click, bool primary)
    {
        var plate = UiKit.Shape(parent, label + " Button", primary ? UiKit.Round : UiKit.Cut,
            primary ? new Color(0.62f, 0.08f, 0.12f, 1f) : new Color(0.05f, 0.04f, 0.05f, 0.94f), primary ? size.y * 0.5f : 16f, pos, size);
        plate.raycastTarget = true;
        var edge = UiKit.Shape(plate.transform, "Edge", primary ? UiKit.RoundOutline : UiKit.CutOutline, primary ? new Color(0.9f, 0.25f, 0.3f, 1f) : Blood,
            primary ? size.y * 0.5f : 16f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var text = UiKit.Label(plate.transform, "Label", label, UiKit.DmSemi, size.y * 0.4f, Bone, TextAlignmentOptions.Center, Vector2.zero, size);
        text.characterSpacing = 5f;
        text.raycastTarget = false;

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        if (click != null) button.onClick.AddListener(click);
        return button;
    }

    // An input line (a text field that works with the phone keyboard in the browser too)
    public static TMP_InputField MakeInput(Transform parent, string placeholder, bool password, int limit, Vector2 pos, Vector2 size)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.mobileKeyboardSupport = true;
#endif
        var plate = UiKit.Shape(parent, "Input", UiKit.Cut, new Color(0.03f, 0.02f, 0.015f, 0.97f), 14f, pos, size);
        plate.raycastTarget = true;
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, Edge, 14f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var viewport = UiKit.NewRect("Text Area", plate.transform);
        UiKit.Stretch(viewport);
        viewport.offsetMin = new Vector2(22f, 6f);
        viewport.offsetMax = new Vector2(-22f, -6f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var text = UiKit.Label(viewport, "Text", "", UiKit.DmMedium, size.y * 0.44f, Bone, TextAlignmentOptions.MidlineLeft, Vector2.zero, size);
        UiKit.Stretch(text.rectTransform);
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.gameObject.AddComponent<LocalizeIgnore>();

        var hint = UiKit.Label(viewport, "Placeholder", placeholder, UiKit.DmMedium, size.y * 0.44f, Dim, TextAlignmentOptions.MidlineLeft, Vector2.zero, size);
        UiKit.Stretch(hint.rectTransform);
        hint.raycastTarget = false;
        hint.enableWordWrapping = false;
        hint.gameObject.AddComponent<LocalizeIgnore>();

        var input = plate.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = plate;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.characterLimit = limit;
        input.caretColor = Bone;
        input.customCaretColor = true;
        input.selectionColor = new Color(Blood.r, Blood.g, Blood.b, 0.45f);
        input.asteriskChar = '*';
        return input;
    }

    // A plate with the game's HUD look
    public static Image Plate(Transform parent, string name, Vector2 pos, Vector2 size, Color color, bool edge)
    {
        var plate = UiKit.Shape(parent, name, UiKit.Cut, color, 20f, pos, size);
        plate.raycastTarget = false;
        if (edge)
        {
            var e = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, Edge, 20f, Vector2.zero, size);
            UiKit.Stretch(e.rectTransform);
        }
        return plate;
    }
}
