using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The light of a lamp in the house. Two kinds:
//   Tube : a spot light under a ceiling lamp (material lightEmissive01_MAT, or an object called "light", "light12"...)
//   Bulb : a warm yellow light shining down, a little scary: it flickers more (the SM_bulb objects, material bulb_emissive_MAT)
// They are made by themselves when the game scene starts (CreateFor), or in the scene with Tools > Ghost > Add Spot Lights To Lamps.
// Every light goes dark with the rest of the house when the power is out (PowerOutage), its glowing material goes dark with it,
// and it flickers a little so the house feels old. Lights far from the player are switched off (speed).
public class LampSpot : MonoBehaviour
{
    public enum Kind { Tube, Bulb }

    const string TubeMaterial = "lightEmissive01_MAT";
    const string BulbMaterial = "bulb_emissive_MAT";

    // Which lamps are in the scene: by their material, or a mesh called "light" / "light12"...
    public static bool TryClassify(Renderer r, out Kind kind)
    {
        kind = Kind.Tube;
        if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;

        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            if (m.name == BulbMaterial) { kind = Kind.Bulb; return true; }
            if (m.name == TubeMaterial) { kind = Kind.Tube; return true; }
        }

        // a mesh called "light", "light12"... is a lamp too (its material is the plain GCC_asset_M02_MAT)
        string n = r.gameObject.name.ToLowerInvariant();
        if (n == "light") return true;
        if (n.StartsWith("light") && n.Length > 5)
        {
            for (int i = 5; i < n.Length; i++)
                if (!char.IsDigit(n[i])) return false;
            return true;
        }
        return false;
    }

    // A mesh called "light", "light12"... with a plain material (it does not glow): it gets the glowing tube material
    public static bool NeedsGlowMaterial(Renderer r)
    {
        if (r == null) return false;
        foreach (var m in r.sharedMaterials)
            if (m != null && (m.name == BulbMaterial || m.name == TubeMaterial || m.name == "Tube_Emissive")) return false;

        string n = r.gameObject.name.ToLowerInvariant();
        if (n == "light") return true;
        if (!n.StartsWith("light") || n.Length <= 5) return false;
        for (int i = 5; i < n.Length; i++)
            if (!char.IsDigit(n[i])) return false;
        return true;
    }

    // The glowing tube look (Resources/Lamps/Tube_Emissive): warm white, like a lit light stick
    public static void ApplyGlowMaterial(Renderer r)
    {
        var glowMaterial = Resources.Load<Material>("Lamps/Tube_Emissive");
        if (glowMaterial == null || !NeedsGlowMaterial(r)) return;

        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) mats[i] = glowMaterial;
        r.sharedMaterials = mats;
    }

    // The light object for one lamp (a child of 'parent'); the caller can register it for Undo
    public static GameObject CreateFor(Renderer r, Kind kind, Transform parent)
    {
        Bounds b = r.bounds;
        var go = new GameObject(kind == Kind.Bulb ? "Bulb Light" : "Lamp Spot Light");
        go.transform.SetParent(parent, true);

        var light = go.AddComponent<Light>();
        if (kind == Kind.Bulb)
        {
            go.transform.position = b.center + Vector3.down * 0.05f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // looks down: the ceiling above it stays dark
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.78f, 0.4f);
            light.intensity = 14f;
            light.range = 7f;
            light.spotAngle = 150f;
            light.innerSpotAngle = 70f;
        }
        else
        {
            go.transform.position = new Vector3(b.center.x, b.min.y - 0.1f, b.center.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 90f;
            light.range = 20f;
            light.spotAngle = 140f;
            light.innerSpotAngle = 60f;
        }
        light.shadows = LightShadows.None;

        var spot = go.AddComponent<LampSpot>();
        spot.kind = kind;
        spot.lamp = light;
        spot.glow = r;
        spot.intensity = light.intensity;
        return go;
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

    // Every lamp that has no light yet gets one when the game scene starts (the lamps that already have a LampSpot are skipped)
    static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<NightFog>() == null) return;                 // only the game scene

        var handled = new HashSet<Renderer>();
        foreach (var existing in Object.FindObjectsByType<LampSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.glow != null) handled.Add(existing.glow);

        Transform parent = null;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (handled.Contains(r) || !TryClassify(r, out Kind kind)) continue;

            if (parent == null) parent = new GameObject("Lamp Lights (auto)").transform;
            CreateFor(r, kind, parent);
        }
    }

    public Kind kind = Kind.Tube;
    public Light lamp;

    [Header("Look (tube lamp: warm and strong, so rooms can be seen)")]
    [Tooltip("On: when the game starts the light gets the values below, whatever was saved in the scene")]
    public bool applyLook = true;
    public Color lampTint = new Color(1f, 0.96f, 0.88f);
    public float tubePower = 55f;
    public float tubeRange = 14f;
    [Range(1f, 179f)] public float lampAngle = 140f;

    [Header("Look (bulb: yellow horror light)")]
    public Color bulbLightColour = new Color(1f, 0.78f, 0.4f);
    public float bulbSoftPower = 9f;
    public float bulbSoftRange = 6f;
    [Range(1f, 179f)] public float bulbLightAngle = 150f;
    [Range(0f, 0.1f)] public float bulbFlicker = 0.02f;
    [Range(0f, 1f)] public float bulbFlickerDip = 0.55f;

    [Tooltip("The lamp's mesh (its glowing material goes dark when the power is out)")]
    public Renderer glow;

    [Header("Light")]
    public float intensity = 25f;
    [Tooltip("How fast it fades when the power goes or comes back")]
    public float fadeSpeed = 8f;
    [Tooltip("Lights further than this from the player fade out softly (meters). Big = you see the lit spots from far away too")]
    public float lightsFarDistance = 45f;
    [Tooltip("...and on a phone")]
    public float phoneLightsFarDistance = 24f;
    [Tooltip("At most this many lamps are on at the same time (the nearest ones). The others fade out softly, they do not pop")]
    public int mostLightsOn = 16;
    [Tooltip("...and on a phone")]
    public int phoneMostLightsOn = 8;

    [Header("Shadows (only the lamps nearest to the player cast them: shadows are slow)")]
    public bool castShadows = true;
    [Tooltip("How many lamps around the player have shadows (PC)")]
    public int shadowedLamps = 3;
    [Tooltip("...and on a phone (0 = none: phones have little memory, and the web page is closed by the browser when it runs out)")]
    public int phoneShadowedLamps = 0;
    public float shadowStrength = 1f;

    [Header("Small flicker")]
    [Range(0f, 0.1f)] public float flickerChance = 0.01f;
    [Range(0f, 1f)] public float flickerDepth = 0.45f;

    private MaterialPropertyBlock block;
    private float level = 1f;
    private float flicker = 1f;
    private bool glowOff;
    private bool tooMany;     // not one of the nearest lamps
    private float rankFade = 1f;

    static readonly List<LampSpot> all = new List<LampSpot>();
    static int lastShadowFrame = -100;
    static readonly List<KeyValuePair<float, LampSpot>> ranked = new List<KeyValuePair<float, LampSpot>>();

    void Awake()
    {
        if (lamp == null) lamp = GetComponent<Light>();

        // the lamp's mesh really glows (the light sticks "light", "light12"... were plain grey)
        ApplyGlowMaterial(glow);

        if (applyLook && lamp != null)
        {
            if (kind == Kind.Bulb)
            {
                // a bulb light looks down (it was a point light that lit the ceiling above the bulb too)
                lamp.type = LightType.Spot;
                lamp.color = bulbLightColour;
                lamp.range = bulbSoftRange;
                lamp.spotAngle = bulbLightAngle;
                lamp.innerSpotAngle = bulbLightAngle * 0.47f;
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                intensity = bulbSoftPower;
                flickerChance = bulbFlicker;
                flickerDepth = bulbFlickerDip;
            }
            else
            {
                lamp.color = lampTint;
                lamp.range = tubeRange;
                lamp.spotAngle = lampAngle;
                lamp.innerSpotAngle = lampAngle * 0.45f;
                intensity = tubePower;
            }
            lamp.intensity = intensity;
        }
    }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    // Every few frames: the lamps nearest to the camera get (soft) shadows, all other lamps none
    static void UpdateShadows()
    {
        if (Time.frameCount - lastShadowFrame < 20) return;
        lastShadowFrame = Time.frameCount;

        var cam = Camera.main;
        if (cam == null) return;

        bool handheld = DeviceDetection.IsMobile();   // (SystemInfo says "desktop" in a phone browser)
        ranked.Clear();
        foreach (var l in all)
        {
            if (l == null || l.lamp == null) continue;
            float d = (l.transform.position - cam.transform.position).sqrMagnitude;
            ranked.Add(new KeyValuePair<float, LampSpot>(d, l));
        }
        ranked.Sort((a, b) => a.Key.CompareTo(b.Key));

        for (int i = 0; i < ranked.Count; i++)
        {
            var l = ranked[i].Value;
            l.tooMany = i >= (handheld ? l.phoneMostLightsOn : l.mostLightsOn);
            int budget = handheld ? l.phoneShadowedLamps : l.shadowedLamps;
            bool on = l.castShadows && i < budget && l.kind == Kind.Tube;   // bulbs are small lights: no shadows

            var light = l.lamp;
            light.shadows = on ? (handheld ? LightShadows.Hard : LightShadows.Soft) : LightShadows.None;
            if (on)
            {
                light.shadowStrength = l.shadowStrength;
                light.shadowBias = 0.03f;
                light.shadowNormalBias = 0.4f;
                light.shadowNearPlane = 0.2f;
                light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            }
        }
    }

    void Update()
    {
        if (lamp == null) return;

        UpdateShadows();

        float goal = ScaryLamp.PowerOut ? 0f : 1f;
        level = Mathf.MoveTowards(level, goal, fadeSpeed * Time.deltaTime);

        // a short drop of light now and then
        flicker = Mathf.MoveTowards(flicker, 1f, 6f * Time.deltaTime);
        if (goal > 0f && Random.value < flickerChance) flicker = 1f - flickerDepth * Random.value;

        // too far from the player: switched off (the house has many lights)
        var cam = Camera.main;
        float cull = DeviceDetection.IsMobile() ? phoneLightsFarDistance : lightsFarDistance;
        // far lamps fade out in the last quarter of the distance, and a lamp that is not one of the nearest fades too: nothing pops
        float distanceFade = 1f;
        if (cam != null)
        {
            float d = (transform.position - cam.transform.position).magnitude;
            distanceFade = Mathf.Clamp01((cull - d) / (cull * 0.25f));
        }
        rankFade = Mathf.MoveTowards(rankFade, tooMany ? 0f : 1f, 2.5f * Time.deltaTime);

        lamp.intensity = intensity * level * flicker * Mathf.Min(distanceFade, rankFade);
        lamp.enabled = lamp.intensity > 0.01f;

        // the lamp's glow follows the power
        bool off = level < 0.05f;
        if (glow != null && off != glowOff)
        {
            glowOff = off;
            if (block == null) block = new MaterialPropertyBlock();

            if (off)
            {
                glow.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", Color.black);
                glow.SetPropertyBlock(block);
            }
            else
            {
                glow.SetPropertyBlock(null);
            }
        }
    }
}
