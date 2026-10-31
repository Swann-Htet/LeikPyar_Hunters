using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The key of every tool, shown next to its icon (on a PC):
//   - bottom left: Bag [B], Torch [T],  each a small plate with the icon and the key at its corner.
//     The plate lights up while the bag is open, the torch is on, the scanner is switched on.
//   - a small key under the settings gear [P] and under the guide button [K].
// A phone has no keys: its buttons already show the icons. It makes itself in the game scene.
public class HudKeys : MonoBehaviour
{
    [Header("Bottom left (canvas units, from the corner)")]
    public Vector2 position = new Vector2(34f, 28f);
    public float chipSize = 72f;
    public float spacing = 12f;

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.72f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Blood = new Color(0.86f, 0.12f, 0.16f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.96f);

    private class Chip
    {
        public Image edge;
        public Image icon;
        public TMP_Text key;
        public System.Func<bool> lit;
    }

    private readonly System.Collections.Generic.List<Chip> chips = new System.Collections.Generic.List<Chip>();
    private Transform canvasRoot;
    private bool gearKey, guideKey;
    private GhostDevice scanner;
    private float startedAt;

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
        if (FindFirstObjectByType<PlayerController>() == null || FindFirstObjectByType<HudKeys>() != null) return;
        new GameObject("Hud Keys (auto)").AddComponent<HudKeys>();
    }

    void Start()
    {
        if (!DeviceDetection.KeyHints) { enabled = false; return; }     // a phone has no keys

        scanner = FindFirstObjectByType<GhostDevice>();
        startedAt = Time.unscaledTime;

        var canvasGo = new GameObject("HudKeysCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 67;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<HorrorSkinIgnore>();
        canvasRoot = canvasGo.transform;

        // the bag and torch plates bottom left are for a computer (the phone controls have their own buttons for them)
        if (!DeviceDetection.IsMobile())
        {
            AddChip(0, "Bag", "B", () => Inventory.Instance != null && Inventory.Instance.IsOpen);
            AddChip(1, "Torch", "T", () => Torch.IsOn);
        }
    }

    void AddChip(int index, string iconName, string key, System.Func<bool> lit)
    {
        var chip = new Chip { lit = lit };

        var plate = UiKit.Shape(canvasRoot, "Chip " + key, UiKit.Round, Plate, 14f, Vector2.zero, new Vector2(chipSize, chipSize));
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = position + new Vector2(index * (chipSize + spacing), 0f);

        chip.edge = UiKit.Shape(plate.transform, "Edge", UiKit.RoundOutline, Edge, 14f, Vector2.zero, new Vector2(chipSize, chipSize));
        UiKit.Stretch(chip.edge.rectTransform);

        chip.icon = UiKit.IconImage(plate.transform, "Icon", iconName, Bone, Vector2.zero, chipSize * 0.56f);

        chip.key = CornerKey(plate.transform, key, chipSize * 0.38f, new Vector2(4f, 8f));
        chips.Add(chip);
    }

    // The key plate of the design: a small dark square with a red edge and the letter, sitting on the bottom right corner of its
    // parent (over the corner a little). Used by the chips here, the hotbar slots (1 2 3) and the K / P buttons.
    public static TMP_Text CornerKey(Transform parent, string key, float size, Vector2 inset)
    {
        var badge = UiKit.Shape(parent, "Key " + key, UiKit.Round, new Color(0.02f, 0.02f, 0.025f, 0.95f), 8f, Vector2.zero, new Vector2(size, size));
        var rt = badge.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-inset.x, inset.y);
        badge.raycastTarget = false;

        var edge = UiKit.Shape(badge.transform, "Edge", UiKit.RoundOutline, Edge, 8f, Vector2.zero, new Vector2(size, size));
        UiKit.Stretch(edge.rectTransform);

        var text = UiKit.Label(badge.transform, "Letter", key, UiKit.DmSemi, size * 0.7f, Bone, TextAlignmentOptions.Center, Vector2.zero, new Vector2(size, size));
        text.raycastTarget = false;
        text.gameObject.AddComponent<LocalizeIgnore>();
        return text;
    }

    void Update()
    {
        foreach (var chip in chips)
        {
            bool on = chip.lit != null && chip.lit();
            chip.edge.color = on ? Blood : Edge;
            chip.icon.color = on ? Color.white : new Color(Bone.r, Bone.g, Bone.b, 0.7f);
        }

        // the gear and the guide button are made a moment after the scene starts
        if (!gearKey && SettingsPanel.Instance != null && SettingsPanel.Instance.gearButton != null)
        {
            // on the round plate the HUD styler puts behind the gear (it is made a moment after the scene starts)
            var back = SettingsPanel.Instance.gearButton.transform.parent.Find("GearBack");
            if (back != null)
            {
                CornerKey(back, "P", 28f, new Vector2(2f, 10f));
                gearKey = true;
            }
        }
        if (!guideKey && Time.unscaledTime - startedAt < 8f)
        {
            var guide = GameObject.Find("Guide Button");
            if (guide != null)
            {
                CornerKey(guide.transform, "K", 28f, new Vector2(2f, 10f));
                guideKey = true;
            }
        }
    }
}
