using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The look of the Settings menu: black and charcoal surfaces, blood-red accents, Cinzel / Inter text and Lucide icons.
// The shapes (angular panels, rounded pills, soft shadows, scratches) are drawn by code, so there are no picture files.
// The fonts and icons come from Assets/Resources (made by Tools/ui/build_ui_assets.py and Tools > Ghost > Setup Settings).
public static class UiKit
{
    // ---- palette
    public static readonly Color Bg = new Color32(0x05, 0x06, 0x09, 255);
    public static readonly Color Panel = new Color32(0x0A, 0x0C, 0x12, 255);
    public static readonly Color Panel2 = new Color32(0x0E, 0x13, 0x1C, 255);
    public static readonly Color Control = new Color32(0x11, 0x17, 0x22, 255);
    public static readonly Color Blood = new Color32(0xD1, 0x0F, 0x2F, 255);
    public static readonly Color Crimson = new Color32(0xFF, 0x24, 0x3F, 255);
    public static readonly Color DarkRed = new Color32(0x59, 0x10, 0x1C, 255);
    public static readonly Color Moon = new Color32(0x21, 0x37, 0x4B, 255);
    public static readonly Color Text = new Color32(0xEB, 0xE1, 0xCE, 255);
    public static readonly Color TextDim = new Color32(0x92, 0x98, 0xA4, 255);
    public static readonly Color Disabled = new Color32(0x41, 0x47, 0x53, 255);
    public static readonly Color Track = new Color32(0x26, 0x2B, 0x36, 255);
    public static readonly Color Steel = new Color32(0x3A, 0x41, 0x4F, 255);
    public static readonly Color IconColor = new Color32(0xE8, 0xE8, 0xE8, 255);

    public const float Border9 = 48f;   // the corner size of the drawn sprites (pixels)

    private static Sprite round, roundOutline, cut, cutOutline, glow, circle, grunge;
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, TMP_FontAsset> fonts = new Dictionary<string, TMP_FontAsset>();

    // ---------------------------------------------------------------- fonts

    public static TMP_FontAsset Body => Font("Inter Medium SDF");          // labels
    public static TMP_FontAsset Strong => Font("Inter SemiBold SDF");      // values, buttons
    public static TMP_FontAsset Heading => Font("Cinzel Bold SDF");        // SETTINGS
    public static TMP_FontAsset Numbers => Font("Rajdhani SemiBold SDF");  // FPS, battery
    public static TMP_FontAsset DmMedium => Font("DM Sans Medium SDF");    // the game HUD
    public static TMP_FontAsset DmSemi => Font("DM Sans SemiBold SDF");

    static TMP_FontAsset Font(string name)
    {
        if (fonts.TryGetValue(name, out TMP_FontAsset cached) && cached != null) return cached;

        var font = Resources.Load<TMP_FontAsset>("UIFonts/" + name);
        fonts[name] = font;
        return font;
    }

    // ---------------------------------------------------------------- icons (Lucide)

    public static Sprite Icon(string name)
    {
        if (icons.TryGetValue(name, out Sprite cached) && cached != null) return cached;

        var sprite = Resources.Load<Sprite>("UIIcons/" + name);
        icons[name] = sprite;
        return sprite;
    }

    // ---------------------------------------------------------------- drawn sprites

    // A white rounded rectangle (9-sliced): for small things such as switches and value chips
    public static Sprite Round => round != null ? round : round = MakeRound(false);
    public static Sprite RoundOutline => roundOutline != null ? roundOutline : roundOutline = MakeRound(true);

    // An angular panel: two big cut corners (top left, bottom right) and two small ones
    public static Sprite Cut => cut != null ? cut : cut = MakeCut(false);
    public static Sprite CutOutline => cutOutline != null ? cutOutline : cutOutline = MakeCut(true);

    // A soft glow / shadow (9-sliced): put it behind something, a little bigger than it
    public static Sprite Glow => glow != null ? glow : glow = MakeGlow();

    public static Sprite Circle => circle != null ? circle : circle = MakeCircle();

    // A thin white ring (the key circle of the interaction prompt, the border of the joystick)
    public static Sprite Ring => ring != null ? ring : ring = MakeRing();

    private static Sprite ring;

    static Sprite MakeRing()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color32[n * n];
        float half = n * 0.5f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - (half - 6f)) - 3f;
                pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        }
        tex.SetPixels32(pixels);
        return MakeSprite(tex, false);
    }

    // White at the top fading to nothing at the bottom: the light sheen of glass
    public static Sprite VerticalFade => fade != null ? fade : fade = MakeFade();

    private static Sprite fade;

    static Sprite MakeFade()
    {
        const int h = 128;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        var pixels = new Color32[4 * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);                         // y = 0 is the bottom
            float a = Mathf.Pow(t, 2.2f);
            for (int x = 0; x < 4; x++) pixels[y * 4 + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels32(pixels);
        return MakeSprite(tex, false);
    }

    // Scratches and worn metal (white, use it with a very low alpha)
    public static Sprite Grunge => grunge != null ? grunge : grunge = MakeGrunge();

    static float RoundDistance(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - (hx - r);
        float qy = Mathf.Abs(py) - (hy - r);
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // negative inside. y points up: the first cut is the top left corner, the second the top right, then bottom left, bottom right
    static float CutDistance(float px, float py, float hx, float hy, float tl, float tr, float bl, float br)
    {
        const float k = 0.70710678f;
        float d = Mathf.Max(Mathf.Abs(px) - hx, Mathf.Abs(py) - hy);

        d = Mathf.Max(d, (-px + py - (hx + hy - tl)) * k);
        d = Mathf.Max(d, (px + py - (hx + hy - tr)) * k);
        d = Mathf.Max(d, (-px - py - (hx + hy - bl)) * k);
        d = Mathf.Max(d, (px - py - (hx + hy - br)) * k);
        return d;
    }

    static Sprite MakeSprite(Texture2D tex, bool sliced)
    {
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();

        return sliced
            ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(Border9, Border9, Border9, Border9))
            : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Shade(float d, bool ring)
    {
        return ring ? Mathf.Clamp01(2f - Mathf.Abs(d + 1.5f)) : Mathf.Clamp01(0.5f - d);
    }

    static Sprite MakeRound(bool ring)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color32[n * n];
        float half = n * 0.5f;

        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                pixels[y * n + x] = new Color(1f, 1f, 1f, Shade(RoundDistance(x + 0.5f - half, y + 0.5f - half, half, half, Border9), ring));

        tex.SetPixels32(pixels);
        return MakeSprite(tex, true);
    }

    static Sprite MakeCut(bool ring)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color32[n * n];
        float half = n * 0.5f;

        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = CutDistance(x + 0.5f - half, y + 0.5f - half, half, half, 40f, 12f, 12f, 40f);
                pixels[y * n + x] = new Color(1f, 1f, 1f, Shade(d, ring));
            }

        tex.SetPixels32(pixels);
        return MakeSprite(tex, true);
    }

    static Sprite MakeGlow()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color32[n * n];
        float half = n * 0.5f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = RoundDistance(x + 0.5f - half, y + 0.5f - half, 40f, 40f, 32f);
                float a = d <= 0f ? 1f : Mathf.Exp(-(d / 11f) * (d / 11f));
                pixels[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(pixels);
        return MakeSprite(tex, true);
    }

    static Sprite MakeCircle()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color32[n * n];
        float half = n * 0.5f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float a = Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - (half - 1f)));
                pixels[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(pixels);
        return MakeSprite(tex, false);
    }

    // Worn, scratched metal: blotchy noise and a few hundred hair-thin scratches
    static Sprite MakeGrunge()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var alpha = new float[n * n];
        var rng = new System.Random(13);

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float blotch = Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 0.55f + Mathf.PerlinNoise(x * 0.05f + 40f, y * 0.05f) * 0.3f;
                float grain = (float)rng.NextDouble() * 0.25f;
                alpha[y * n + x] = Mathf.Clamp01(blotch * 0.35f + grain * 0.25f);
            }
        }

        for (int i = 0; i < 260; i++)
        {
            float x = (float)rng.NextDouble() * n;
            float y = (float)rng.NextDouble() * n;
            float angle = Mathf.Deg2Rad * (rng.Next(0, 2) == 0 ? rng.Next(15, 40) : rng.Next(110, 135));
            float length = rng.Next(30, 190);
            float strength = 0.35f + (float)rng.NextDouble() * 0.65f;

            for (int s = 0; s < length; s++)
            {
                int px = Mathf.RoundToInt(x + Mathf.Cos(angle) * s);
                int py = Mathf.RoundToInt(y + Mathf.Sin(angle) * s);
                if (px < 0 || py < 0 || px >= n || py >= n) break;

                float fade = 1f - s / length;
                alpha[py * n + px] = Mathf.Max(alpha[py * n + px], strength * fade);
            }
        }

        var pixels = new Color32[n * n];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, alpha[i]);
        tex.SetPixels32(pixels);
        return MakeSprite(tex, false);
    }

    // ---------------------------------------------------------------- building blocks

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    // A shape. 'radius' = the corner size in canvas units (for a pill: half the height)
    public static Image Shape(Transform parent, string name, Sprite sprite, Color color, float radius, Vector2 position, Vector2 size)
    {
        var rt = NewRect(name, parent);
        Place(rt, position, size);

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = Border9 / Mathf.Max(1f, radius);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // A soft glow or shadow BEHIND a shape (a sibling placed under it), 'margin' bigger on every side.
    // It is not a child, so it never tints the shape itself. Use black for a shadow, red for light spill
    public static Image GlowBehind(Image shape, Color color, float margin, Vector2 offset)
    {
        var source = shape.rectTransform;
        var rt = NewRect("Glow", source.parent);
        Place(rt, source.anchoredPosition + offset, source.sizeDelta + new Vector2(margin * 2f, margin * 2f));
        rt.SetSiblingIndex(source.GetSiblingIndex());

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = Glow;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // A glow drawn over a shape, spilling past its edges (good for thin bars such as the slider fill)
    public static Image AddGlow(Transform shape, Color color, float margin)
    {
        var rt = NewRect("Glow", shape);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-margin, -margin);
        rt.offsetMax = new Vector2(margin, margin);

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = Glow;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Image IconImage(Transform parent, string name, string iconName, Color color, Vector2 position, float size)
    {
        var rt = NewRect(name, parent);
        Place(rt, position, new Vector2(size, size));

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = Icon(iconName);
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        image.enabled = image.sprite != null;
        return image;
    }

    public static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions alignment, Vector2 position, Vector2 box)
    {
        var rt = NewRect(name, parent);
        Place(rt, position, box);

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
