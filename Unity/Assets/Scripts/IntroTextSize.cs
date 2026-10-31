using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Two small buttons in a corner of the intro menu:  A-  100%  A+
// They change the size of the Burmese text (the same setting as Settings > Text Size in the game). It makes itself in the intro scene.
public class IntroTextSize : MonoBehaviour
{
    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.78f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    private TMP_Text value;

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
        if (FindFirstObjectByType<IntroMenu>() == null || FindFirstObjectByType<IntroTextSize>() != null) return;
        new GameObject("Intro Text Size (auto)").AddComponent<IntroTextSize>();
    }

    void Start()
    {
        var canvasGo = new GameObject("TextSizeCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<HorrorSkinIgnore>();

        // the plate, bottom right
        var size = new Vector2(300f, 64f);
        var plate = UiKit.Shape(canvasGo.transform, "Text Size", UiKit.Round, Plate, 28f, Vector2.zero, size);
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 36f);

        var edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 28f, Vector2.zero, size);
        UiKit.Stretch(edge.rectTransform);

        MakeButton(plate.transform, "A-", new Vector2(-105f, 0f), -1);
        MakeButton(plate.transform, "A+", new Vector2(105f, 0f), 1);

        value = UiKit.Label(plate.transform, "Value", "", UiKit.DmSemi, 28f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(120f, 56f));
        value.raycastTarget = false;
        value.gameObject.AddComponent<LocalizeIgnore>();

        Loc.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        Loc.Changed -= Refresh;
    }

    void MakeButton(Transform parent, string label, Vector2 position, int step)
    {
        var button = UiKit.Shape(parent, "Button " + label, UiKit.Round, new Color(0.08f, 0.07f, 0.08f, 1f), 22f, position, new Vector2(80f, 46f));
        button.raycastTarget = true;

        var text = UiKit.Label(button.transform, "Label", label, UiKit.DmSemi, 28f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(80f, 46f));
        text.raycastTarget = false;
        text.gameObject.AddComponent<LocalizeIgnore>();

        var b = button.gameObject.AddComponent<Button>();
        b.targetGraphic = button;
        b.onClick.AddListener(() => Loc.StepTextSize(step));
    }

    void Refresh()
    {
        if (value != null) value.text = Mathf.RoundToInt(Loc.TextSize * 100f) + "%";
    }
}
