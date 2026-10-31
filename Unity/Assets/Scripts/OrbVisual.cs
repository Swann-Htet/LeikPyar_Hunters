using UnityEngine;

// The look of an Orb (the baby ghost): the magical orb model, floating at the baby's place and turning slowly.
// It is added by the GhostSpawner to every Orb.
//  - The model (ASSET/Orb/magical_orb.glb) is two turning energy shapes and a grey sphere that is not wanted: the sphere is
//    removed, and the shapes get the glowing material Resources/Orb/Orb_Glow (soft blue light, like a glowing ball).
//  - The orb keeps its own size (Diameter = how wide the glowing ball looks) and its place above the baby's pivot (Lift),
//    whatever the baby's own scale or tilt is, and it does not tilt with it.
public class OrbVisual : MonoBehaviour
{
    [Tooltip("How wide the glowing ball looks (meters)")]
    public float diameter = 0.9f;
    [Tooltip("How high above the baby's pivot the orb floats (meters)")]
    public float lift = 0.45f;
    [Tooltip("Slow turn (degrees per second)")]
    public float spin = 35f;
    [Tooltip("How much it swells and shrinks (0 = steady)")]
    [Range(0f, 0.3f)] public float pulse = 0.08f;
    public float pulseSpeed = 1.6f;
    [Tooltip("How much of the model's size the visible glowing ball fills (the glow picture has soft empty edges)")]
    [Range(0.2f, 1f)] public float glowFraction = 0.45f;
    [Tooltip("A soft blue light around the orb (not on phones)")]
    public bool glowLight = true;

    private Transform root;
    private float baseScale = 1f;
    private float angle;
    private Renderer[] shapeRenderers;
    private Light orbLight;

    void Awake()
    {
        root = transform.parent;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        var glow = Resources.Load<Material>("Orb/Orb_Glow");

        // the shapes get the glow, the sphere goes
        var shapes = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            if (IsSphere(r)) { DestroyImmediate(r.gameObject); continue; }

            if (glow != null)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = glow;
                r.sharedMaterials = mats;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            shapes.Add(r);
        }

        shapeRenderers = shapes.ToArray();

        // the model's size as it comes, then the scale that makes the glowing ball 'diameter' wide in the world
        bool any = false;
        Bounds box = new Bounds();
        foreach (var r in shapes)
        {
            if (!any) { box = r.bounds; any = true; }
            else box.Encapsulate(r.bounds);
        }

        float size = any ? Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)) : 0f;
        if (size > 0.0001f) baseScale = diameter / (size * glowFraction);
        angle = Random.value * 360f;

        if (glowLight && !DeviceDetection.IsMobile()) AddLight();
    }

    // The model has a sphere called Icosphere (a grey ball beside the shapes)
    bool IsSphere(Renderer r)
    {
        for (Transform t = r.transform; t != null && t != transform.parent; t = t.parent)
            if (t.name.StartsWith("Icosphere") || t.name == "Object_8") return true;
        return false;
    }

    void AddLight()
    {
        var go = new GameObject("Orb Light");
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.35f, 0.75f, 1f);
        l.range = 2.5f;
        l.intensity = 1.2f;
        l.shadows = LightShadows.None;
        orbLight = l;
    }

    // The spawner hides an Orb by switching its renderers off: its light must go with it
    bool IsShown()
    {
        if (shapeRenderers == null) return true;
        foreach (var r in shapeRenderers)
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
        return false;
    }

    void LateUpdate()
    {
        if (root == null) return;

        if (orbLight != null)
        {
            bool shown = IsShown();
            if (orbLight.enabled != shown) orbLight.enabled = shown;
        }

        angle += spin * Time.deltaTime;
        transform.SetPositionAndRotation(root.position + Vector3.up * lift, Quaternion.Euler(0f, angle, 0f));
        transform.localScale = Vector3.one * (baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed + angle * 0.05f) * pulse));
    }
}
