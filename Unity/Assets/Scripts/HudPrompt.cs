using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The interaction prompt of the HUD:  ( E )  Open Door
// Every key written in [brackets] in the text becomes a key circle wherever it stands:
//   "[E] Take back"                    ->  (E) Take back
//   "Put MEP in this cage   [F]"       ->  Put MEP in this cage (F)
//   "Hold [Left Click] to pull ghost"  ->  Hold (Left Click) to pull ghost
// Only the key has a dark see-through background. The words have none, only a soft shadow, in bone-white DM Sans.
// It is low on the screen (not in the middle, no line to the door).
// Everything that wants to show a prompt (doors, gun, generator, inventory, baby ghosts) calls HudPrompt.Show(text)
// every frame it should be visible. A text can have several lines.
public class HudPrompt : MonoBehaviour
{
    private const int MaxRows = 4;
    private const float BottomOffset = 230f;   // canvas units above the bottom of the screen (1080 high)

    private static readonly Regex KeyRegex = new Regex(@"\[([^\[\]]{1,14})\]");
    private static readonly Color Bone = new Color(0.93f, 0.89f, 0.82f, 0.97f);

    // "Left click: drop Cage" is written like "[Left Click] drop Cage", so the click gets its key circle too
    private static readonly Regex ClickRegex = new Regex(@"^(Left|Right)\s+click\s*:\s*", RegexOptions.IgnoreCase);
    private static readonly Regex ClickBurmeseRegex = new Regex(@"^ဘယ်ဘက်ကလစ်\s*:\s*");

    private static HudPrompt instance;
    private static string message;
    private static float requestTime = -10f;

    // one piece of a row: a key circle or some words
    private class Seg
    {
        public GameObject root;
        public bool isKey;
        public LayoutElement layout;
        public Image ring;
        public Image disc;
        public TMP_Text key;
        public TMP_Text text;
        public TMP_Text shadow;
    }

    private class Row
    {
        public GameObject root;
        public readonly List<Seg> segments = new List<Seg>();
    }

    private readonly Row[] rows = new Row[MaxRows];
    private CanvasGroup group;
    private GameObject container;
    private string shownMessage;
    private bool shownBurmese;

    // True: the HUD draws the prompts (the old centre texts stay hidden)
    public static bool Drawing => true;

    public static void Show(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (instance == null)
            instance = new GameObject("HudPrompt").AddComponent<HudPrompt>();

        message = text;
        requestTime = Time.unscaledTime;
    }

    void Awake()
    {
        instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        bool show = Time.unscaledTime - requestTime < 0.12f && !SettingsPanel.IsOpen;

        float goal = show ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, goal, Time.unscaledDeltaTime * 10f);
        container.SetActive(show || group.alpha > 0.01f);

        if (show && (message != shownMessage || Loc.IsBurmese != shownBurmese))
        {
            shownMessage = message;
            shownBurmese = Loc.IsBurmese;
            Apply(message);
        }
    }

    // ---------------------------------------------------------------- content

    void Apply(string text)
    {
        string[] lines = text.Split('\n');

        for (int i = 0; i < MaxRows; i++)
        {
            var row = rows[i];
            bool used = i < lines.Length && lines[i].Trim().Length > 0;
            row.root.SetActive(used);
            if (!used) continue;

            string line = lines[i].Trim();

            // Burmese: the whole line is translated first, then the keys are found in it
            bool translated = false;
            if (Loc.IsBurmese)
            {
                string burmese = Loc.Translate(line);
                translated = burmese != line;
                line = burmese;
            }

            FillRow(row, line, translated);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)container.transform);
    }

    void FillRow(Row row, string line, bool translated)
    {
        foreach (var seg in row.segments) seg.root.SetActive(false);

        line = ClickRegex.Replace(line, m => "[" + (m.Groups[1].Value.ToLowerInvariant() == "left" ? "Left" : "Right") + " Click] ");
        line = ClickBurmeseRegex.Replace(line, "[ဘယ်ဘက်ကလစ်] ");

        // cut the line into words and keys
        var parts = new List<KeyValuePair<bool, string>>();
        int last = 0;
        foreach (Match m in KeyRegex.Matches(line))
        {
            string before = line.Substring(last, m.Index - last).Trim();
            if (before.Length > 0) parts.Add(new KeyValuePair<bool, string>(false, before));
            parts.Add(new KeyValuePair<bool, string>(true, m.Groups[1].Value));
            last = m.Index + m.Length;
        }
        string tail = line.Substring(last).Trim();
        if (tail.Length > 0) parts.Add(new KeyValuePair<bool, string>(false, tail));

        // A phone has no keys: the words stay, the key circles ([E], [F], [Left Click]) go
        if (DeviceDetection.IsMobile()) parts.RemoveAll(p => p.Key);

        var font = translated && Loc.BurmeseFont != null ? Loc.BurmeseFont : UiKit.DmMedium;
        int keys = 0, words = 0;

        for (int i = 0; i < parts.Count; i++)
        {
            bool isKey = parts[i].Key;
            Seg seg = TakeSegment(row, isKey, isKey ? keys++ : words++);
            seg.root.SetActive(true);
            seg.root.transform.SetSiblingIndex(i);

            if (isKey)
            {
                string key = parts[i].Value;
                bool wide = key.Length > 2;

                seg.key.text = key;

                // a key written in Burmese needs the Burmese font
                bool myanmar = false;
                foreach (char c in key) if (c > 0x7f) myanmar = true;
                if (myanmar && Loc.BurmeseFont != null) seg.key.font = Loc.BurmeseFont;
                else if (UiKit.DmSemi != null) seg.key.font = UiKit.DmSemi;
                seg.ring.sprite = wide ? UiKit.RoundOutline : UiKit.Ring;
                seg.ring.type = wide ? Image.Type.Sliced : Image.Type.Simple;
                seg.ring.pixelsPerUnitMultiplier = wide ? UiKit.Border9 / 23f : 1f;
                seg.disc.sprite = wide ? UiKit.Round : UiKit.Circle;
                seg.disc.type = wide ? Image.Type.Sliced : Image.Type.Simple;
                seg.disc.pixelsPerUnitMultiplier = wide ? UiKit.Border9 / 20f : 1f;
                seg.layout.preferredWidth = wide ? Mathf.Max(70f, seg.key.GetPreferredValues(key).x + 36f) : 46f;
            }
            else
            {
                seg.text.text = parts[i].Value;
                seg.shadow.text = parts[i].Value;
                if (font != null)
                {
                    seg.text.font = font;
                    seg.shadow.font = font;
                }

                // Burmese syllables are big: a little smaller (the player's Text Size on top), no extra letter spacing
                bool burmeseWords = translated && Loc.BurmeseFont != null;
                float wordSize = burmeseWords ? 31f * 0.92f * Loc.TextSize : 31f;
                seg.text.fontSize = wordSize;
                seg.shadow.fontSize = wordSize;
            }
        }
    }

    // A piece of the right kind: an old one is used again, or a new one is made
    Seg TakeSegment(Row row, bool isKey, int index)
    {
        int found = -1;
        foreach (var seg in row.segments)
        {
            if (seg.isKey != isKey) continue;
            found++;
            if (found == index) return seg;
        }

        var made = isKey ? MakeKey(row.root.transform) : MakeWords(row.root.transform);
        row.segments.Add(made);
        return made;
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        var canvasGo = new GameObject("HudPromptCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<HorrorSkinIgnore>();

        var box = UiKit.NewRect("Prompts", canvasGo.transform);
        container = box.gameObject;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.anchoredPosition = new Vector2(0f, BottomOffset);

        group = container.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        var column = container.AddComponent<VerticalLayoutGroup>();
        column.childAlignment = TextAnchor.UpperCenter;
        column.spacing = 10f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;

        var fitter = container.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < MaxRows; i++) rows[i] = BuildRow(box);
        container.SetActive(false);
    }

    Row BuildRow(Transform parent)
    {
        var row = new Row();

        // no box behind the row: only the keys have a background
        var rt = UiKit.NewRect("Row", parent);
        row.root = rt.gameObject;

        var h = row.root.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 14f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var fit = row.root.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return row;
    }

    // The key: a thin bone ring, a dark see-through disc, and the letter
    Seg MakeKey(Transform parent)
    {
        var seg = new Seg { isKey = true };

        var badge = UiKit.NewRect("Key", parent);
        seg.root = badge.gameObject;
        seg.ring = seg.root.AddComponent<Image>();
        seg.ring.sprite = UiKit.Ring;
        seg.ring.color = new Color(0.92f, 0.88f, 0.8f, 0.9f);
        seg.ring.raycastTarget = false;
        seg.layout = seg.root.AddComponent<LayoutElement>();
        seg.layout.preferredWidth = 46f;
        seg.layout.preferredHeight = 46f;

        var disc = UiKit.NewRect("Disc", badge);
        UiKit.Stretch(disc);
        disc.offsetMin = new Vector2(3f, 3f);
        disc.offsetMax = new Vector2(-3f, -3f);
        seg.disc = disc.gameObject.AddComponent<Image>();
        seg.disc.sprite = UiKit.Circle;
        seg.disc.color = new Color(0f, 0f, 0f, 0.5f);
        seg.disc.raycastTarget = false;
        disc.SetAsFirstSibling();

        var key = UiKit.NewRect("Letter", badge);
        UiKit.Stretch(key);
        seg.key = key.gameObject.AddComponent<TextMeshProUGUI>();
        if (UiKit.DmSemi != null) seg.key.font = UiKit.DmSemi;
        seg.key.fontSize = 26f;
        seg.key.alignment = TextAlignmentOptions.Center;
        seg.key.color = new Color(0.95f, 0.92f, 0.86f, 1f);
        seg.key.raycastTarget = false;
        seg.key.overflowMode = TextOverflowModes.Overflow;
        key.gameObject.AddComponent<LocalizeIgnore>();
        return seg;
    }

    // The words: a dark copy underneath gives the shadow (the copy also decides the size in the layout)
    Seg MakeWords(Transform parent)
    {
        var seg = new Seg { isKey = false };

        var shadowRt = UiKit.NewRect("Words", parent);
        seg.root = shadowRt.gameObject;
        seg.shadow = seg.root.AddComponent<TextMeshProUGUI>();
        Style(seg.shadow, new Color(0f, 0f, 0f, 0.7f));
        seg.root.AddComponent<LocalizeIgnore>();

        var label = UiKit.NewRect("Text", shadowRt);
        UiKit.Stretch(label);
        label.offsetMin = new Vector2(-2f, 2f);
        label.offsetMax = new Vector2(-2f, 2f);
        seg.text = label.gameObject.AddComponent<TextMeshProUGUI>();
        Style(seg.text, Bone);
        label.gameObject.AddComponent<LocalizeIgnore>();
        return seg;
    }

    static void Style(TMP_Text t, Color color)
    {
        if (UiKit.DmMedium != null) t.font = UiKit.DmMedium;
        t.fontSize = 31f;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = color;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
    }
}
