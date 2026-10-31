using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A dark, bloody look for every canvas. Nothing to set up: HorrorBootstrap adds a HorrorSkin to each canvas
// when a scene loads. It only skins PLAIN UI (no sprite): your own drawn pictures are never touched.
// To keep one object as it is, add the component HorrorSkinIgnore to it (or to its parent).
public static class HorrorUI
{
    public static readonly Color Bone = new Color(0.92f, 0.87f, 0.79f, 1f);
    public static readonly Color Blood = new Color(0.69f, 0.07f, 0.12f, 1f);
    public static readonly Color BrightBlood = new Color(0.9f, 0.14f, 0.23f, 1f);
    public static readonly Color Ink = new Color(0.04f, 0.025f, 0.03f, 0.94f);

    private static Sprite panel;
    private static Sprite vignette;
    private static Sprite button;

    // A 9-sliced panel: near-black with grain, a double blood-red border and a red glow inside the edge
    public static Sprite Panel
    {
        get
        {
            if (panel == null) panel = MakePanel();
            return panel;
        }
    }

    // A flat, slim button: dark glass with a thin blood-red line at the bottom and a red notch on the left
    public static Sprite Button
    {
        get
        {
            if (button == null)
            {
                var tex = ButtonTexture();
                button = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(ButtonBorder, ButtonBorder, ButtonBorder, ButtonBorder));
            }
            return button;
        }
    }

    public const int ButtonBorder = 6;

    public static Texture2D ButtonTexture()
    {
        const int size = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = new Color(0.04f, 0.02f, 0.03f, 0.72f);

                if (y < 2) c = new Color(0.75f, 0.1f, 0.15f, 1f);                       // bottom line
                else if (x < 3) c = new Color(0.9f, 0.14f, 0.23f, 1f);                  // left notch

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // Transparent in the middle, dark red-black at the edges
    public static Sprite Vignette
    {
        get
        {
            if (vignette == null) vignette = MakeVignette();
            return vignette;
        }
    }

    public const int PanelBorder = 12;

    static Sprite MakePanel()
    {
        var tex = PanelTexture();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder));
    }

    // The panel picture (also saved as a PNG by the Horror Intro Menu tool)
    public static Texture2D PanelTexture()
    {
        const int size = 48;
        const int border = PanelBorder;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var rng = new System.Random(7);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));

                // dark body, a little lighter at the top, with grain
                float shade = Mathf.Lerp(0.03f, 0.11f, y / (float)size);
                float grain = (float)(rng.NextDouble() - 0.5) * 0.035f;
                float noise = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.04f;
                Color c = new Color(shade + noise + grain, (shade + noise + grain) * 0.45f, (shade + noise + grain) * 0.5f, 0.96f);

                if (d < 3) c = new Color(0.62f + (d == 0 ? -0.2f : 0f), 0.05f, 0.09f, 1f);                       // blood border
                else if (d == 3) c = new Color(0.02f, 0.01f, 0.012f, 1f);                                          // black gap
                else if (d == 4) c = new Color(0.36f, 0.04f, 0.07f, 1f);                                           // thin inner line
                else if (d < border)
                {
                    float glow = 1f - (d - 5f) / (border - 5f);                                                    // red glow fading inward
                    c.r += 0.22f * glow * glow;
                    c.g += 0.01f * glow;
                }

                // chipped corners
                if (x + y < 4 || (size - 1 - x) + y < 4 || x + (size - 1 - y) < 4 || (size - 1 - x) + (size - 1 - y) < 4)
                    c.a = 0f;

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    static Sprite MakeVignette()
    {
        var tex = VignetteTexture();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Texture2D VignetteTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - 0.5f) * 2f;
                float dy = (y / (size - 1f) - 0.5f) * 2f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.2f);

                float a = Mathf.SmoothStep(0.3f, 1f, r);
                a = Mathf.Pow(a, 1.4f) * 0.8f;

                tex.SetPixel(x, y, new Color(0.12f * r, 0f, 0.015f * r, a));
            }
        }
        tex.Apply();
        return tex;
    }
}

// Adds a HorrorSkin to every canvas when a scene loads
public static class HorrorBootstrap
{
    private static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) => AddSkins();
        }
        AddSkins();
    }

    static void AddSkins()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.GetComponent<HorrorSkin>() == null)
                canvas.gameObject.AddComponent<HorrorSkin>();
        }
    }
}
