using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The GAME OVER screen. It comes when the player dies: the game stops, the screen fades in over a dark red glow, and shows what the agent did
// in this run (the day he reached, souls sealed, cages checked, nights survived, the score) and who the agent is.
//   TRY AGAIN (Enter) - a new run        LEADERBOARD - the high scores        MAIN MENU (Esc)
// It makes itself in every scene that has a player. (It is drawn by code in the game's own style: there is no scene file to keep.)
public class GameOverScreen : MonoBehaviour
{
    static readonly Color Blood = new Color(0.82f, 0.10f, 0.15f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.97f);
    static readonly Color Dim = new Color(0.92f, 0.88f, 0.80f, 0.5f);

    private GameObject root;
    private CanvasGroup group;
    private Image glow;
    private TMP_Text dayText, soulsText, cagesText, nightsText, scoreText, agentText;
    private bool showing;
    private float shownAt;

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
        if (FindFirstObjectByType<PlayerHealth>() == null || FindFirstObjectByType<GameOverScreen>() != null) return;
        new GameObject("Game Over Screen (auto)").AddComponent<GameOverScreen>();
    }

    void OnEnable()
    {
        PlayerHealth.Died += OnDied;
    }

    void OnDisable()
    {
        PlayerHealth.Died -= OnDied;
        if (showing) Time.timeScale = 1f;
    }

    void Start()
    {
        Build();
        root.SetActive(false);
    }

    void OnDied()
    {
        if (showing) return;
        StartCoroutine(Show());
    }

    // ---------------------------------------------------------------- the screen

    IEnumerator Show()
    {
        // the hit is seen for a moment, then the screen comes
        yield return new WaitForSecondsRealtime(1.1f);

        Fill();
        showing = true;
        shownAt = Time.unscaledTime;
        group.alpha = 0f;
        root.SetActive(true);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!showing) return;

        float t = Time.unscaledTime - shownAt;
        group.alpha = Mathf.Clamp01(t / 1.4f);

        // the glow breathes
        if (glow != null)
        {
            float pulse = 0.20f + 0.07f * Mathf.Sin(t * 1.6f);
            glow.color = new Color(0.75f, 0.05f, 0.08f, pulse * group.alpha);
        }

        if (t > 1f && !LeaderboardScreen.IsOpen && !AccountScreen.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TryAgain();
            else if (Input.GetKeyDown(KeyCode.Escape) && !SettingsPanel.IsOpen) ToMenu();
        }
    }

    void Fill()
    {
        int day = AgentStats.Day;
        dayText.text = day.ToString();
        soulsText.text = AgentStats.Captures.ToString();
        cagesText.text = AgentStats.Cages.ToString();
        nightsText.text = AgentStats.Nights.ToString();
        scoreText.text = AgentStats.Score.ToString("N0");

        if (AgentAccount.SignedIn && AgentAccount.Agent != null)
            agentText.text = AgentAccount.Agent.agentId + "   " + AgentAccount.Agent.codename;
        else
            agentText.text = "Sign in to put your score on the board";
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        root = OnlineUi.MakeScreen(transform, "GameOver", 100, 0.95f);
        group = root.AddComponent<CanvasGroup>();
        var r = root.transform;

        // a dark red glow behind the ghost
        glow = UiKit.Shape(r, "Glow", UiKit.Circle, new Color(0.75f, 0.05f, 0.08f, 0.2f), 0f, new Vector2(0f, 250f), new Vector2(900f, 900f));
        glow.type = Image.Type.Simple;
        glow.raycastTarget = false;

        // the ghost of the game, big and pale
        var logoRt = UiKit.NewRect("Ghost", r);
        UiKit.Place(logoRt, new Vector2(0f, 330f), new Vector2(250f, 250f));
        var logo = logoRt.gameObject.AddComponent<Image>();
        var texture = Resources.Load<Texture2D>("Brand/logo");
        if (texture != null) logo.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        logo.enabled = logo.sprite != null;
        logo.preserveAspect = true;
        logo.color = new Color(1f, 0.92f, 0.92f, 0.9f);
        logo.raycastTarget = false;

        var title = OnlineUi.Text(r, "GAME OVER", 138f, Blood, TextAlignmentOptions.Center, new Vector2(0f, 170f), new Vector2(1500f, 170f), true);
        title.characterSpacing = 14f;
        var sub = OnlineUi.Text(r, "THE GHOST CAUGHT YOU", 30f, Dim, TextAlignmentOptions.Center, new Vector2(0f, 84f), new Vector2(1000f, 44f));
        sub.characterSpacing = 8f;

        // what the agent did
        string[] labels = { "DAY", "SOULS SEALED", "CAGES CHECKED", "NIGHTS SURVIVED" };
        var values = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 300f;
            OnlineUi.Plate(r, "Stat", new Vector2(x, -20f), new Vector2(276f, 128f), new Color(0.03f, 0.02f, 0.015f, 0.9f), true);
            var l = OnlineUi.Text(r, labels[i], 18f, Dim, TextAlignmentOptions.Center, new Vector2(x, 22f), new Vector2(264f, 28f));
            l.characterSpacing = 3f;
            values[i] = OnlineUi.Text(r, "0", 58f, Bone, TextAlignmentOptions.Center, new Vector2(x, -38f), new Vector2(264f, 70f), true, true);
        }
        dayText = values[0]; soulsText = values[1]; cagesText = values[2]; nightsText = values[3];

        // the score
        OnlineUi.Plate(r, "Score", new Vector2(0f, -150f), new Vector2(1176f, 96f), new Color(0.07f, 0.02f, 0.03f, 0.92f), true);
        var scoreLabel = OnlineUi.Text(r, "SCORE", 26f, Dim, TextAlignmentOptions.MidlineLeft, new Vector2(-380f, -150f), new Vector2(300f, 40f));
        scoreLabel.characterSpacing = 6f;
        scoreText = OnlineUi.Text(r, "0", 66f, OnlineUi.Amber, TextAlignmentOptions.MidlineRight, new Vector2(300f, -150f), new Vector2(520f, 80f), true, true);

        // who
        agentText = OnlineUi.Text(r, "", 24f, Dim, TextAlignmentOptions.Center, new Vector2(0f, -232f), new Vector2(1000f, 36f));

        // the buttons
        OnlineUi.MakeButton(r, "TRY AGAIN", new Vector2(-339f, -330f), new Vector2(360f, 84f), TryAgain, true);
        OnlineUi.MakeButton(r, "LEADERBOARD", new Vector2(30f, -330f), new Vector2(330f, 84f), () => LeaderboardScreen.Open(), false);
        OnlineUi.MakeButton(r, "MAIN MENU", new Vector2(369f, -330f), new Vector2(300f, 84f), ToMenu, false);

        var hint = OnlineUi.Text(r, "Enter  try again        Esc  main menu", 20f, new Color(Bone.r, Bone.g, Bone.b, 0.35f), TextAlignmentOptions.Center,
            new Vector2(0f, -420f), new Vector2(900f, 30f));
        hint.characterSpacing = 2f;
    }

    // ---------------------------------------------------------------- the buttons

    void TryAgain()
    {
        Time.timeScale = 1f;
        showing = false;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(TutorialMode.GameSceneName);
    }

    void ToMenu()
    {
        Time.timeScale = 1f;
        showing = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(TutorialMode.MenuSceneName);
    }
}
