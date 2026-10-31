using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// All the touch controls of a phone, in the horror look of the HUD:
//   - a full-screen LOOK pad (smooth camera, see MobileCameraLook) behind every button, which replaces the hand-made
//     "CameraTouchArea" objects (they are switched off)
//   - GUN (G on a PC) and JUMP (space on a PC) buttons next to the interact button
//   - the BAG, TORCH, DROP and UPGRADE buttons made by the other scripts are put in one tidy column under the settings gear
// It is made by itself in the game scene, and shows only when the game runs on a phone, in a mobile browser, in the
// Device Simulator or with Tools > Ghost > Mobile UI In Editor ticked. Nothing shows on a PC.
public class MobileControls : MonoBehaviour
{
    [Header("Buttons (canvas units, from the center of the screen)")]
    public Vector2 gunPosition = new Vector2(640f, -402f);
    public Vector2 jumpPosition = new Vector2(690f, -235f);
    [Tooltip("The scanner (radio) button: R on a PC. Left of the GUN button")]
    public Vector2 scannerPosition = new Vector2(500f, -402f);
    [Tooltip("The power button: O on a PC. Shows only while the scanner is out")]
    public Vector2 powerPosition = new Vector2(500f, -285f);
    public float buttonSize = 124f;

    [Header("Right column under the gear")]
    public float columnTop = -262f;
    public float columnSize = 100f;
    public float columnGap = 10f;

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.66f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 1f);       // dark red
    static readonly Color Blood = new Color(0.70f, 0.08f, 0.12f, 1f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 1f);

    private Canvas canvas;
    private GameObject root;
    private GameObject lookPad;
    private Image scannerEdge;
    private Image powerEdge;
    private RectTransform powerButton;
    private GhostDevice scanner;
    private Image gunEdge;
    private Image gunIcon;
    private bool built;

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
        if (FindFirstObjectByType<PlayerController>() == null) return;     // only the game scene
        if (FindFirstObjectByType<MobileControls>() != null) return;

        new GameObject("Mobile Controls (auto)").AddComponent<MobileControls>();
    }

    void Start()
    {
        if (DeviceDetection.IsMobile()) FitCanvases();
        building = true;
        StartCoroutine(Build());
    }

    IEnumerator Build()
    {
        // Let the other scripts make their own buttons first
        yield return null;
        yield return new WaitForSecondsRealtime(0.8f);

        if (!DeviceDetection.IsMobile()) { building = false; yield break; }     // a PC: nothing to add (the editor can switch later: see Update)

        var joystick = FindFirstObjectByType<Joystick>(FindObjectsInactive.Include);
        var interact = FindFirstObjectByType<MobileInput>(FindObjectsInactive.Include);

        Canvas host = null;
        if (interact != null) host = interact.GetComponentInParent<Canvas>(true);
        if (host == null && joystick != null) host = joystick.GetComponentInParent<Canvas>(true);
        if (host == null) host = FindFirstObjectByType<Canvas>();
        if (host == null) yield break;

        canvas = host.rootCanvas;

        root = UiKit.NewRect("Mobile Controls", canvas.transform).gameObject;
        UiKit.Stretch((RectTransform)root.transform);
        root.AddComponent<HorrorSkinIgnore>();      // the horror skin must not repaint these buttons

        Sync(true);          // joystick, interact button, BAG / TORCH / DROP: all there, even if they started before the phone was known
        BuildLookPad();
        BuildButtons();
        TidyColumn();
        synced = true;
        built = true;
    }

    void Update()
    {
        // In the editor the device can be switched while the game runs (Device Simulator): build late, or show / hide
        if (!built)
        {
            if (!building && DeviceDetection.IsMobile())
            {
                building = true;
                StartCoroutine(Build());
            }
            return;
        }

        bool mobile = DeviceDetection.IsMobile();
        if (root.activeSelf != mobile) root.SetActive(mobile);
        if (lookPad != null && lookPad.activeSelf != mobile) lookPad.SetActive(mobile);
        if (mobile != synced)
        {
            synced = mobile;
            Sync(mobile);
            if (mobile) TidyColumn();
        }

        // the scanner buttons: the ring is red while it is out, the power ring while it is on
        if (scanner != null)
        {
            bool isOut = scanner.IsShown;
            if (scannerEdge != null) scannerEdge.color = isOut ? Blood : Edge;
            if (powerButton != null && powerButton.gameObject.activeSelf != isOut) powerButton.gameObject.SetActive(isOut);
            if (powerEdge != null) powerEdge.color = scanner.IsPowered ? new Color(0.25f, 0.8f, 0.3f, 1f) : Edge;
        }

    }

    private bool building;
    private bool synced;

    // The objects that belong to a phone are on for a phone and off for a PC. They switched themselves when the game started,
    // but the Device Simulator tells the phone a little later, so it is done again here.
    // The main canvas was made with "Constant Pixel Size": it looked right on a 1080 px high screen only. A phone browser
    // draws the game at a different size, so the joystick, the gear and the numbers came out two or three times too big
    // (and a different size on every phone). "Scale With Screen Size" keeps the layout the same on every screen.
    static void FitCanvases()
    {
        foreach (var scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    void Sync(bool mobile)
    {
        if (mobile) FitCanvases();

        foreach (var part in FindObjectsByType<MobileJoystickUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // the hand-made touch areas stay off: the look pad replaces them
            if (part.GetComponent<MobileCameraLook>() != null) continue;
            if (part.gameObject.activeSelf != mobile) part.gameObject.SetActive(mobile);
        }

        var hud = FindFirstObjectByType<HudStyler>();
        if (hud != null) hud.PlaceHeat(mobile);

        if (!mobile) return;

        if (Inventory.Instance != null) Inventory.Instance.EnsureMobileButtons();
        var torch = FindFirstObjectByType<Torch>();
        if (torch != null) torch.EnsureMobileButton();
    }

    // ---------------------------------------------------------------- look pad

    void BuildLookPad()
    {
        var player = FindFirstObjectByType<PlayerController>();
        Transform playerCamera = player != null ? player.playerCamera : null;
        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;

        // the hand-made touch areas: the pad takes over (their sensitivity is kept for the pad)
        float sensitivity = 0.08f;
        foreach (var old in FindObjectsByType<MobileCameraLook>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sensitivity = old.sensitivity;
            old.gameObject.SetActive(false);
        }

        var padRect = UiKit.NewRect("Look Pad", canvas.transform);
        UiKit.Stretch(padRect);
        padRect.SetAsFirstSibling();                       // behind the joystick and every button
        lookPad = padRect.gameObject;
        lookPad.AddComponent<HorrorSkinIgnore>();

        var image = lookPad.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        lookPad.SetActive(false);                          // so the component starts with all its fields set
        var look = lookPad.AddComponent<MobileCameraLook>();
        look.player = player != null ? player.transform : null;
        look.playerCamera = playerCamera;
        look.sensitivity = sensitivity;
        lookPad.SetActive(true);
    }

    // ---------------------------------------------------------------- buttons

    void BuildButtons()
    {
        Image unusedEdge, unusedIcon;
        MakeButton("JumpButton", "chevrons-up", "", jumpPosition, buttonSize * 0.88f, MobileInput.PressJump, out unusedEdge, out unusedIcon);

        // the scanner (the radio): R takes it out, O switches it on, V changes the range
        scanner = FindFirstObjectByType<GhostDevice>();
        if (scanner != null)
        {
            powerButton = MakeButton("PowerButton", "BeanPower", "O", powerPosition, buttonSize * 0.72f, MobileInput.PressPower, out powerEdge, out unusedIcon);
            powerButton.gameObject.SetActive(false);
        }
    }

    RectTransform MakeButton(string name, string icon, string tag, Vector2 position, float size,
        System.Action onPress, out Image edge, out Image iconImage)
    {
        var rt = UiKit.NewRect(name, root.transform);
        UiKit.Place(rt, position, new Vector2(size, size));

        var plate = rt.gameObject.AddComponent<Image>();
        plate.sprite = UiKit.Circle;
        plate.color = Plate;
        plate.raycastTarget = true;

        var ringRt = UiKit.NewRect("Edge", rt);
        UiKit.Stretch(ringRt);
        edge = ringRt.gameObject.AddComponent<Image>();
        edge.sprite = UiKit.Ring;
        edge.color = Edge;
        edge.raycastTarget = false;

        iconImage = UiKit.IconImage(rt, "Icon", icon, new Color(Bone.r, Bone.g, Bone.b, 0.85f), Vector2.zero, size * 0.46f);

        if (!string.IsNullOrEmpty(tag) && UiKit.DmSemi != null)
        {
            var label = UiKit.Label(rt, "Key", tag, UiKit.DmSemi, size * 0.17f, new Color(Bone.r, Bone.g, Bone.b, 0.75f),
                TextAlignmentOptions.Center, new Vector2(0f, -size * 0.34f), new Vector2(size * 0.6f, size * 0.22f));
            label.raycastTarget = false;
        }

        var press = rt.gameObject.AddComponent<MobilePressButton>();
        press.onPress = onPress;
        return rt;
    }

    // ---------------------------------------------------------------- the column on the right

    void TidyColumn()
    {
        // BAG, TORCH, DROP are made by other scripts at the top right: one even column under the gear, an icon each (no words).
        // The UPGRADE button is not wanted on a phone.
        string[] names = { "BagButton", "TorchButton", "DropButton" };
        string[] icons = { "Bag", "Torch", "arrow-down-to-line" };
        float y = columnTop;

        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var button in buttons)
        {
            if (button.name == "UpgradeOpenButton") button.gameObject.SetActive(false);
        }

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform rt = null;
            foreach (var button in buttons)
            {
                if (button.name == names[i]) { rt = (RectTransform)button.transform; break; }
            }
            if (rt == null) continue;

            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(columnSize, columnSize);
            rt.anchoredPosition = new Vector2(-30f, y);
            y -= columnSize + columnGap;

            // the dark plate with a thin dark-red edge, like the other buttons
            var plate = rt.GetComponent<Image>();
            if (plate != null)
            {
                plate.sprite = UiKit.Round;
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = UiKit.Border9 / 16f;
                plate.color = Plate;
            }
            if (rt.Find("Edge") == null)
            {
                var edge = UiKit.Shape(rt, "Edge", UiKit.RoundOutline, Edge, 16f, Vector2.zero, rt.sizeDelta);
                UiKit.Stretch(edge.rectTransform);
            }
            if (rt.GetComponent<HorrorSkinIgnore>() == null) rt.gameObject.AddComponent<HorrorSkinIgnore>();

            // the word goes, an icon takes its place
            foreach (var text in rt.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
            if (rt.Find("Icon") == null)
            {
                var icon = UiKit.IconImage(rt, "Icon", icons[i], new Color(Bone.r, Bone.g, Bone.b, 0.92f), Vector2.zero, columnSize * 0.5f);
                icon.raycastTarget = false;
            }
        }
    }
}
