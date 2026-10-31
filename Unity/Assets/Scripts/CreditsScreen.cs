using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The CREDITS of the intro menu, as the game's own "AGENT ROSTER": every person is an agent card (a dark plate with a red edge, a square frame with
// the picture of a tool of the game, the role, the name on one line) on a dark brown background with HUD corner brackets.
// Only small things move: a thin light line sweeps down slowly, a few specks of dust drift up, and the light flickers a little.
// It makes itself in the intro scene and replaces the old credits panel. BACK or Esc closes it.
// To change the people: edit the Crew list below.
public class CreditsScreen : MonoBehaviour
{
    // role, name, picture (Resources/Crew: the faceless avatar of each person), a short line under the name (can be empty)
    static readonly string[][] Crew =
    {
        new[] { "GAME DESIGNER", "Ayar Min", "game-designer", "" },
        new[] { "SOUND", "Wai Yan Min Thu", "sound", "" },
        new[] { "DEVELOPER", "Swan Htet", "developer", "" },
        new[] { "3D ART", "May Thinzar Khin", "artist", "" },
    };

    // The avatar is a plain picture file (Resources/Crew/<name>.png): it becomes a sprite here, so it needs no import setting
    static Sprite LoadAvatar(string file)
    {
        var texture = Resources.Load<Texture2D>("Crew/" + file);
        if (texture == null) return Resources.Load<Sprite>("Crew/" + file);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.82f, 0.10f, 0.15f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.97f);
    static readonly Color Brown = new Color(0.075f, 0.047f, 0.030f, 1f);
    static readonly Color Warm = new Color(1f, 0.82f, 0.62f, 1f);

    private IntroMenu menu;
    private GameObject panel;
    private float openedAt;
    private bool wasOpen;

    [System.Serializable] private class CrewEntry { public string slot; public string photo; }
    [System.Serializable] private class CrewAnswer { public bool ok; public CrewEntry[] crew; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void JS_LoadCrewPhoto(string url, string slot, string objectName, string method);
#endif

    private class Card
    {
        public string slot;
        public Image picture;
        public RectTransform root;
        public CanvasGroup group;
        public Vector2 home;
        public Image glow;
    }

    private class Wisp
    {
        public RectTransform rt;
        public Image image;
        public float speed, sway, phase, baseX, alpha;
    }

    private readonly System.Collections.Generic.List<Card> cards = new System.Collections.Generic.List<Card>();
    private readonly System.Collections.Generic.List<Wisp> wisps = new System.Collections.Generic.List<Wisp>();
    private CanvasGroup titleGroup, footerGroup;
    private RectTransform scan;
    private Image veilImage_;
    private TMP_Text titleText;
    private float flickerAt;

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
        if (FindFirstObjectByType<IntroMenu>() == null || FindFirstObjectByType<CreditsScreen>() != null) return;
        new GameObject("Credits Screen (auto)").AddComponent<CreditsScreen>();
    }

    void Start()
    {
        menu = FindFirstObjectByType<IntroMenu>();
        if (menu == null) { enabled = false; return; }

        Build();

        // the old panel goes, this one takes its place (the menu opens and closes 'creditsPanel')
        if (menu.creditsPanel != null) Destroy(menu.creditsPanel);
        menu.creditsPanel = panel;
        panel.SetActive(false);

        // the real photos of the people, from the server (the ones the admins put on the website)
        StartCoroutine(LoadPhotos());
    }

    // ---------------------------------------------------------------- the photos from the server

    // Asks the server which creators have a photo. Every photo replaces the avatar of its card; the avatars stay when there is no photo or no server.
    IEnumerator LoadPhotos()
    {
        string json = null;
        ApiClient.Get("/api/crew", r => json = r.ok ? r.text : "");
        while (json == null) yield return null;
        if (json.Length == 0) yield break;

        CrewAnswer answer = null;
        try { answer = JsonUtility.FromJson<CrewAnswer>(json); } catch (System.Exception) { }
        if (answer == null || answer.crew == null) yield break;

        foreach (var entry in answer.crew)
        {
            if (entry == null || string.IsNullOrEmpty(entry.photo)) continue;

            string url = ApiClient.BaseUrl + entry.photo;
#if UNITY_WEBGL && !UNITY_EDITOR
            // the browser reads the picture (it also reads WebP) and sends it back as a JPG: see Plugins/WebGL/CrewPhoto.jslib
            JS_LoadCrewPhoto(url, entry.slot, gameObject.name, "OnCrewPhoto");
#else
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) ShowPhoto(entry.slot, DownloadHandlerTexture.GetContent(request));
                else Debug.LogWarning("[Credits] photo of " + entry.slot + ": " + request.error + "  (PNG and JPG only here; the web game also reads WebP)");
            }
#endif
        }
    }

    // Called by the browser plugin: "<slot>|<base64 of a JPG>"
    public void OnCrewPhoto(string message)
    {
        int bar = message.IndexOf('|');
        if (bar < 0) return;

        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (texture.LoadImage(System.Convert.FromBase64String(message.Substring(bar + 1)))) ShowPhoto(message.Substring(0, bar), texture);
            else Destroy(texture);
        }
        catch (System.Exception)
        {
            Destroy(texture);
        }
    }

    // The picture goes into the square frame of the card (the middle square of the picture, so a person is not stretched)
    void ShowPhoto(string slot, Texture2D texture)
    {
        if (texture == null) return;

        foreach (var card in cards)
        {
            if (card.slot != slot || card.picture == null) continue;

            texture.wrapMode = TextureWrapMode.Clamp;
            int side = Mathf.Min(texture.width, texture.height);
            var square = new Rect((texture.width - side) * 0.5f, (texture.height - side) * 0.5f, side, side);
            card.picture.sprite = Sprite.Create(texture, square, new Vector2(0.5f, 0.5f), 100f);
            card.picture.enabled = true;
            card.picture.color = Color.white;
        }
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        var canvasGo = new GameObject("CreditsCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();

        // the veil: dark, blocks the menu behind it
        var veil = UiKit.NewRect("Credits Panel", canvasGo.transform);
        UiKit.Stretch(veil);
        var veilImage = veil.gameObject.AddComponent<Image>();
        veilImage.color = Brown;
        veilImage_ = veilImage;
        veilImage.raycastTarget = true;
        panel = veil.gameObject;

        var grunge = UiKit.NewRect("Grunge", veil);
        UiKit.Stretch(grunge);
        var grungeImage = grunge.gameObject.AddComponent<Image>();
        grungeImage.sprite = UiKit.Grunge;
        grungeImage.color = new Color(1f, 0.9f, 0.85f, 0.05f);
        grungeImage.raycastTarget = false;

        BuildBackdrop(veil);
        BuildDust(veil);

        // a thin line of light that sweeps down, slowly
        scan = UiKit.NewRect("Scan", veil);
        scan.anchorMin = scan.anchorMax = new Vector2(0.5f, 0.5f);
        scan.sizeDelta = new Vector2(2400f, 3f);
        var scanImage = scan.gameObject.AddComponent<Image>();
        scanImage.color = new Color(Warm.r, Warm.g, Warm.b, 0.07f);
        scanImage.raycastTarget = false;

        BuildBrackets(veil);
        BuildTitle(veil);

        for (int i = 0; i < Crew.Length; i++) BuildCard(veil, i);

        BuildFooter(veil);
    }

    // The background: dark brown, darker at the top and the bottom. No round shapes.
    void BuildBackdrop(Transform parent)
    {
        foreach (bool top in new[] { true, false })
        {
            var fade = UiKit.NewRect(top ? "Dark Top" : "Dark Bottom", parent);
            fade.anchorMin = new Vector2(0f, top ? 1f : 0f);
            fade.anchorMax = new Vector2(1f, top ? 1f : 0f);
            fade.pivot = new Vector2(0.5f, top ? 1f : 0f);
            fade.sizeDelta = new Vector2(0f, 300f);
            fade.anchoredPosition = Vector2.zero;
            var image = fade.gameObject.AddComponent<Image>();
            image.sprite = UiKit.VerticalFade;
            image.color = new Color(0.02f, 0.012f, 0.008f, 0.75f);
            image.raycastTarget = false;
            if (!top) fade.localScale = new Vector3(1f, -1f, 1f);   // the fade is drawn upside down at the bottom
        }
    }

    // Small specks of dust (little squares), drifting up
    void BuildDust(Transform parent)
    {
        for (int i = 0; i < 26; i++)
        {
            var rt = UiKit.NewRect("Dust " + i, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Random.Range(2f, 5f);
            rt.sizeDelta = new Vector2(size, size);

            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(Warm.r, Warm.g, Warm.b, Random.Range(0.10f, 0.28f));
            image.raycastTarget = false;

            var wisp = new Wisp
            {
                rt = rt,
                image = image,
                speed = Random.Range(6f, 20f),
                sway = Random.Range(8f, 30f),
                phase = Random.Range(0f, 6.28f),
                baseX = Random.Range(-930f, 930f)
            };
            rt.anchoredPosition = new Vector2(wisp.baseX, Random.Range(-540f, 540f));
            wisps.Add(wisp);
        }
    }

    // HUD corner brackets
    void BuildBrackets(Transform parent)
    {
        for (int c = 0; c < 4; c++)
        {
            float sx = (c % 2 == 0) ? 1f : -1f;      // left / right
            float sy = (c < 2) ? 1f : -1f;           // bottom / top
            var corner = new Vector2(c % 2 == 0 ? 0f : 1f, c < 2 ? 0f : 1f);

            for (int part = 0; part < 2; part++)
            {
                var rt = UiKit.NewRect("Bracket", parent);
                rt.anchorMin = rt.anchorMax = corner;
                rt.pivot = corner;
                rt.sizeDelta = part == 0 ? new Vector2(90f, 4f) : new Vector2(4f, 90f);
                rt.anchoredPosition = new Vector2(48f * sx, 48f * sy);
                var image = rt.gameObject.AddComponent<Image>();
                image.color = new Color(Blood.r, Blood.g, Blood.b, 0.7f);
                image.raycastTarget = false;
            }
        }
    }

    void BuildTitle(Transform parent)
    {
        var holder = UiKit.NewRect("Title", parent);
        UiKit.Stretch(holder);
        titleGroup = holder.gameObject.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;

        var heading = UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi;

        var tag = UiKit.Label(holder, "Tag", "SPECTRAL AGENTS", heading, 30f, Blood, TextAlignmentOptions.Center, new Vector2(0f, 432f), new Vector2(900f, 44f));
        tag.characterSpacing = 14f;
        tag.raycastTarget = false;
        tag.gameObject.AddComponent<LocalizeIgnore>();

        titleText = UiKit.Label(holder, "Roster", "AGENT ROSTER", heading, 88f, Bone, TextAlignmentOptions.Center, new Vector2(0f, 352f), new Vector2(1300f, 120f));
        titleText.characterSpacing = 8f;
        titleText.raycastTarget = false;

        var sub = UiKit.Label(holder, "Sub", "MYANMAR  -  CLASSIFIED", UiKit.DmMedium, 24f, new Color(Bone.r, Bone.g, Bone.b, 0.55f), TextAlignmentOptions.Center,
            new Vector2(0f, 286f), new Vector2(900f, 34f));
        sub.characterSpacing = 6f;
        sub.raycastTarget = false;

        var line = UiKit.Shape(holder, "Line", UiKit.Round, Blood, 2f, new Vector2(0f, 258f), new Vector2(180f, 4f));
        line.raycastTarget = false;
    }

    void BuildCard(Transform parent, int i)
    {
        var data = Crew[i];
        float x = (i - (Crew.Length - 1) * 0.5f) * 420f;
        var home = new Vector2(x, -45f);

        var plate = HudStyler.MakePlate(parent, "Agent " + (i + 1), 22f, new Vector2(380f, 430f));
        var rt = plate.rectTransform;
        rt.anchoredPosition = home;
        var group = plate.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        var card = new Card { slot = data[2], root = rt, group = group, home = home };

        // the number of the agent and his status
        var tag = UiKit.Label(rt, "Number", "AGENT " + (i + 1).ToString("00"), UiKit.DmSemi, 20f, new Color(Bone.r, Bone.g, Bone.b, 0.6f),
            TextAlignmentOptions.MidlineLeft, new Vector2(-70f, 180f), new Vector2(220f, 30f));
        tag.characterSpacing = 3f;
        tag.raycastTarget = false;

        var status = UiKit.Label(rt, "Status", "ON DUTY", UiKit.DmSemi, 18f, new Color(Bone.r, Bone.g, Bone.b, 0.55f),
            TextAlignmentOptions.MidlineRight, new Vector2(86f, 180f), new Vector2(150f, 30f));
        status.characterSpacing = 3f;
        status.raycastTarget = false;
        var dot = UiKit.Shape(rt, "Dot", UiKit.Round, new Color(0.3f, 0.9f, 0.45f, 1f), 2f, new Vector2(176f, 180f), new Vector2(10f, 10f));
        dot.raycastTarget = false;

        // the picture in a square frame with cut corners
        var frame = UiKit.Shape(rt, "Frame", UiKit.Cut, new Color(0.03f, 0.02f, 0.015f, 0.92f), 16f, new Vector2(0f, 66f), new Vector2(176f, 176f));
        frame.raycastTarget = false;
        var frameEdge = UiKit.Shape(frame.transform, "Edge", UiKit.CutOutline, Blood, 16f, Vector2.zero, new Vector2(176f, 176f));
        UiKit.Stretch(frameEdge.rectTransform);

        // the avatar fills the frame; the frame's cut corners are its mask
        var holder = UiKit.Shape(rt, "Picture Mask", UiKit.Cut, Color.white, 16f, new Vector2(0f, 66f), new Vector2(168f, 168f));
        holder.raycastTarget = false;
        holder.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var pictureRt = UiKit.NewRect("Picture", holder.transform);
        UiKit.Stretch(pictureRt);
        var picture = pictureRt.gameObject.AddComponent<Image>();
        picture.sprite = LoadAvatar(data[2]);
        picture.preserveAspect = false;
        picture.raycastTarget = false;
        picture.enabled = picture.sprite != null;
        card.picture = picture;

        var line = UiKit.Shape(rt, "Line", UiKit.Round, new Color(Edge.r, Edge.g, Edge.b, 0.9f), 2f, new Vector2(0f, -44f), new Vector2(300f, 2f));
        line.raycastTarget = false;

        // the role and the name
        var role = UiKit.Label(rt, "Role", data[0], UiKit.DmSemi, 25f, Blood, TextAlignmentOptions.Center, new Vector2(0f, -82f), new Vector2(350f, 38f));
        role.characterSpacing = 5f;
        role.raycastTarget = false;

        var heading = UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi;
        var nameText = UiKit.Label(rt, "Name", data[1], heading, 38f, Bone, TextAlignmentOptions.Center, new Vector2(0f, -140f), new Vector2(340f, 56f));
        nameText.enableWordWrapping = false;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMax = 38f;
        nameText.fontSizeMin = 18f;
        nameText.overflowMode = TextOverflowModes.Overflow;
        nameText.raycastTarget = false;
        nameText.gameObject.AddComponent<LocalizeIgnore>();

        // what this person made (a short line under the name)
        if (!string.IsNullOrEmpty(data[3]))
        {
            var note = UiKit.Label(rt, "Note", data[3], UiKit.DmMedium, 17f, new Color(Bone.r, Bone.g, Bone.b, 0.55f), TextAlignmentOptions.Center,
                new Vector2(0f, -188f), new Vector2(330f, 46f));
            note.characterSpacing = 2f;
            note.enableWordWrapping = true;
            note.raycastTarget = false;
        }

        cards.Add(card);
    }

    void BuildFooter(Transform parent)
    {
        var holder = UiKit.NewRect("Footer", parent);
        UiKit.Stretch(holder);
        footerGroup = holder.gameObject.AddComponent<CanvasGroup>();

        var motto = UiKit.Label(holder, "Motto", "SURVIVE THE NIGHT", UiKit.DmMedium, 26f, new Color(Bone.r, Bone.g, Bone.b, 0.6f),
            TextAlignmentOptions.Center, new Vector2(0f, -312f), new Vector2(800f, 36f));
        motto.characterSpacing = 10f;
        motto.raycastTarget = false;

        // BACK: a cut-corner plate like the game's HUD, an arrow, the word, and the key (Esc) in the same little key plate as the HUD
        var size = new Vector2(330f, 68f);
        var plate = UiKit.Shape(holder, "Back", UiKit.Cut, new Color(0.05f, 0.04f, 0.05f, 0.92f), 16f, new Vector2(0f, -400f), size);
        plate.raycastTarget = true;
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, Blood, 16f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var arrow = UiKit.IconImage(plate.transform, "Arrow", "arrow-left", Bone, new Vector2(-122f, 0f), 30f);
        arrow.raycastTarget = false;

        var label = UiKit.Label(plate.transform, "Label", "BACK", UiKit.DmSemi, 30f, Bone, TextAlignmentOptions.MidlineLeft, new Vector2(10f, 0f), new Vector2(200f, 60f));
        label.characterSpacing = 8f;
        label.raycastTarget = false;

        var chip = UiKit.Shape(plate.transform, "Key Esc", UiKit.Round, new Color(0.02f, 0.02f, 0.025f, 0.95f), 8f, new Vector2(118f, 0f), new Vector2(64f, 36f));
        chip.raycastTarget = false;
        var chipEdge = UiKit.Shape(chip.transform, "Edge", UiKit.RoundOutline, Edge, 8f, Vector2.zero, new Vector2(64f, 36f));
        UiKit.Stretch(chipEdge.rectTransform);
        var chipText = UiKit.Label(chip.transform, "Letters", "ESC", UiKit.DmSemi, 20f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(64f, 36f));
        chipText.raycastTarget = false;
        chipText.gameObject.AddComponent<LocalizeIgnore>();

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.8f, 0.8f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.onClick.AddListener(() => { if (menu != null) menu.CloseCredits(); });
    }

    // ---------------------------------------------------------------- the small movement

    void Update()
    {
        if (panel == null || !panel.activeInHierarchy) return;

        float t = Time.unscaledTime;

        // the line of light sweeps down, slowly
        float sweep = Mathf.Repeat(t / 11f, 1f);
        scan.anchoredPosition = new Vector2(0f, Mathf.Lerp(560f, -560f, sweep));

        // the dust drifts up and sways a little
        foreach (var w in wisps)
        {
            Vector2 p = w.rt.anchoredPosition;
            p.y += w.speed * Time.unscaledDeltaTime;
            if (p.y > 560f) p.y = -560f;
            p.x = w.baseX + Mathf.Sin(t * 0.3f + w.phase) * w.sway;
            w.rt.anchoredPosition = p;
        }

        // the light flickers a little, like an old lamp
        float flicker = 1f + (Mathf.PerlinNoise(t * 5f, 0.3f) - 0.5f) * 0.12f;
        if (veilImage_ != null) veilImage_.color = new Color(Brown.r * flicker, Brown.g * flicker, Brown.b * flicker, 1f);
    }
}
