using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HorrorSkin : MonoBehaviour
{
    [Tooltip("A dark red vignette over the whole screen. Off by default: it made the game too dark. The intro menu has its own")]
    public bool addVignette = false;
    [Range(0f, 1f)] public float vignetteStrength = 0.75f;

    private static readonly string[] SkipNames =
        { "Icon", "Fill", "Bar", "Handle", "Letter", "LoadingImage", "HorrorVignette", "Joystick", "Stick" };

    void Start()
    {
        Apply();

        // UI that other scripts build a moment later is skinned too
        StartCoroutine(Again(0.6f));
        StartCoroutine(Again(2.5f));
    }

    IEnumerator Again(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Apply();
    }

    public void Apply()
    {
        var canvas = GetComponent<Canvas>();

        foreach (var g in GetComponentsInChildren<Graphic>(true))
        {
            // An invisible click area (alpha 0) gets no clicks unless its mesh is not culled
            if (g is Image clickArea && clickArea.color.a < 0.05f && clickArea.GetComponent<Button>() != null)
                clickArea.canvasRenderer.cullTransparentMesh = false;

            if (g == null || g.GetComponentInParent<HorrorSkinIgnore>(true) != null) continue;
            if (g.GetComponent<HorrorStyled>() != null) continue;

            // Joysticks and the like keep their own look
            if (g.GetComponentInParent<MobileJoystickUI>(true) != null) continue;

            if (g is TMP_Text text)
                SkinText(text);
            else if (g is Image image)
                SkinImage(image);
        }

        foreach (var slider in GetComponentsInChildren<Slider>(true))
            SkinSlider(slider);

        if (addVignette && canvas != null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            EnsureVignette();
    }

    static bool IsPlain(Image image)
    {
        return image.sprite == null || image.sprite.name == "UISprite" || image.sprite.name == "Background";
    }

    static bool Skipped(Graphic g)
    {
        foreach (var n in SkipNames)
            if (g.name == n) return true;
        return false;
    }

    void SkinImage(Image image)
    {
        if (!image.enabled || Skipped(image) || !IsPlain(image)) return;
        if (image.GetComponentInParent<Slider>(true) != null || image.GetComponentInParent<Scrollbar>(true) != null) return;
        if (image.GetComponent<Mask>() != null) return;

        // A see-through click area (such as the menu entries) is left as it is
        if (image.color.a < 0.05f || image.GetComponentInParent<HorrorMenuItem>(true) != null) return;

        var button = image.GetComponent<Button>();
        var rect = image.rectTransform.rect;

        if (button != null)
        {
            StyleButton(button, image);
            return;
        }

        // A panel: big enough, not nearly invisible, not a thin bar
        bool big = rect.width >= 150f && rect.height >= 60f;
        if (!big || image.color.a < 0.3f) return;

        image.sprite = HorrorUI.Panel;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp(image.color.a, 0.9f, 1f));
        image.gameObject.AddComponent<HorrorStyled>();
    }

    void StyleButton(Button button, Image image)
    {
        image.sprite = HorrorUI.Button;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        var colors = button.colors;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.highlightedColor = new Color(1f, 0.72f, 0.72f, 1f);
        colors.pressedColor = new Color(0.5f, 0.4f, 0.4f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (button.targetGraphic == null) button.targetGraphic = image;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = HorrorUI.Bone;
            label.characterSpacing = Mathf.Max(label.characterSpacing, 4f);
            label.gameObject.AddComponent<HorrorStyled>();
        }

        // Your own button animations are left alone
        if (button.GetComponent<Animator>() == null && button.GetComponent<HorrorButtonFx>() == null)
        {
            var fx = button.gameObject.AddComponent<HorrorButtonFx>();
            fx.label = label;
        }

        image.gameObject.AddComponent<HorrorStyled>();
    }

    void SkinText(TMP_Text text)
    {
        // Only plain white text becomes bone white: colors you picked stay
        var c = text.color;
        if (c.r > 0.9f && c.g > 0.9f && c.b > 0.9f && c.a > 0.5f)
            text.color = new Color(HorrorUI.Bone.r, HorrorUI.Bone.g, HorrorUI.Bone.b, c.a);

        text.gameObject.AddComponent<HorrorStyled>();
    }

    void SkinSlider(Slider slider)
    {
        if (slider.GetComponent<HorrorStyled>() != null || slider.GetComponentInParent<HorrorSkinIgnore>(true) != null) return;
        slider.gameObject.AddComponent<HorrorStyled>();

        if (slider.fillRect != null)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            if (fill != null && IsPlain(fill)) fill.color = HorrorUI.BrightBlood;
        }

        var background = slider.transform.Find("Background");
        if (background != null)
        {
            var bg = background.GetComponent<Image>();
            if (bg != null && IsPlain(bg))
            {
                bg.color = new Color(0.07f, 0.03f, 0.04f, 0.9f);
                if (bg.GetComponent<Outline>() == null)
                {
                    var outline = bg.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.7f, 0.08f, 0.12f, 0.95f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
            }
        }

        if (slider.handleRect != null)
        {
            var handle = slider.handleRect.GetComponent<Image>();
            if (handle != null && IsPlain(handle)) handle.color = HorrorUI.Bone;
        }
    }

    void EnsureVignette()
    {
        var existing = transform.Find("HorrorVignette");
        if (existing != null) return;

        var go = new GameObject("HorrorVignette", typeof(RectTransform), typeof(Image), typeof(HorrorVignetteFx), typeof(HorrorStyled));
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = HorrorUI.Vignette;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, vignetteStrength);

        go.GetComponent<HorrorVignetteFx>().strength = vignetteStrength;
    }
}
