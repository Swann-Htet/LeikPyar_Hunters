using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The HIGH SCORES screen, in the style of an arcade machine: rank (1ST, 2ND...), the agent, souls / cages / nights and the score with
// seven digits. The best ten agents, and the player's own row under them when he is lower. Open it with LeaderboardScreen.Open().
public class LeaderboardScreen : MonoBehaviour
{
    private static LeaderboardScreen instance;

    public static void Open()
    {
        if (instance == null)
        {
            var go = new GameObject("Leaderboard Screen");
            instance = go.AddComponent<LeaderboardScreen>();
        }
        instance.Show();
    }

    public static bool IsOpen { get { return instance != null && instance.panel != null && instance.panel.activeSelf; } }

    const int Rows = 10;
    const float RowStep = 50f;
    const float FirstRow = 236f;

    private class Row
    {
        public Image plate, edge;
        public TMP_Text rank, name, id, souls, cages, nights, score;
    }

    private GameObject panel;
    private readonly Row[] rows = new Row[Rows + 1];     // the last one is the player's own row
    private TMP_Text status, prompt, gap;
    private bool loading;

    void Awake()
    {
        Build();
        panel.SetActive(false);
    }

    void OnEnable() { AgentAccount.Changed += RefreshPrompt; }
    void OnDisable() { AgentAccount.Changed -= RefreshPrompt; }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) panel.SetActive(false);
    }

    void Show()
    {
        panel.SetActive(true);
        RefreshPrompt();
        Load();
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        panel = OnlineUi.MakeScreen(transform, "Leaderboard", 105, 0.94f);
        var root = panel.transform;

        var plate = UiKit.Shape(root, "Board", UiKit.Cut, OnlineUi.Brown, 26f, Vector2.zero, new Vector2(1560f, 920f));
        plate.raycastTarget = false;
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.CutOutline, OnlineUi.Edge, 26f, Vector2.zero, new Vector2(1560f, 920f));
        UiKit.Stretch(edge.rectTransform);

        // the lines of the screen
        var lines = UiKit.NewRect("Scanlines", plate.transform);
        UiKit.Stretch(lines);
        var linesImage = lines.gameObject.AddComponent<Image>();
        linesImage.sprite = ScanSprite();
        linesImage.type = Image.Type.Tiled;
        linesImage.color = Color.white;
        linesImage.raycastTarget = false;

        var t = plate.transform;
        var title = OnlineUi.Text(t, "HIGH SCORES", 70f, OnlineUi.Amber, TextAlignmentOptions.Center, new Vector2(0f, 392f), new Vector2(1200f, 96f), true);
        title.characterSpacing = 16f;
        var sub = OnlineUi.Text(t, "BEST NIGHT OF EACH AGENT", 20f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, 334f), new Vector2(1000f, 30f));
        sub.characterSpacing = 8f;

        // the head of the table
        float hy = 290f;
        Head(t, "RANK", -650f, 140f, hy, TextAlignmentOptions.MidlineLeft);
        Head(t, "AGENT", -330f, 520f, hy, TextAlignmentOptions.MidlineLeft);
        Head(t, "SOULS", 170f, 120f, hy, TextAlignmentOptions.Center);
        Head(t, "CAGES", 300f, 120f, hy, TextAlignmentOptions.Center);
        Head(t, "NIGHTS", 430f, 120f, hy, TextAlignmentOptions.Center);
        Head(t, "SCORE", 600f, 240f, hy, TextAlignmentOptions.MidlineRight);
        UiKit.Shape(t, "Line", UiKit.Round, OnlineUi.Edge, 2f, new Vector2(0f, hy - 24f), new Vector2(1440f, 2f)).raycastTarget = false;

        for (int i = 0; i < Rows; i++) rows[i] = MakeRow(t, FirstRow - i * RowStep);
        gap = OnlineUi.Text(t, ". . .", 20f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, FirstRow - Rows * RowStep + 6f), new Vector2(400f, 24f));
        rows[Rows] = MakeRow(t, FirstRow - Rows * RowStep - 34f);

        status = OnlineUi.Text(t, "", 26f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1200f, 40f));
        status.characterSpacing = 6f;

        prompt = OnlineUi.Text(t, "", 22f, OnlineUi.Amber, TextAlignmentOptions.Center, new Vector2(0f, -352f), new Vector2(1000f, 32f));
        prompt.characterSpacing = 8f;

        OnlineUi.MakeButton(t, "BACK", new Vector2(-560f, -402f), new Vector2(300f, 66f), () => panel.SetActive(false), false);
        OnlineUi.MakeButton(t, "REFRESH", new Vector2(560f, -402f), new Vector2(300f, 66f), Load, false);
    }

    void Head(Transform parent, string text, float x, float width, float y, TextAlignmentOptions align)
    {
        var h = OnlineUi.Text(parent, text, 18f, OnlineUi.Dim, align, new Vector2(x, y), new Vector2(width, 26f));
        h.characterSpacing = 4f;
    }

    Row MakeRow(Transform parent, float y)
    {
        var row = new Row();
        row.plate = UiKit.Shape(parent, "Row", UiKit.Round, new Color(1f, 1f, 1f, 0f), 10f, new Vector2(0f, y), new Vector2(1470f, 46f));
        row.plate.raycastTarget = false;
        row.edge = UiKit.Shape(row.plate.transform, "Edge", UiKit.RoundOutline, new Color(1f, 1f, 1f, 0f), 10f, Vector2.zero, new Vector2(1470f, 46f));
        UiKit.Stretch(row.edge.rectTransform);

        row.rank = OnlineUi.Text(parent, "", 32f, OnlineUi.Bone, TextAlignmentOptions.MidlineLeft, new Vector2(-650f, y), new Vector2(140f, 44f), true, true);
        row.rank.characterSpacing = 3f;
        row.name = OnlineUi.Text(parent, "", 28f, OnlineUi.Bone, TextAlignmentOptions.MidlineLeft, new Vector2(-330f, y + 7f), new Vector2(520f, 32f), false, true);
        row.id = OnlineUi.Text(parent, "", 15f, OnlineUi.Dim, TextAlignmentOptions.MidlineLeft, new Vector2(-330f, y - 15f), new Vector2(520f, 20f), false, true);
        row.id.characterSpacing = 4f;
        row.souls = OnlineUi.Text(parent, "", 26f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(170f, y), new Vector2(120f, 40f), false, true);
        row.cages = OnlineUi.Text(parent, "", 26f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(300f, y), new Vector2(120f, 40f), false, true);
        row.nights = OnlineUi.Text(parent, "", 26f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(430f, y), new Vector2(120f, 40f), false, true);
        row.score = OnlineUi.Text(parent, "", 36f, OnlineUi.Bone, TextAlignmentOptions.MidlineRight, new Vector2(600f, y), new Vector2(240f, 44f), true, true);
        row.score.characterSpacing = 6f;
        return row;
    }

    // thin dark lines, like the screen of a machine (a small picture that repeats)
    static Sprite scan;
    static Sprite ScanSprite()
    {
        if (scan != null) return scan;
        // 256 x 16: one thin line in 16 rows (the picture repeats, so only a few hundred squares are drawn)
        var tex = new Texture2D(256, 16, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 256; x++)
                tex.SetPixel(x, y, y == 0 ? new Color(1f, 0.9f, 0.8f, 0.04f) : new Color(0f, 0f, 0f, 0f));
        tex.Apply();
        scan = Sprite.Create(tex, new Rect(0, 0, 256, 16), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        return scan;
    }

    // ---------------------------------------------------------------- data

    void RefreshPrompt()
    {
        if (prompt == null) return;
        prompt.text = AgentAccount.SignedIn ? "PLAY TO BEAT YOUR BEST" : "SIGN IN TO ENTER THE BOARD";
    }

    static string Ordinal(int n)
    {
        if (n % 100 >= 11 && n % 100 <= 13) return n + "TH";
        switch (n % 10)
        {
            case 1: return n + "ST";
            case 2: return n + "ND";
            case 3: return n + "RD";
            default: return n + "TH";
        }
    }

    void Clear()
    {
        foreach (var r in rows) Fill(r, null, false);
        gap.gameObject.SetActive(false);
    }

    void Fill(Row r, ApiClient.EntryDto e, bool mine)
    {
        bool has = e != null && e.Valid;
        r.plate.gameObject.SetActive(has);
        r.rank.gameObject.SetActive(has);
        r.name.gameObject.SetActive(has);
        r.id.gameObject.SetActive(has);
        r.souls.gameObject.SetActive(has);
        r.cages.gameObject.SetActive(has);
        r.nights.gameObject.SetActive(has);
        r.score.gameObject.SetActive(has);
        if (!has) return;

        Color rankColor = e.rank == 1 ? new Color(0.95f, 0.76f, 0.49f, 1f) : e.rank == 2 ? new Color(0.83f, 0.82f, 0.79f, 1f) : e.rank == 3 ? new Color(0.82f, 0.56f, 0.38f, 1f) : OnlineUi.Bone;
        r.rank.text = Ordinal(e.rank);
        r.rank.color = rankColor;
        r.name.text = e.codename + (mine ? "   YOU" : "");
        r.name.color = mine ? OnlineUi.Ok : OnlineUi.Bone;
        r.id.text = e.agentId;
        r.souls.text = e.captures.ToString();
        r.cages.text = e.cages.ToString();
        r.nights.text = e.nights.ToString();
        r.score.text = e.score.ToString("0000000");
        r.score.color = e.rank <= 3 ? rankColor : OnlineUi.Bone;

        Color plate = e.rank == 1 ? new Color(0.94f, 0.66f, 0.30f, 0.16f) : new Color(1f, 1f, 1f, e.rank % 2 == 0 ? 0.035f : 0f);
        if (mine) plate = new Color(OnlineUi.Ok.r, OnlineUi.Ok.g, OnlineUi.Ok.b, 0.1f);
        r.plate.color = plate;
        r.edge.color = mine ? new Color(OnlineUi.Ok.r, OnlineUi.Ok.g, OnlineUi.Ok.b, 0.6f) : (e.rank == 1 ? new Color(0.94f, 0.66f, 0.30f, 0.45f) : new Color(1f, 1f, 1f, 0f));
    }

    void Load()
    {
        if (loading) return;
        loading = true;
        Clear();
        status.text = "LOADING";

        ApiClient.Get("/api/leaderboard?limit=" + Rows, r =>
        {
            loading = false;
            if (!r.ok)
            {
                status.text = "THE BOARD IS NOT REACHABLE";
                return;
            }

            ApiClient.BoardAnswer board = null;
            try { board = JsonUtility.FromJson<ApiClient.BoardAnswer>(r.text); } catch (System.Exception) { }
            if (board == null || !board.ok || board.entries == null)
            {
                status.text = "THE BOARD IS NOT REACHABLE";
                return;
            }

            if (board.entries.Count == 0)
            {
                status.text = "NO SCORES YET. BE THE FIRST AGENT";
                return;
            }

            status.text = "";
            bool meShown = false;
            for (int i = 0; i < Rows && i < board.entries.Count; i++)
            {
                var e = board.entries[i];
                bool mine = board.me != null && board.me.Valid && board.me.agentId == e.agentId;
                if (mine) meShown = true;
                Fill(rows[i], e, mine);
            }

            if (board.me != null && board.me.Valid && !meShown)
            {
                gap.gameObject.SetActive(true);
                Fill(rows[Rows], board.me, true);
            }
        });
    }
}
