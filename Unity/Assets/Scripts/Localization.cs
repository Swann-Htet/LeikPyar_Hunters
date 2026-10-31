using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// English / Burmese.
//
// TextMeshPro can not shape Myanmar script on its own, so the Burmese texts are prepared by
// Tools/burmese/build_burmese.py (HarfBuzz + Noto Serif Myanmar): every syllable becomes one glyph of the font
// "BurmeseClusters" and the game only swaps English text for the prepared text. Nothing is shaped at runtime.
//
// How it works in the game: LocalizeRunner looks at every TMP text. When the language is Burmese and the text
// is one it knows (exact text, a sentence with {0} parts, a clock, or lines/rich text made of known pieces),
// it shows the Burmese version with the Burmese font. Other texts stay English.
// To add a sentence: add it to Tools/burmese/translations.json and run build_burmese.py again.
public static class Loc
{
    public enum Language { English, Burmese }

    private const string PrefKey = "Language";
    private const string FontResource = "Burmese/BurmeseFont SDF";
    private const string MapResource = "Burmese/BurmeseMap";

    public static event Action Changed;

    private class Template
    {
        public Regex regex;
        public string burmese;
        public int literalLength;
    }

    [Serializable] private class MapFile { public MapEntry[] entries; }
    [Serializable] private class MapEntry { public string k; public string v; }

    private static Language current;
    private static bool languageLoaded;
    private static bool mapLoaded;
    private static TMP_FontAsset burmeseFont;
    private static bool fontLoaded;

    private static readonly Dictionary<string, string> exact = new Dictionary<string, string>();
    private static readonly List<Template> templates = new List<Template>();
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

    private static readonly Regex PlaceholderRegex = new Regex(@"\{(\d+)\}");
    private static readonly Regex ClockRegex = new Regex(@"^(\d{1,2}):(\d{2})\s?(AM|PM)$", RegexOptions.IgnoreCase);
    private static readonly Regex SplitRegex = new Regex(@"(<[^>]*>|\n)");

    public static Language Current
    {
        get
        {
            if (!languageLoaded)
            {
                languageLoaded = true;
                current = PlayerPrefs.GetString(PrefKey, "en") == "my" ? Language.Burmese : Language.English;
            }
            return current;
        }
    }

    public static bool IsBurmese => Current == Language.Burmese;

    // The size of the Burmese text, chosen by the player (Settings > Text Size, or A- / A+ in the intro menu)
    public static readonly float[] TextSizes = { 0.7f, 0.85f, 1f, 1.15f, 1.3f };
    private const string SizePrefKey = "TextSize";
    private static int sizeIndex = -1;

    public static int TextSizeIndex
    {
        get
        {
            if (sizeIndex < 0) sizeIndex = Mathf.Clamp(PlayerPrefs.GetInt(SizePrefKey, 2), 0, TextSizes.Length - 1);
            return sizeIndex;
        }
    }

    public static float TextSize => TextSizes[TextSizeIndex];

    public static void StepTextSize(int step)
    {
        int next = Mathf.Clamp(TextSizeIndex + step, 0, TextSizes.Length - 1);
        if (next == sizeIndex) return;

        sizeIndex = next;
        PlayerPrefs.SetInt(SizePrefKey, next);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void Set(Language language)
    {
        if (Current == language) return;

        current = language;
        Debug.Log("[Loc] Language: " + language);
        PlayerPrefs.SetString(PrefKey, language == Language.Burmese ? "my" : "en");
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static TMP_FontAsset BurmeseFont
    {
        get
        {
            if (!fontLoaded)
            {
                fontLoaded = true;
                burmeseFont = Resources.Load<TMP_FontAsset>(FontResource);
                if (burmeseFont == null)
                    Debug.LogWarning("[Loc] The Burmese font asset is missing. Run Tools > Ghost > Setup Burmese Language.");
            }
            return burmeseFont;
        }
    }

    // A prepared Burmese text by its key (e.g. "@language_name"), or "" if there is none
    public static string Raw(string key)
    {
        LoadMap();
        return exact.TryGetValue(key, out string value) ? value : "";
    }

    // The Burmese version of an English text. Unknown texts come back unchanged
    public static string Translate(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        LoadMap();
        if (cache.TryGetValue(source, out string cached)) return cached;

        string result = TranslateCore(source);
        if (result == null && (source.IndexOf('<') >= 0 || source.IndexOf('\n') >= 0))
            result = TranslateSegments(source);
        if (result == null) result = source;

        if (cache.Count > 3000) cache.Clear();
        cache[source] = result;
        return result;
    }

    // Exact text, a clock, or a sentence with {0} parts. Null = not known
    private static string TranslateCore(string text)
    {
        if (exact.TryGetValue(text, out string value)) return value;

        Match clock = ClockRegex.Match(text);
        if (clock.Success) return Clock(clock);

        foreach (var t in templates)
        {
            Match m = t.regex.Match(text);
            if (!m.Success) continue;

            return PlaceholderRegex.Replace(t.burmese, p =>
            {
                Group g = m.Groups["g" + p.Groups[1].Value];
                return g.Success ? Translate(g.Value) : p.Value;
            });
        }
        return null;
    }

    // Lines and rich text: every known piece is translated, tags and line breaks stay
    private static string TranslateSegments(string source)
    {
        string[] parts = SplitRegex.Split(source);
        var sb = new StringBuilder();
        bool any = false;

        foreach (string part in parts)
        {
            if (part.Length == 0) continue;

            bool isTag = part[0] == '<' && part[part.Length - 1] == '>';
            if (part == "\n" || isTag) { sb.Append(part); continue; }

            string trimmed = part.Trim();
            string translated = trimmed.Length > 0 ? TranslateCore(trimmed) : null;
            if (translated == null) { sb.Append(part); continue; }

            any = true;
            sb.Append(part, 0, part.Length - part.TrimStart().Length);
            sb.Append(translated);
            sb.Append(part, part.TrimEnd().Length, part.Length - part.TrimEnd().Length);
        }
        return any ? sb.ToString() : null;
    }

    // "08:54 PM" -> the time of day in Burmese + the same numbers
    private static string Clock(Match m)
    {
        int hour = int.Parse(m.Groups[1].Value);
        bool pm = string.Equals(m.Groups[3].Value, "PM", StringComparison.OrdinalIgnoreCase);
        int hour24 = pm ? (hour == 12 ? 12 : hour + 12) : (hour == 12 ? 0 : hour);

        string key = hour24 >= 5 && hour24 < 12 ? "@period_morning"
                   : hour24 >= 12 && hour24 < 16 ? "@period_noon"
                   : hour24 >= 16 && hour24 < 19 ? "@period_evening"
                   : "@period_night";

        return Raw(key) + " " + m.Groups[1].Value + ":" + m.Groups[2].Value;
    }

    private static void LoadMap()
    {
        if (mapLoaded) return;
        mapLoaded = true;

        var asset = Resources.Load<TextAsset>(MapResource);
        if (asset == null)
        {
            Debug.LogWarning("[Loc] Burmese texts are missing (Resources/" + MapResource + "). Run Tools/burmese/build_burmese.py.");
            return;
        }

        var map = JsonUtility.FromJson<MapFile>(asset.text);
        if (map == null || map.entries == null) return;

        foreach (var entry in map.entries)
        {
            if (string.IsNullOrEmpty(entry.k)) continue;

            if (!entry.k.StartsWith("@") && PlaceholderRegex.IsMatch(entry.k))
                templates.Add(MakeTemplate(entry.k, entry.v));
            else
                exact[entry.k] = entry.v;
        }

        // The sentence with the most fixed text first, so "{0} +{1}% > {2}% LEVEL {3}!" wins over "{0} +{1}% > {2}%"
        templates.Sort((a, b) => b.literalLength.CompareTo(a.literalLength));
    }

    private static Template MakeTemplate(string key, string burmese)
    {
        var sb = new StringBuilder("^");
        int last = 0;
        int literal = 0;

        foreach (Match m in PlaceholderRegex.Matches(key))
        {
            string text = key.Substring(last, m.Index - last);
            literal += text.Length;
            sb.Append(Regex.Escape(text));
            sb.Append("(?<g").Append(m.Groups[1].Value).Append(">.+?)");
            last = m.Index + m.Length;
        }

        string tail = key.Substring(last);
        literal += tail.Length;
        sb.Append(Regex.Escape(tail)).Append("$");

        return new Template
        {
            regex = new Regex(sb.ToString(), RegexOptions.Singleline),
            burmese = burmese,
            literalLength = literal
        };
    }
}
