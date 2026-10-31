using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The AGENT REGISTRY screen: sign in or create an agent (an Agent ID is given), and the Agent card when signed in.
// The same account works on the website. Open it with AccountScreen.Open(). Esc closes it.
public class AccountScreen : MonoBehaviour
{
    private static AccountScreen instance;

    public static void Open()
    {
        if (instance == null)
        {
            var go = new GameObject("Account Screen");
            instance = go.AddComponent<AccountScreen>();
        }
        instance.Show();
    }

    public static bool IsOpen { get { return instance != null && instance.panel != null && instance.panel.activeSelf; } }

    private GameObject panel;
    private GameObject formView, cardView;
    private bool registerMode;
    private TMP_InputField nameField, passField;
    private TMP_Text message, submitLabel, tabLogin, tabRegister;
    private Image underLogin, underRegister;
    private TMP_Text cardId, cardName, cardRank, cardScore, cardNights;
    private Button submit;
    private bool busy;

    // Male / Female
    private string pickedGender = "";
    private GameObject genderRow, askView;
    private Image maleFill, femaleFill;
    private TMP_Text cardGender, askMessage;
    private RectTransform messageRect, submitRect;
    private bool captchaOn;

    void Awake()
    {
        Build();
        panel.SetActive(false);
    }

    void OnEnable()
    {
        AgentAccount.Changed += Refresh;
    }

    void OnDisable()
    {
        AgentAccount.Changed -= Refresh;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (IsOpen && formView.activeSelf && Input.GetKeyDown(KeyCode.Tab) && nameField != null)
        {
            if (nameField.isFocused) passField.ActivateInputField();
            else nameField.ActivateInputField();
        }
        if (IsOpen && formView.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Submit();
    }

    void Show()
    {
        panel.SetActive(true);
        message.text = "";
        Refresh();
        if (!AgentAccount.Checked) AgentAccount.Refresh();

        // does the server ask for a robot check (reCAPTCHA) when an agent is created? A game window can not show it: the website does
        ApiClient.Get("/api/config", r =>
        {
            if (!r.ok) return;
            var config = JsonUtility.FromJson<ApiClient.ConfigAnswer>(r.text);
            captchaOn = config != null && config.captcha;
            SetMode(registerMode);
        });
    }

    void Close()
    {
        panel.SetActive(false);
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        panel = OnlineUi.MakeScreen(transform, "Account", 105, 0.9f);
        var root = panel.transform;

        var plate = HudStyler.MakePlate(root, "Registry", 24f, new Vector2(860f, 760f));

        var title = OnlineUi.Text(plate.transform, "AGENT REGISTRY", 46f, OnlineUi.Blood, TextAlignmentOptions.Center, new Vector2(0f, 318f), new Vector2(760f, 70f), true);
        title.characterSpacing = 8f;
        var sub = OnlineUi.Text(plate.transform, "ONE ACCOUNT FOR THE GAME AND THE WEBSITE", 19f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, 268f), new Vector2(760f, 30f));
        sub.characterSpacing = 4f;

        var close = OnlineUi.MakeButton(plate.transform, "X", new Vector2(380f, 330f), new Vector2(64f, 64f), Close, false);
        close.GetComponentInChildren<TMP_Text>().text = "X";

        BuildForm(plate.transform);
        BuildCard(plate.transform);
    }

    void BuildForm(Transform parent)
    {
        var holder = UiKit.NewRect("Form", parent);
        UiKit.Stretch(holder);
        formView = holder.gameObject;

        // the two tabs
        var b1 = OnlineUi.MakeButton(holder, "SIGN IN", new Vector2(-170f, 205f), new Vector2(330f, 60f), () => SetMode(false), false);
        var b2 = OnlineUi.MakeButton(holder, "CREATE AGENT", new Vector2(170f, 205f), new Vector2(330f, 60f), () => SetMode(true), false);
        tabLogin = b1.GetComponentInChildren<TMP_Text>();
        tabRegister = b2.GetComponentInChildren<TMP_Text>();
        underLogin = UiKit.Shape(b1.transform, "Under", UiKit.Round, OnlineUi.Blood, 2f, new Vector2(0f, -30f), new Vector2(300f, 4f));
        underRegister = UiKit.Shape(b2.transform, "Under", UiKit.Round, OnlineUi.Blood, 2f, new Vector2(0f, -30f), new Vector2(300f, 4f));
        underLogin.raycastTarget = underRegister.raycastTarget = false;

        var l1 = OnlineUi.Text(holder, "CODENAME", 20f, OnlineUi.Dim, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 150f), new Vector2(640f, 28f));
        l1.characterSpacing = 4f;
        nameField = OnlineUi.MakeInput(holder, "3 to 16 characters", false, 16, new Vector2(0f, 100f), new Vector2(640f, 64f));

        var l2 = OnlineUi.Text(holder, "PASSWORD", 20f, OnlineUi.Dim, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 42f), new Vector2(640f, 28f));
        l2.characterSpacing = 4f;
        passField = OnlineUi.MakeInput(holder, "6 characters or more", true, 64, new Vector2(0f, -8f), new Vector2(640f, 64f));

        // MALE / FEMALE (only when an agent is created)
        var row = UiKit.NewRect("Gender", holder);
        UiKit.Stretch(row);
        genderRow = row.gameObject;
        var l3 = OnlineUi.Text(row, "GENDER", 20f, OnlineUi.Dim, TextAlignmentOptions.MidlineLeft, new Vector2(0f, -64f), new Vector2(640f, 28f));
        l3.characterSpacing = 4f;
        var male = OnlineUi.MakeButton(row, "MALE", new Vector2(-165f, -112f), new Vector2(310f, 56f), () => PickGender("male"), false);
        var female = OnlineUi.MakeButton(row, "FEMALE", new Vector2(165f, -112f), new Vector2(310f, 56f), () => PickGender("female"), false);
        maleFill = (Image)male.targetGraphic;
        femaleFill = (Image)female.targetGraphic;
        ShowGender();

        message = OnlineUi.Text(holder, "", 22f, OnlineUi.Blood, TextAlignmentOptions.Center, new Vector2(0f, -166f), new Vector2(760f, 34f), false, true);
        messageRect = message.rectTransform;

        submit = OnlineUi.MakeButton(holder, "SIGN IN", new Vector2(0f, -230f), new Vector2(640f, 72f), Submit, true);
        submitLabel = submit.GetComponentInChildren<TMP_Text>();
        submitRect = (RectTransform)submit.transform;

        var foot = OnlineUi.Text(holder, "leikpyarhunters.org", 20f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, -330f), new Vector2(600f, 30f), false, true);
        foot.characterSpacing = 3f;

        SetMode(false);
    }

    void BuildCard(Transform parent)
    {
        var holder = UiKit.NewRect("Card", parent);
        UiKit.Stretch(holder);
        cardView = holder.gameObject;

        var top = OnlineUi.Text(holder, "SPECTRAL AGENT", 22f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, 190f), new Vector2(640f, 30f));
        top.characterSpacing = 8f;

        cardId = OnlineUi.Text(holder, "AGT-000000", 84f, OnlineUi.Amber, TextAlignmentOptions.Center, new Vector2(0f, 110f), new Vector2(760f, 110f), true, true);
        cardId.characterSpacing = 6f;
        cardName = OnlineUi.Text(holder, "Codename", 44f, OnlineUi.Bone, TextAlignmentOptions.Center, new Vector2(0f, 30f), new Vector2(760f, 64f), false, true);
        cardGender = OnlineUi.Text(holder, "", 20f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, -8f), new Vector2(400f, 28f));
        cardGender.characterSpacing = 6f;

        string[] labels = { "RANK", "BEST SCORE", "NIGHTS" };
        var values = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 250f;
            OnlineUi.Plate(holder, "Stat", new Vector2(x, -80f), new Vector2(228f, 112f), new Color(0.03f, 0.02f, 0.015f, 0.9f), true);
            var l = OnlineUi.Text(holder, labels[i], 17f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(x, -52f), new Vector2(220f, 26f));
            l.characterSpacing = 3f;
            values[i] = OnlineUi.Text(holder, "-", 40f, OnlineUi.Bone, TextAlignmentOptions.Center, new Vector2(x, -96f), new Vector2(220f, 56f), true, true);
        }
        cardRank = values[0]; cardScore = values[1]; cardNights = values[2];

        OnlineUi.MakeButton(holder, "LEADERBOARD", new Vector2(-170f, -250f), new Vector2(330f, 72f), () => { Close(); LeaderboardScreen.Open(); }, false);
        OnlineUi.MakeButton(holder, "SIGN OUT", new Vector2(170f, -250f), new Vector2(330f, 72f), SignOut, true);

        // an agent made before Male / Female existed must choose it before the card is shown
        var ask = UiKit.NewRect("Choose Gender", parent);
        UiKit.Stretch(ask);
        askView = ask.gameObject;
        var askTitle = OnlineUi.Text(ask, "CHOOSE YOUR GENDER", 44f, OnlineUi.Bone, TextAlignmentOptions.Center, new Vector2(0f, 150f), new Vector2(760f, 64f), true);
        askTitle.characterSpacing = 5f;
        var askSub = OnlineUi.Text(ask, "Choose Male or Female to continue.", 22f, OnlineUi.Dim, TextAlignmentOptions.Center, new Vector2(0f, 90f), new Vector2(720f, 34f));
        OnlineUi.MakeButton(ask, "MALE", new Vector2(-175f, -10f), new Vector2(330f, 96f), () => SaveGender("male"), true);
        OnlineUi.MakeButton(ask, "FEMALE", new Vector2(175f, -10f), new Vector2(330f, 96f), () => SaveGender("female"), true);
        askMessage = OnlineUi.Text(ask, "", 22f, OnlineUi.Blood, TextAlignmentOptions.Center, new Vector2(0f, -100f), new Vector2(760f, 34f), false, true);
        OnlineUi.MakeButton(ask, "SIGN OUT", new Vector2(0f, -250f), new Vector2(330f, 72f), SignOut, false);
        askView.SetActive(false);
    }

    // ---------------------------------------------------------------- behaviour

    void SetMode(bool register)
    {
        registerMode = register;
        if (submitLabel != null) submitLabel.text = register ? (captchaOn ? "CREATE ON THE WEBSITE" : "CREATE MY AGENT ID") : "SIGN IN";
        if (tabLogin != null) tabLogin.color = register ? OnlineUi.Dim : OnlineUi.Bone;
        if (tabRegister != null) tabRegister.color = register ? OnlineUi.Bone : OnlineUi.Dim;
        if (underLogin != null) underLogin.enabled = !register;
        if (underRegister != null) underRegister.enabled = register;
        if (message != null) message.text = "";

        // MALE / FEMALE is asked when an agent is created (also when the robot check is done on the website: the choice goes with the player)
        if (genderRow != null) genderRow.SetActive(register);
        if (messageRect != null) messageRect.anchoredPosition = new Vector2(0f, register ? -166f : -88f);
        if (submitRect != null) submitRect.anchoredPosition = new Vector2(0f, register ? -230f : -152f);
    }

    void PickGender(string value)
    {
        pickedGender = value;
        ShowGender();
    }

    // the chosen button is red, the other one dark
    void ShowGender()
    {
        var on = new Color(0.62f, 0.08f, 0.12f, 1f);
        var off = new Color(0.05f, 0.04f, 0.05f, 0.94f);
        if (maleFill != null) maleFill.color = pickedGender == "male" ? on : off;
        if (femaleFill != null) femaleFill.color = pickedGender == "female" ? on : off;
    }

    // for an old agent: Male / Female is saved on the account
    void SaveGender(string value)
    {
        if (busy) return;
        busy = true;
        askMessage.text = "";
        AgentAccount.SetGender(value, (ok, error) =>
        {
            busy = false;
            if (!ok) { askMessage.text = error; return; }
            Refresh();
        });
    }

    void Refresh()
    {
        if (panel == null) return;
        bool signed = AgentAccount.SignedIn;
        bool ask = AgentAccount.NeedsGender;
        formView.SetActive(!signed);
        cardView.SetActive(signed && !ask);
        askView.SetActive(ask);
        if (!signed) return;

        var a = AgentAccount.Agent;
        cardId.text = a.agentId;
        cardName.text = a.codename;
        cardGender.text = a.gender == "female" ? "FEMALE" : a.gender == "male" ? "MALE" : "";
        cardRank.text = a.rank > 0 ? "#" + a.rank : "-";
        cardScore.text = a.bestScore.ToString("N0");
        cardNights.text = a.runs.ToString("N0");
    }

    void Submit()
    {
        if (busy) return;

        // the robot check lives on the website: it opens there, and the agent signs in here afterwards
        if (registerMode && captchaOn)
        {
            if (string.IsNullOrEmpty(pickedGender)) { Say("Choose Male or Female", true); return; }
            // the website opens on the Create agent form with the chosen gender already marked
            Application.OpenURL(ApiClient.SiteUrl + "?register=1&gender=" + pickedGender + "#agent");
            Say("Do the robot check on the website, then sign in here", false);
            return;
        }

        string name = nameField.text.Trim();
        string pass = passField.text;
        if (name.Length < 3) { Say("The codename needs at least 3 characters", true); return; }
        if (pass.Length < 6) { Say("The password needs at least 6 characters", true); return; }
        if (registerMode && string.IsNullOrEmpty(pickedGender)) { Say("Choose Male or Female", true); return; }

        busy = true;
        Say(registerMode ? "Creating your Agent ID" : "Signing in", false);
        System.Action<bool, string> done = (ok, error) =>
        {
            busy = false;
            if (!ok) { Say(error, true); return; }
            passField.text = "";
            Say("", false);
            Refresh();
        };
        if (registerMode) AgentAccount.Register(name, pass, pickedGender, done);
        else AgentAccount.Login(name, pass, done);
    }

    void SignOut()
    {
        AgentAccount.Logout();
    }

    void Say(string text, bool error)
    {
        if (message == null) return;
        message.color = error ? OnlineUi.Blood : OnlineUi.Dim;
        message.text = text;
    }
}
