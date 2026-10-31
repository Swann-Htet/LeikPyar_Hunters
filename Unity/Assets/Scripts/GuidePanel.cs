using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The GUIDE: a round "?" button next to the settings gear (and the K or F1 key) opens a screen with every key of the game
// (or every button on a phone) and a START TRAINING button that opens the training (TutorialMode).
// It makes itself in the game scene.
public class GuidePanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Keys")]
    public KeyCode openKey = KeyCode.K;
    public KeyCode openKeyAlternate = KeyCode.F1;

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.9f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.70f, 0.08f, 0.12f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    // key / button  ->  what it does (English: the game translates them)
    static readonly string[][] PcRows =
    {
        new[] { "W A S D", "Move" },
        new[] { "Mouse", "Look around" },
        new[] { "Shift", "Run" },
        new[] { "Space", "Jump" },
        new[] { "F", "Open doors, use the table" },
        new[] { "E", "Take items" },
        new[] { "B", "Backpack" },
        new[] { "T", "Torch" },
        new[] { "1", "Gun: take out or put away" },
        new[] { "2", "Scanner: take out or put away" },
        new[] { "3", "Injector: take out or put away" },
        new[] { "Left Click", "Pull the ghost, use the injector, drop the held item" },
        new[] { "O", "Scanner: switch on or off" },
        new[] { "H", "Hide under a bed, in a wardrobe or a cupboard" },
        new[] { "U", "Upgrades (at the NPC)" },
        new[] { "P  /  Esc", "Settings" },
        new[] { "K  /  F1", "This guide" },
    };

    static readonly string[][] PhoneRows =
    {
        new[] { "Joystick", "Move" },
        new[] { "Drag", "Look around" },
        new[] { "Hand", "Use, take, pull the ghost, use the injector" },
        new[] { "Jump", "Jump" },
        new[] { "Bottom bar", "Gun, scanner, injector: tap to take out or put away" },
        new[] { "Power", "Scanner: switch on or off" },
        new[] { "BAG", "Backpack" },
        new[] { "Torch", "Torch" },
        new[] { "Gear", "Settings" },
    };

    private GameObject panel;
    private CanvasGroup panelGroup;
    private float previousTimeScale = 1f;
    private bool pausedByUs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        IsOpen = false;
    }

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
        if (FindFirstObjectByType<PlayerController>() == null || FindFirstObjectByType<GuidePanel>() != null) return;
        new GameObject("Guide Panel (auto)").AddComponent<GuidePanel>();
    }

    bool Phone { get { return DeviceDetection.IsMobile(); } }

    void Start()
    {
        var canvasGo = new GameObject("GuideCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();

        BuildButton(canvasGo.transform);
        BuildPanel(canvasGo.transform);
        panel.SetActive(false);
    }

    // ---------------------------------------------------------------- the "?" button

    void BuildButton(Transform parent)
    {
        float inset = Phone ? 28f : 0f;

        var plate = UiKit.Shape(parent, "Guide Button", UiKit.Circle, new Color(0.03f, 0.03f, 0.035f, 0.85f), 0f,
            Vector2.zero, new Vector2(70f, 70f));
        plate.type = Image.Type.Simple;
        plate.raycastTarget = true;

        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-139f, -118f - inset);

        var ring = UiKit.NewRect("Ring", rt);
        UiKit.Stretch(ring);
        var ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = UiKit.Ring;
        ringImage.color = Edge;
        ringImage.raycastTarget = false;

        var label = UiKit.Label(rt, "Question", "?", UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi, 40f, Bone,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(70f, 70f));
        label.raycastTarget = false;
        label.gameObject.AddComponent<LocalizeIgnore>();

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(Toggle);
    }

    // ---------------------------------------------------------------- the screen

    void BuildPanel(Transform parent)
    {
        var plate = HudStyler.MakePlate(parent, "Guide", 24f, new Vector2(1400f, 860f));
        panel = plate.gameObject;
        panelGroup = panel.AddComponent<CanvasGroup>();

        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var title = UiKit.Label(rt, "Title", "GUIDE", UiKit.Heading != null ? UiKit.Heading : UiKit.DmSemi, 58f, Blood,
            TextAlignmentOptions.Center, new Vector2(0f, 360f), new Vector2(900f, 90f));
        title.characterSpacing = 6f;

        var rows = Phone ? PhoneRows : PcRows;
        int perColumn = Mathf.CeilToInt(rows.Length / 2f);
        for (int i = 0; i < rows.Length; i++)
        {
            int column = i / perColumn;
            int row = i % perColumn;
            float x = column == 0 ? -330f : 360f;
            float y = 250f - row * 64f;
            AddRow(rt, rows[i][0], rows[i][1], x, y);
        }

        MakeButton(rt, "START TRAINING", new Vector2(-230f, -355f), new Vector2(400f, 84f), StartTraining);
        MakeButton(rt, "CLOSE", new Vector2(230f, -355f), new Vector2(300f, 84f), Close);
    }

    // A key plate and its meaning (x, y from the middle of the screen)
    void AddRow(Transform parent, string key, string meaning, float x, float y)
    {
        var plate = UiKit.Shape(parent, "Key " + key, UiKit.Round, new Color(0.08f, 0.07f, 0.08f, 1f), 14f, new Vector2(x - 190f, y), new Vector2(190f, 50f));
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 14f, Vector2.zero, new Vector2(190f, 50f));
        UiKit.Stretch(edge.rectTransform);

        var keyLabel = UiKit.Label(plate.transform, "Text", key, UiKit.DmSemi, 26f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(186f, 50f));
        keyLabel.raycastTarget = false;
        keyLabel.gameObject.AddComponent<LocalizeIgnore>();

        var text = UiKit.Label(parent, "Meaning", meaning, UiKit.DmMedium, 27f, Bone, TextAlignmentOptions.MidlineLeft,
            new Vector2(x + 140f, y), new Vector2(560f, 56f));
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    static void MakeButton(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var plate = UiKit.Shape(parent, label + " Button", UiKit.Round, new Color(0.06f, 0.05f, 0.06f, 0.95f), 18f, position, size);
        plate.raycastTarget = true;
        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 18f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        var button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(action);

        var text = UiKit.Label(plate.transform, "Label", label, UiKit.DmSemi, size.y * 0.33f, Bone, TextAlignmentOptions.Center, Vector2.zero, size);
        text.raycastTarget = false;
    }

    // ---------------------------------------------------------------- open / close

    void Update()
    {
        if (TutorialMode.Active && Input.GetKeyDown(openKey)) return;   // in the training the keys are in the task list

        if (Input.GetKeyDown(openKey) || Input.GetKeyDown(openKeyAlternate))
        {
            if (!SettingsPanel.IsOpen && !UpgradePanel.IsOpen) Toggle();
        }
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    void Open()
    {
        if (IsOpen || SettingsPanel.IsOpen || UpgradePanel.IsOpen) return;
        if (Inventory.Instance != null) Inventory.Instance.SetOpen(false);

        IsOpen = true;
        panel.SetActive(true);

        // the game waits, the mouse is free
        if (Time.timeScale > 0f)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByUs = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        panel.SetActive(false);

        if (pausedByUs)
        {
            Time.timeScale = previousTimeScale;
            pausedByUs = false;
        }
        if (!Phone)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void StartTraining()
    {
        Close();
        Time.timeScale = 1f;
        TutorialMode.Load();
    }

    void OnDisable()
    {
        if (IsOpen) Close();
    }
}
