using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A picture button and the game's mark.
//   - the camera button (key C on a computer): takes a picture of the screen. In the browser the phone's share sheet opens (or the
//     picture is downloaded); on a computer the picture is saved in Pictures/Leikpyar Hunters.
//   - the mark (the logo, "LEIKPYAR HUNTERS" and the web address, bottom right) is not on the screen while you play: it is put on
//     the picture only at the moment the picture is taken.
// It makes itself in every scene that has a player. The logo is Resources/Brand/logo.png.
public class ScreenshotShare : MonoBehaviour
{
    public const string SiteName = "LEIKPYAR HUNTERS";
    public const string SiteAddress = "leikpyarhunters.org";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void LH_ShareImage(byte[] data, int size, string name, string text, string url);
#endif

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.72f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.86f, 0.12f, 0.16f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    private GameObject watermark, chip, saveChip;
    private Image chipEdge;
    private float startedAt;
    private bool placed;
    private bool busy;
    private bool phone;
    private string toast;
    private float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoCreate();
    }

    static void AutoCreate()
    {
        if (FindFirstObjectByType<PlayerController>() == null || FindFirstObjectByType<ScreenshotShare>() != null) return;
        new GameObject("Screenshot Share (auto)").AddComponent<ScreenshotShare>();
    }

    // The logo as a sprite (a plain picture file in Resources/Brand: it needs no import setting)
    static Sprite LoadLogo()
    {
        var texture = Resources.Load<Texture2D>("Brand/logo");
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    void Start()
    {
        phone = DeviceDetection.IsMobile();
        startedAt = Time.unscaledTime;

        var canvasGo = new GameObject("ScreenshotCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 66;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();

        BuildWatermark(canvasGo.transform);
        BuildButton(canvasGo.transform);
        BuildSaveButton(canvasGo.transform);
    }

    // ---------------------------------------------------------------- the watermark

    void BuildWatermark(Transform parent)
    {
        var root = UiKit.NewRect("Watermark", parent);
        root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-34f, 26f);
        watermark = root.gameObject;

        var row = watermark.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 12f;
        row.childAlignment = TextAnchor.MiddleRight;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var fit = watermark.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // the logo
        var logoRt = UiKit.NewRect("Logo", root);
        var logo = logoRt.gameObject.AddComponent<Image>();
        logo.sprite = LoadLogo();
        logo.enabled = logo.sprite != null;
        logo.preserveAspect = true;
        logo.color = new Color(1f, 1f, 1f, 0.8f);
        logo.raycastTarget = false;
        var logoSize = logoRt.gameObject.AddComponent<LayoutElement>();
        logoSize.preferredWidth = logoSize.preferredHeight = 62f;

        // the name and the address
        var lines = UiKit.NewRect("Lines", root);
        var column = lines.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 0f;
        column.childAlignment = TextAnchor.MiddleRight;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        var name = FlowLabel(lines, "Name", SiteName, UiKit.DmSemi, 24f, new Color(Bone.r, Bone.g, Bone.b, 0.78f));
        name.characterSpacing = 4f;
        FlowLabel(lines, "Address", SiteAddress, UiKit.DmMedium, 19f, new Color(Blood.r, 0.45f, 0.45f, 0.85f));

        // the mark shows only in the pictures
        watermark.SetActive(false);
    }

    static TMP_Text FlowLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color)
    {
        var t = UiKit.Label(parent, name, text, font, size, color, TextAlignmentOptions.Right, Vector2.zero, new Vector2(100f, 30f));
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.gameObject.AddComponent<LocalizeIgnore>();
        return t;
    }

    // ---------------------------------------------------------------- the button

    void BuildButton(Transform parent)
    {
        float size = phone ? 64f : 72f;

        var plate = UiKit.Shape(parent, "Picture Button", UiKit.Round, Plate, 14f, Vector2.zero, new Vector2(size, size));
        plate.raycastTarget = true;
        var rt = plate.rectTransform;
        if (phone)
        {
            // top right, in the row of the guide (?) and settings buttons (it is moved next to them in PlaceOnPhone):
            // the other corners and edges are full of the game's buttons
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-300f, -160f);
        }
        else
        {
            // bottom right corner
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-34f, 36f);
        }
        chip = plate.gameObject;

        chipEdge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 14f, Vector2.zero, new Vector2(size, size));
        UiKit.Stretch(chipEdge.rectTransform);
        UiKit.IconImage(plate.transform, "Icon", "camera", new Color(Bone.r, Bone.g, Bone.b, 0.85f), Vector2.zero, size * 0.5f);
        if (DeviceDetection.KeyHints) HudKeys.CornerKey(plate.transform, "C", size * 0.38f, new Vector2(4f, 8f));

        var button = chip.AddComponent<Button>();
        button.targetGraphic = plate;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;
        button.onClick.AddListener(Take);
    }

    // SAVE: the game goes to the server with a photo (see GameSave). Next to the camera button.
    void BuildSaveButton(Transform parent)
    {
        float size = phone ? 64f : 72f;

        var plate = UiKit.Shape(parent, "Save Button", UiKit.Round, Plate, 14f, Vector2.zero, new Vector2(size, size));
        plate.raycastTarget = true;
        var rt = plate.rectTransform;
        if (phone)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-380f, -160f);     // moved to the left of the camera button in PlaceOnPhone
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-34f - size - 14f, 36f);
        }
        saveChip = plate.gameObject;

        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 14f, Vector2.zero, new Vector2(size, size));
        UiKit.Stretch(edge.rectTransform);
        UiKit.IconImage(plate.transform, "Icon", "arrow-down-to-line", new Color(Bone.r, Bone.g, Bone.b, 0.85f), Vector2.zero, size * 0.5f);
        if (DeviceDetection.KeyHints) HudKeys.CornerKey(plate.transform, "V", size * 0.38f, new Vector2(4f, 8f));

        var button = saveChip.AddComponent<Button>();
        button.targetGraphic = plate;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;
        button.onClick.AddListener(SaveGame);
    }

    // ---------------------------------------------------------------- the picture

    void Update()
    {
        // the keys (C: a picture, V: save the game), when nothing else has the keyboard
        if (DeviceDetection.KeyHints && !busy && !UiLock.Open && !Inventory.BackpackOpen)
        {
            if (Input.GetKeyDown(KeyCode.C)) Take();
            else if (Input.GetKeyDown(KeyCode.V)) SaveGame();
        }

        if (phone && !placed) PlaceOnPhone();

        // the buttons are hidden while a panel is open
        if (chip != null && !busy)
        {
            bool show = !UiLock.Open;
            if (chip.activeSelf != show) chip.SetActive(show);
            if (saveChip != null && saveChip.activeSelf != show) saveChip.SetActive(show);
        }

        if (toast != null)
        {
            if (Time.unscaledTime < toastUntil) InteractPrompt.Request(toast);
            else toast = null;
        }
        else if (GameSave.ToastText != null)
        {
            if (Time.unscaledTime < GameSave.ToastUntil) InteractPrompt.Request(GameSave.ToastText);
            else GameSave.ToastText = null;
        }
    }

    void SaveGame()
    {
        if (busy || UiLock.Open) return;
        if (TutorialMode.Active) { Say("The training can not be saved"); return; }
        if (!AgentAccount.SignedIn) { Say("Sign in from the main menu to save your game"); return; }
        StartCoroutine(SaveRoutine());
    }

    IEnumerator SaveRoutine()
    {
        busy = true;
        chip.SetActive(false);
        saveChip.SetActive(false);

        yield return new WaitForEndOfFrame();

        Texture2D shot = null;
        try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
        catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }

        chip.SetActive(true);
        saveChip.SetActive(true);

        if (shot == null)
        {
            Say("The game could not be saved");
            busy = false;
            yield break;
        }

        Texture2D small = Shrink(shot, 640);
        Destroy(shot);

        Say("Saving...");
        GameSave.Upload(small, (ok, error) =>
        {
            if (small != null) Destroy(small);
            Say(ok ? "Game saved. Continue it from the main menu" : (string.IsNullOrEmpty(error) ? "The game could not be saved" : error));
            busy = false;
        });
    }

    // The photo of the save: the same picture, smaller (a few tens of KB as a JPEG)
    static Texture2D Shrink(Texture2D source, int width)
    {
        int height = Mathf.Max(2, Mathf.RoundToInt(width * (float)source.height / Mathf.Max(1, source.width)));
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, target);

        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var small = new Texture2D(width, height, TextureFormat.RGB24, false);
        small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        small.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return small;
    }

    // On a phone the button goes to the left of the guide (?) button, on the same line (the HUD makes it a moment after the scene starts)
    void PlaceOnPhone()
    {
        var guide = GameObject.Find("Guide Button");
        if (guide == null)
        {
            if (Time.unscaledTime - startedAt > 6f) placed = true;    // not found: it stays where it was put
            return;
        }

        var g = (RectTransform)guide.transform;
        var corners = new Vector3[4];
        g.GetWorldCorners(corners);
        float width = Mathf.Abs(corners[2].x - corners[0].x);
        float height = Mathf.Abs(corners[2].y - corners[0].y);
        if (width < 4f) return;

        var rt = (RectTransform)chip.transform;
        float canvasScale = Mathf.Max(0.01f, rt.GetComponentInParent<Canvas>().scaleFactor);
        Vector3 centre = (corners[0] + corners[2]) * 0.5f;
        rt.position = new Vector3(centre.x - width - 22f * canvasScale, centre.y, centre.z);

        // the SAVE button is the next one to the left
        if (saveChip != null)
            ((RectTransform)saveChip.transform).position = new Vector3(centre.x - 2f * width - 44f * canvasScale, centre.y, centre.z);
        placed = true;
    }

    void Take()
    {
        if (busy || UiLock.Open) return;
        StartCoroutine(CaptureRoutine());
    }

    IEnumerator CaptureRoutine()
    {
        busy = true;

        // the buttons are not in the picture, the mark is
        chip.SetActive(false);
        if (saveChip != null) saveChip.SetActive(false);
        watermark.SetActive(true);

        // on a phone the screen is full of buttons (stick, jump, hotbar, ...): every other canvas is switched off for the picture
        var hidden = phone ? HideOtherCanvases() : null;

        yield return null;                       // a frame for the canvases to go and the mark to be laid out
        yield return new WaitForEndOfFrame();

        Texture2D shot = null;
        try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
        catch (Exception e) { Debug.LogWarning("[Picture] " + e.Message); }

        if (hidden != null)
            foreach (var canvas in hidden)
                if (canvas != null) canvas.enabled = true;

        chip.SetActive(true);
        if (saveChip != null) saveChip.SetActive(true);
        watermark.SetActive(false);

        if (shot != null)
        {
            byte[] png = shot.EncodeToPNG();
            Destroy(shot);
            Deliver(png);
        }
        else
        {
            Say("The picture could not be taken");
        }

        busy = false;
    }

    // Switches off every screen canvas except the one with the mark; returns the ones it switched off, to put them back
    System.Collections.Generic.List<Canvas> HideOtherCanvases()
    {
        var hidden = new System.Collections.Generic.List<Canvas>();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!canvas.enabled || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas.transform.IsChildOf(transform)) continue;       // our own canvas (the mark)
            canvas.enabled = false;
            hidden.Add(canvas);
        }
        return hidden;
    }

    void Deliver(byte[] png)
    {
        string file = "LeikpyarHunters_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

#if UNITY_WEBGL && !UNITY_EDITOR
        LH_ShareImage(png, png.Length, file, "I am hunting ghosts in Leikpyar Hunters. " + SiteAddress, ApiClient.SiteUrl);
        Say("Picture ready to share");
#else
        try
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(pictures)) pictures = Application.persistentDataPath;
            string folder = Path.Combine(pictures, "Leikpyar Hunters");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, file);
            File.WriteAllBytes(path, png);
            Debug.Log("[Picture] Saved: " + path);
            Say("Picture saved in Pictures / Leikpyar Hunters");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Picture] " + e.Message);
            Say("The picture could not be saved");
        }
#endif
    }

    void Say(string text)
    {
        toast = text;
        toastUntil = Time.unscaledTime + 3.5f;
    }
}
