using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The online parts of the intro menu, made by themselves in the intro scene:
//  - a small AGENT plate at the top right: "SIGN IN" or the Agent ID; it opens the Agent Registry
//  - LOAD GAME (there is no save system) becomes LEADERBOARD and opens the High Scores
public class IntroOnline : MonoBehaviour
{
    private TMP_Text line1, line2;
    private Image edge;

    // the saved game: a card with its photo, bottom right
    private GameObject card;
    private RawImage cardPhoto;
    private TMP_Text cardLine, cardNote;
    private bool lastSigned;

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
        if (FindFirstObjectByType<IntroMenu>() == null || FindFirstObjectByType<IntroOnline>() != null) return;
        new GameObject("Intro Online (auto)").AddComponent<IntroOnline>();
    }

    void Start()
    {
        Build();
        AgentAccount.Changed += Refresh;
        Refresh();
        if (!AgentAccount.Checked) AgentAccount.Refresh();
        StartCoroutine(RenameLoadGame());
    }

    void OnDestroy()
    {
        AgentAccount.Changed -= Refresh;
    }

    void Build()
    {
        var canvasGo = new GameObject("AgentChipCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();

        var size = new Vector2(310f, 72f);
        var plate = UiKit.Shape(canvasGo.transform, "Agent", UiKit.Cut, new Color(0.04f, 0.03f, 0.03f, 0.88f), 16f, Vector2.zero, size);
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -150f);   // under the language text (MYANMAR) at the top right
        plate.raycastTarget = true;

        var e = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, OnlineUi.Edge, 16f, Vector2.zero, size);
        UiKit.Stretch(e.rectTransform);
        edge = e;

        line1 = OnlineUi.Text(plate.transform, "AGENT", 15f, OnlineUi.Dim, TextAlignmentOptions.MidlineLeft, new Vector2(10f, 18f), new Vector2(270f, 22f));
        line1.characterSpacing = 5f;
        line2 = OnlineUi.Text(plate.transform, "SIGN IN", 28f, OnlineUi.Bone, TextAlignmentOptions.MidlineLeft, new Vector2(10f, -12f), new Vector2(270f, 36f));
        line2.characterSpacing = 3f;

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.onClick.AddListener(AccountScreen.Open);

        BuildCard(canvasGo.transform);
    }

    // CONTINUE: the photo of the saved game, the day and the time. A click loads it.
    void BuildCard(Transform canvas)
    {
        var size = new Vector2(420f, 340f);
        var plate = UiKit.Shape(canvas, "Continue", UiKit.Cut, new Color(0.04f, 0.03f, 0.03f, 0.9f), 16f, Vector2.zero, size);
        plate.raycastTarget = true;
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 40f);

        var e = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, OnlineUi.Edge, 16f, Vector2.zero, size);
        UiKit.Stretch(e.rectTransform);

        var photoRt = UiKit.NewRect("Photo", plate.transform);
        UiKit.Place(photoRt, new Vector2(0f, 38f), new Vector2(380f, 214f));
        cardPhoto = photoRt.gameObject.AddComponent<RawImage>();
        cardPhoto.color = new Color(0.12f, 0.1f, 0.1f, 1f);
        cardPhoto.raycastTarget = false;

        var title = OnlineUi.Text(plate.transform, "CONTINUE", 28f, OnlineUi.Blood, TextAlignmentOptions.MidlineLeft, new Vector2(0f, -96f), new Vector2(380f, 36f), true);
        title.characterSpacing = 5f;
        cardLine = OnlineUi.Text(plate.transform, "", 24f, OnlineUi.Bone, TextAlignmentOptions.MidlineLeft, new Vector2(0f, -130f), new Vector2(380f, 32f));
        cardNote = OnlineUi.Text(plate.transform, "", 19f, OnlineUi.Dim, TextAlignmentOptions.MidlineLeft, new Vector2(0f, -158f), new Vector2(380f, 28f));

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            cardNote.text = "Loading the saved game";
            GameSave.StartLoad(message => cardNote.text = message);
        });

        card = plate.gameObject;
        card.SetActive(false);
    }

    // Is there a saved game? The card shows it (signed in only: the save belongs to the agent)
    void RefreshSave()
    {
        if (card == null) return;
        if (!AgentAccount.SignedIn) { card.SetActive(false); return; }

        GameSave.FetchInfo(info =>
        {
            if (card == null) return;
            if (info == null || !info.Exists) { card.SetActive(false); return; }

            cardLine.text = "DAY " + info.day + "  \u00b7  " + GameSave.ClockText(info.hour);
            cardNote.text = System.DateTimeOffset.FromUnixTimeSeconds(info.saved).LocalDateTime.ToString("dd MMM yyyy   HH:mm");
            card.SetActive(true);

            if (info.photo)
                GameSave.FetchPhoto(texture =>
                {
                    if (cardPhoto == null || texture == null) return;
                    cardPhoto.texture = texture;
                    cardPhoto.color = Color.white;
                });
        });
    }

    void Refresh()
    {
        if (line1 == null) return;

        if (AgentAccount.SignedIn != lastSigned)
        {
            lastSigned = AgentAccount.SignedIn;
            RefreshSave();
        }

        if (AgentAccount.SignedIn)
        {
            line1.text = AgentAccount.Agent.codename.ToUpperInvariant();
            line2.text = AgentAccount.Agent.agentId;
            line2.color = OnlineUi.Amber;
            edge.color = new Color(OnlineUi.Ok.r, OnlineUi.Ok.g, OnlineUi.Ok.b, 0.55f);
        }
        else
        {
            line1.text = "AGENT";
            line2.text = "SIGN IN";
            line2.color = OnlineUi.Bone;
            edge.color = OnlineUi.Edge;
        }
    }

    // The LOAD GAME button shows LEADERBOARD (its action is changed in IntroMenu.LoadGame)
    IEnumerator RenameLoadGame()
    {
        yield return null;
        yield return null;

        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "LoadGame") continue;
            foreach (var text in t.GetComponentsInChildren<TMP_Text>(true))
                if (text.text.Trim().ToUpperInvariant() == "LOAD GAME") text.text = "LEADERBOARD";
        }
    }
}
