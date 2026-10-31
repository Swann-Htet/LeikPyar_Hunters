using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// SKIP TO 6 PM: a button at the top of the screen (key N on a computer). While it is day (6 AM - 6 PM) it jumps the game clock to 6:00 PM,
// so the player does not have to wait for the evening. It hides itself at night and in the training.
public class SkipDayButton : MonoBehaviour
{
    public const int TargetHour = 18;

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.82f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.97f);

    private WallClockController clock;
    private GameObject button;
    private bool phone;
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
        if (FindFirstObjectByType<PlayerController>() == null || FindFirstObjectByType<WallClockController>() == null) return;
        if (FindFirstObjectByType<SkipDayButton>() != null) return;
        new GameObject("Skip Day Button (auto)").AddComponent<SkipDayButton>();
    }

    void Start()
    {
        clock = FindFirstObjectByType<WallClockController>();

        var canvasGo = new GameObject("SkipDayCanvas");
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

        phone = DeviceDetection.IsMobile();
        var size = new Vector2(268f, 50f);
        var plate = UiKit.Shape(canvasGo.transform, "Skip Day", UiKit.Round, Plate, size.y * 0.5f, Vector2.zero, size);
        plate.raycastTarget = true;
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, phone ? -116f : -96f);       // under the FPS line
        button = plate.gameObject;

        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, size.y * 0.5f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);
        UiKit.IconImage(plate.transform, "Icon", "clock", Bone, new Vector2(-108f, 0f), 24f);
        var label = UiKit.Label(plate.transform, "Label", "SKIP TO 6 PM", UiKit.DmSemi, 21f, Bone, TextAlignmentOptions.Center, new Vector2(10f, 0f), new Vector2(200f, size.y));
        label.characterSpacing = 2f;
        label.raycastTarget = false;
        if (DeviceDetection.KeyHints) HudKeys.CornerKey(plate.transform, "N", 26f, new Vector2(-2f, 4f));

        var click = button.AddComponent<Button>();
        click.targetGraphic = plate;
        var colors = click.colors;
        colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        click.colors = colors;
        click.onClick.AddListener(Skip);

        button.SetActive(false);
    }

    void Update()
    {
        if (clock == null || button == null) return;

        // only by day: from 6 AM to 6 PM, and not in the training or inside a hiding place or a panel
        float hour = clock.GameHour24;
        bool day = hour >= 6f && hour < TargetHour;
        bool show = day && !TutorialMode.Active && !UiLock.Open && !HideSystem.PlayerHidden && (Inventory.Instance == null || !Inventory.Instance.IsOpen);
        if (button.activeSelf != show) button.SetActive(show);

        // N on a computer
        if (show && DeviceDetection.KeyHints && Input.GetKeyDown(KeyCode.N)) Skip();

        if (Time.unscaledTime < toastUntil) InteractPrompt.Request("It is 6 PM");
    }

    void Skip()
    {
        if (clock == null) return;
        if (clock.GameHour24 >= TargetHour) return;

        clock.SetGameTime(TargetHour, 0);
        toastUntil = Time.unscaledTime + 2.5f;
        button.SetActive(false);
        Debug.Log("[Clock] The day was skipped: it is 6 PM");
    }
}
