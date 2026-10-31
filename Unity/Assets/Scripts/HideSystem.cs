using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The player can HIDE from the Solid ghost: under a bed, in a wardrobe or in a cupboard.
// Look at the place (close to it) and press H (the interact button on a phone): the player goes inside and cannot move,
// the screen shows only a thin slit. The ghost does not see a hidden player: if she is chasing, she goes to the place,
// looks around for a few seconds (GhostSpawner > Search Seconds) and gives up. Press H again to come out.
// You cannot hide when she is right behind you (closer than Too Close To Ghost).
// It makes itself in the game scene: the places are found by name (bed1, bed2... / cabinet12... / smallcabinet...)
// and size (a wardrobe or cupboard must be tall enough to stand in).
public class HideSystem : MonoBehaviour
{
    public static bool PlayerHidden { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        PlayerHidden = false;
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
        if (FindFirstObjectByType<PlayerController>() == null || FindFirstObjectByType<GhostSpawner>() == null) return;
        if (FindFirstObjectByType<HideSystem>() != null) return;

        new GameObject("Hide System (auto)").AddComponent<HideSystem>();
    }

    public enum Kind { Bed, Wardrobe, Cupboard }

    public class Spot
    {
        public Kind kind;
        public Bounds bounds;
        public string name;
        public Transform t;
    }

    [Header("Keys")]
    public KeyCode hideKey = KeyCode.H;

    [Header("Places")]
    [Tooltip("How close the player must be to a place to hide in it (meters)")]
    public float lookReach = 3.8f;
    [Tooltip("A cabinet is a wardrobe or a cupboard only if it is at least this tall (meters)")]
    public float minCabinetHeight = 1.35f;
    [Tooltip("A cabinet this tall or taller is called a wardrobe, a lower one a cupboard (meters)")]
    public float wardrobeHeight = 1.7f;

    [Header("Hiding")]
    [Tooltip("Cannot hide when she is hunting the player and is closer than this (meters)")]
    public float tooCloseToGhost = 3f;
    [Tooltip("The camera height under a bed (meters above the floor)")]
    public float bedCameraHeight = 0.3f;

    private readonly List<Spot> spots = new List<Spot>();
    private PlayerController playerController;
    private Transform player;
    private Transform cam;
    private GhostSpawner spawner;

    private Spot current;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private float savedPitch;

    private CanvasGroup overlay;
    private float overlayAlpha;
    private float messageUntil;
    private string message;

    public static HideSystem Instance { get; private set; }

    // True when a box touches a hiding place (the training keeps these when it clears the furniture)
    public bool OverlapsSpot(Bounds b)
    {
        foreach (var s in spots)
            if (s.bounds.Intersects(b)) return true;
        return false;
    }

    // The middle of the hiding place nearest to a point (used by the training to point at one)
    public bool TryNearestSpot(Vector3 from, out Vector3 point)
    {
        point = Vector3.zero;
        float best = float.MaxValue;
        bool found = false;
        foreach (var s in spots)
        {
            float d = (s.bounds.center - from).sqrMagnitude;
            if (d < best) { best = d; point = s.bounds.center; found = true; }
        }
        return found;
    }

    void Start()
    {
        Instance = this;
        playerController = FindFirstObjectByType<PlayerController>();
        spawner = FindFirstObjectByType<GhostSpawner>();
        if (playerController == null) { enabled = false; return; }

        player = playerController.transform;
        cam = playerController.playerCamera != null ? playerController.playerCamera : (Camera.main != null ? Camera.main.transform : null);

        FindSpots();
        BuildOverlay();
        Debug.Log("[Hide] " + spots.Count + " hiding place(s) found (beds, wardrobes, cupboards)");
    }

    // ---------------------------------------------------------------- the places

    void FindSpots()
    {
        spots.AddRange(Scan(player, minCabinetHeight, wardrobeHeight, extraNames, findWoodCupboards));
        foreach (var spot in spots)
            Debug.Log("[Hide] place: " + spot.name + " (" + spot.kind + ", " + spot.bounds.size.ToString("F1") + ")");
    }

    [Header("More places")]
    [Tooltip("Names of extra objects that can be hidden in (a wardrobe or cupboard that has no name like 'cabinet12')")]
    public string[] extraNames = { "polySurface185", "polySurface186" };
    [Tooltip("Also: a tall, box-shaped 'polySurface' with the wood03 material (the big dark cupboards) is a hiding place")]
    public bool findWoodCupboards = true;

    // Every hiding place of the open scene (the editor tool Tools > Ghost > Check Hiding Places uses it too)
    public static List<Spot> Scan(Transform ignore, float minCabinetHeight, float wardrobeHeight, string[] extraNames, bool findWoodCupboards)
    {
        var result = new List<Spot>();
        var done = new HashSet<Transform>();

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (ignore != null && r.transform.IsChildOf(ignore)) continue;

            string n = r.gameObject.name.ToLowerInvariant();
            bool bed = IsNumbered(n, "bed");
            bool cabinet = IsNumbered(n, "cabinet") || IsNumbered(n, "smallcabinet") || IsListed(n, extraNames) ||
                           IsWoodCupboard(r, n, findWoodCupboards, minCabinetHeight);
            if (!bed && !cabinet) continue;
            if (!done.Add(r.transform)) continue;

            Bounds box = r.bounds;
            foreach (var child in r.GetComponentsInChildren<Renderer>())
                if (child != r && !(child is ParticleSystemRenderer)) box.Encapsulate(child.bounds);

            var spot = new Spot { bounds = box, name = r.gameObject.name, t = r.transform };
            if (bed)
            {
                spot.kind = Kind.Bed;
            }
            else
            {
                if (box.size.y < minCabinetHeight || Mathf.Min(box.size.x, box.size.z) < 0.4f) continue;
                spot.kind = box.size.y >= wardrobeHeight ? Kind.Wardrobe : Kind.Cupboard;
            }
            result.Add(spot);
        }
        return result;
    }

    static bool IsListed(string lowerName, string[] extraNames)
    {
        if (extraNames == null) return false;
        foreach (var e in extraNames)
            if (!string.IsNullOrEmpty(e) && e.ToLowerInvariant() == lowerName) return true;
        return false;
    }

    // The big dark cupboards are called polySurface185 and so on; only the ones with the wood03 material, tall and as thick as a cupboard
    static bool IsWoodCupboard(Renderer r, string lowerName, bool enabled, float minHeight)
    {
        if (!enabled || !lowerName.StartsWith("polysurface")) return false;

        bool wood = false;
        foreach (var m in r.sharedMaterials)
            if (m != null && m.name == "GCC_asset_wood03_MAT") { wood = true; break; }
        if (!wood) return false;

        Vector3 size = r.bounds.size;
        return size.y >= minHeight && Mathf.Min(size.x, size.z) >= 0.4f && Mathf.Max(size.x, size.z) <= 2.4f;
    }

    // "bed", "bed3" yes; "bedroom" no
    static bool IsNumbered(string name, string word)
    {
        if (!name.StartsWith(word)) return false;
        for (int i = word.Length; i < name.Length; i++)
            if (!char.IsDigit(name[i])) return false;
        return true;
    }

    static string Prompt(Kind kind)
    {
        switch (kind)
        {
            case Kind.Bed: return "[H] Hide under the bed";
            case Kind.Wardrobe: return "[H] Hide in the wardrobe";
            default: return "[H] Hide in the cupboard";
        }
    }

    // The place the player is looking at, close enough
    Spot LookedAt()
    {
        if (cam == null) return null;

        var ray = new Ray(cam.position, cam.forward);
        Spot best = null;
        float bestDistance = lookReach;

        foreach (var s in spots)
        {
            Bounds b = s.bounds;
            b.Expand(0.3f);
            if (b.IntersectRay(ray, out float d) && d <= bestDistance)
            {
                bestDistance = d;
                best = s;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- hide / come out

    void Update()
    {
        // fades the dark screen in and out
        overlayAlpha = Mathf.MoveTowards(overlayAlpha, PlayerHidden ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        if (overlay != null) overlay.alpha = overlayAlpha;

        if (player == null || cam == null) return;
        if (UiLock.Open || (Inventory.Instance != null && Inventory.Instance.IsOpen)) return;

        bool pressed = Input.GetKeyDown(hideKey);

        if (PlayerHidden)
        {
            InteractPrompt.Request("[H] Come out");
            if (pressed || MobileInput.GetInteract()) ComeOut();
            return;
        }

        Spot spot = LookedAt();
        if (spot != null)
        {
            if (Time.time < messageUntil) InteractPrompt.Request(message);
            else InteractPrompt.Request(Prompt(spot.kind));

            if (pressed || MobileInput.GetInteract())
            {
                if (spawner != null && spawner.IsChasing && spawner.DistanceToPlayer < tooCloseToGhost)
                {
                    message = "She is too close!";
                    messageUntil = Time.time + 1.5f;
                }
                else
                {
                    Hide(spot);
                }
            }
        }
    }

    void Hide(Spot spot)
    {
        current = spot;
        savedPosition = player.position;
        savedRotation = player.rotation;
        savedCameraPosition = cam.localPosition;
        savedCameraRotation = cam.localRotation;
        savedPitch = CameraLookState.Pitch;

        // the same height above the floor as before, but inside the place
        float offset = savedPosition.y - FloorBelow(savedPosition, savedPosition.y);
        Vector3 center = spot.bounds.center;
        float floor = FloorBelow(new Vector3(center.x, spot.bounds.min.y + 0.3f, center.z), spot.bounds.min.y);
        Vector3 inside = new Vector3(center.x, floor + offset, center.z);

        Vector3 outward = savedPosition - inside;
        outward.y = 0f;
        Quaternion facing = outward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(outward) : savedRotation;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.SetPositionAndRotation(inside, facing);
        if (controller != null) controller.enabled = true;

        playerController.enabled = false;

        float pitch = spot.kind == Kind.Bed ? -8f : 0f;
        Vector3 local = savedCameraPosition;
        if (spot.kind == Kind.Bed) local.y = bedCameraHeight;
        cam.localPosition = local;
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        CameraLookState.Pitch = pitch;

        PlayerHidden = true;
        Debug.Log("[Hide] The player hides (" + spot.kind + " '" + spot.name + "')");
    }

    void ComeOut()
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.SetPositionAndRotation(savedPosition, savedRotation);
        if (controller != null) controller.enabled = true;

        cam.localPosition = savedCameraPosition;
        cam.localRotation = savedCameraRotation;
        CameraLookState.Pitch = savedPitch;

        playerController.enabled = true;
        PlayerHidden = false;
        current = null;
    }

    void OnDisable()
    {
        if (PlayerHidden && player != null) ComeOut();
        PlayerHidden = false;
    }

    public static float FloorBelow(Vector3 from, float fallback)
    {
        if (Physics.Raycast(from + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return fallback;
    }

    // ---------------------------------------------------------------- the dark screen with a thin slit

    void BuildOverlay()
    {
        var canvasGo = new GameObject("Hide Overlay");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;     // under the HUD

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        overlay = canvasGo.AddComponent<CanvasGroup>();
        overlay.alpha = 0f;
        overlay.blocksRaycasts = false;
        overlay.interactable = false;

        MakeBar(canvasGo.transform, "Top", new Vector2(0f, 0.57f), new Vector2(1f, 1f));
        MakeBar(canvasGo.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0.43f));
        MakeBar(canvasGo.transform, "Left", new Vector2(0f, 0.43f), new Vector2(0.12f, 0.57f));
        MakeBar(canvasGo.transform, "Right", new Vector2(0.88f, 0.43f), new Vector2(1f, 0.57f));
    }

    static void MakeBar(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.96f);
        image.raycastTarget = false;
    }
}
