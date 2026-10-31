using UnityEngine;

// The Soul Orb (Lv 1) as the animated butterfly (ASSET/animated_butterfly.glb). It sits on the root of the prefab ASSET/Orb/Butterfly Orb.prefab
// (made by Tools > Ghost > Use Butterfly Orb, MirageGhost And Muzzle Orb) and the GhostSpawner puts a copy on every Orb ("Orb Prefab").
// The wings flap (the "Flying" clip, played by AnimatedModel), the butterfly floats at the baby's place and turns slowly, and its wings
// get a little light blue emission and a soft blue light.
// Its size (Diameter = the wingspan in meters) and its place above the baby's pivot (Lift) come from the GhostSpawner (Orb Width, Orb Lift).
public class ButterflyOrb : MonoBehaviour
{
    [Tooltip("The wingspan in meters (the GhostSpawner sets it from Orb Width)")]
    public float diameter = 0.9f;
    [Tooltip("How high above the baby's pivot it floats (meters)")]
    public float lift = 0.45f;
    [Tooltip("How fast it turns around (degrees per second)")]
    public float spin = 28f;
    [Tooltip("How far it floats up and down (meters)")]
    public float bob = 0.07f;

    [Header("Glow")]
    [Tooltip("The blue of the emission and of the light")]
    public Color glowColor = new Color(0.55f, 0.86f, 1f, 1f);
    [Tooltip("How much light blue emission the wings have (0 = none). A little: 0.1 to 0.3")]
    [Range(0f, 2f)] public float emissionStrength = 0.16f;
    [Tooltip("How strong the soft blue light around it is")]
    [Range(0f, 2f)] public float lightStrength = 0.28f;
    [Tooltip("A soft blue light around the butterfly (not on phones)")]
    public bool glowLight = true;

    [Header("Glass")]
    [Tooltip("ON: the wings are see-through glass, like the ghost material (Resources/Mirage/Mirage_Ghost), with a cold light on the edges. The pattern of the wings stays. OFF: the wings of the .glb file")]
    public bool glassLook = true;
    [Tooltip("The colour of the glass")]
    public Color glassTint = new Color(0.46f, 0.66f, 0.84f, 1f);
    [Tooltip("The colour of the edges")]
    public Color glassEdge = new Color(0.72f, 0.86f, 0.96f, 1f);
    [Tooltip("How much of the wing can be seen through (0 = clear glass, 1 = solid)")]
    [Range(0f, 1f)] public float glassBody = 0.5f;
    [Tooltip("How strong the light on the edges is")]
    [Range(0f, 1f)] public float glassRim = 0.45f;
    [Tooltip("The brightness of the glass")]
    [Range(0.2f, 3f)] public float glassBrightness = 0.65f;
    [Tooltip("How thin and sharp the bright edge is (higher = a thinner, crisper edge like a glass rim, lower = a soft blur)")]
    [Range(0.5f, 6f)] public float glassEdgeSharpness = 3.6f;

    private Transform root;
    private Renderer[] renderers;
    private Light orbLight;
    private float baseScale = 1f;
    private float angle;
    private float phase;

    void Awake()
    {
        root = transform.parent;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        angle = Random.value * 360f;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Start()
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            if (!MakeGlass(r))
            {
                // each butterfly has its own copy of the materials, with a little light blue emission
                var mats = r.materials;
                foreach (var m in mats) SetEmission(m);
                r.materials = mats;
            }

            list.Add(r);
        }
        renderers = list.ToArray();

        // the size as it comes (the baby's own scale included), then the scale that makes the wingspan 'diameter' wide in the world
        bool any = false;
        Bounds box = new Bounds();
        foreach (var r in renderers)
        {
            if (!any) { box = r.bounds; any = true; }
            else box.Encapsulate(r.bounds);
        }
        float size = any ? Mathf.Max(box.size.x, box.size.z) : 0f;
        if (size > 0.0001f) baseScale = diameter / size;

        if (glowLight && !DeviceDetection.IsMobile()) AddLight();
    }

    // The wings become glass: a copy of the ghost material for each material of the wings, with the picture of the wings in it.
    // Returns false when the glass is off or the ghost material is missing (the wings then stay as they are).
    bool MakeGlass(Renderer r)
    {
        if (!glassLook) return false;

        if (glassTemplate == null) glassTemplate = Resources.Load<Material>("Mirage/Mirage_Ghost");
        if (glassTemplate == null) return false;

        var old = r.sharedMaterials;
        var glass = new Material[old.Length];
        for (int i = 0; i < old.Length; i++)
        {
            var g = new Material(glassTemplate);
            var picture = PictureOf(old[i]);
            if (picture != null) g.SetTexture("_BaseMap", picture);
            g.SetColor("_Tint", glassTint);
            g.SetColor("_RimColor", glassEdge);
            g.SetFloat("_Alpha", glassBody);
            g.SetFloat("_RimAlpha", glassRim);
            g.SetFloat("_Glow", glassBrightness);
            g.SetFloat("_RimPower", glassEdgeSharpness);
            g.SetFloat("_Pulse", 0.02f);             // almost no pulse: a still, clear glass (the pulse made it look blurry)
            g.SetFloat("_Cull", 0f);                 // thin wings: both sides are drawn
            glass[i] = g;
        }
        r.materials = glass;
        return true;
    }

    static Material glassTemplate;

    // The colour picture of a material (the .glb shader calls it baseColorTexture, a URP shader _BaseMap)
    static Texture PictureOf(Material m)
    {
        if (m == null) return null;
        foreach (var property in new[] { "baseColorTexture", "_BaseMap", "_MainTex", "_BaseColorMap" })
        {
            if (!m.HasProperty(property)) continue;
            var texture = m.GetTexture(property);
            if (texture != null) return texture;
        }
        return null;
    }

    void SetEmission(Material m)
    {
        if (m == null) return;

        var c = new Color(glowColor.r * emissionStrength, glowColor.g * emissionStrength, glowColor.b * emissionStrength, 1f);
        // the .glb shader (glTFast) calls it emissiveFactor, a normal URP Lit shader calls it _EmissionColor
        foreach (var property in new[] { "emissiveFactor", "_EmissionColor", "_EmissiveColor" })
            if (m.HasProperty(property)) m.SetColor(property, c);
        m.EnableKeyword("_EMISSION");
    }

    void AddLight()
    {
        var go = new GameObject("Butterfly Light");
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = glowColor;
        l.range = 1.8f;
        l.intensity = lightStrength;
        l.shadows = LightShadows.None;
        orbLight = l;
    }

    // The spawner hides an Orb by switching its renderers off: the light must go with it
    bool IsShown()
    {
        if (renderers == null) return true;
        foreach (var r in renderers)
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
        return false;
    }

    void LateUpdate()
    {
        if (root == null) return;

        bool shown = IsShown();
        if (orbLight != null && orbLight.enabled != shown) orbLight.enabled = shown;
        if (!shown) return;

        angle += spin * Time.deltaTime;
        float y = lift + Mathf.Sin(Time.time * 1.7f + phase) * bob;
        transform.SetPositionAndRotation(root.position + Vector3.up * y, Quaternion.Euler(0f, angle, 0f));
        transform.localScale = Vector3.one * baseScale;

        if (orbLight != null) orbLight.intensity = lightStrength * (1f + 0.25f * Mathf.Sin(Time.time * 2.3f + phase * 1.7f));
    }
}
