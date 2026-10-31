using UnityEngine;

// Electric arcs inside the glass tube of the MEP: thin bright bolts that jump from end to end, a soft blue glow around them and a
// flickering light. Made from code, so it always has the size of the tube (no prefab to fit) and loops by itself.
// Add it with Tools > Ghost > Add Lightning VFX To MEP Glass. It only shows while the glass is shown (the MEP is out).
public class MepLightning : MonoBehaviour
{
    [Tooltip("The glass tube of the MEP")]
    public Renderer glass;
    [Tooltip("How many bolts")]
    [Range(1, 6)] public int bolts = 3;
    [Tooltip("How much of the tube length the bolts cover (1 = end to end)")]
    [Range(0.3f, 1f)] public float length = 0.8f;
    [Tooltip("How far a bolt wanders from the middle line, as a share of the tube width")]
    [Range(0.1f, 1f)] public float wander = 0.65f;
    [Tooltip("How often the bolts jump to a new shape (seconds)")]
    public float jumpEvery = 0.07f;
    [Tooltip("Brightness of the color (above 1 glows with the Bloom of the game)")]
    public float brightness = 2.5f;
    public Color color = new Color(0.45f, 0.8f, 1f, 1f);
    public bool addLight = true;

    private Transform root;
    private LineRenderer[] cores;
    private LineRenderer[] glows;
    private Light glowLight;
    private Vector3 axisLocal = Vector3.forward;
    private float half;
    private float radius;
    private float nextJump;
    private const int Points = 14;
    private MaterialPropertyBlock block;

    void Start()
    {
        if (glass == null) glass = GetComponent<Renderer>();
        if (glass == null) { enabled = false; return; }

        Measure();
        Build();
    }

    // The tube: which way it points, how long and how wide
    void Measure()
    {
        var filter = glass.GetComponent<MeshFilter>();
        Vector3 local = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size : glass.bounds.size;
        Vector3 scale = glass.transform.lossyScale;
        Vector3 world = new Vector3(Mathf.Abs(local.x * scale.x), Mathf.Abs(local.y * scale.y), Mathf.Abs(local.z * scale.z));

        int longest = world.x >= world.y && world.x >= world.z ? 0 : (world.y >= world.z ? 1 : 2);
        axisLocal = longest == 0 ? Vector3.right : (longest == 1 ? Vector3.up : Vector3.forward);

        float a = world[longest];
        float b = world[(longest + 1) % 3];
        float c = world[(longest + 2) % 3];
        half = a * 0.5f * length;
        radius = Mathf.Min(b, c) * 0.5f;
    }

    void Build()
    {
        // a child of the glass that keeps a world scale of 1 (the glass or the MEP may be scaled): then the numbers are meters
        root = new GameObject("MEP Lightning").transform;
        root.SetParent(glass.transform, false);
        Vector3 s = glass.transform.lossyScale;
        root.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));

        // the middle of the tube (the middle of its mesh) and the tube's long side as the Z direction of the child
        var filter = glass.GetComponent<MeshFilter>();
        Vector3 centerLocal = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;
        root.localPosition = centerLocal;
        Vector3 worldAxis = glass.transform.TransformDirection(axisLocal).normalized;
        Vector3 worldUp = Mathf.Abs(Vector3.Dot(worldAxis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        root.rotation = Quaternion.LookRotation(worldAxis, worldUp);

        var material = Resources.Load<Material>("Orb/Orb_Glow");   // soft and additive: it glows
        block = new MaterialPropertyBlock();

        cores = new LineRenderer[bolts];
        glows = new LineRenderer[bolts];
        for (int i = 0; i < bolts; i++)
        {
            cores[i] = MakeLine("Bolt " + i, material, Mathf.Max(0.004f, radius * 0.07f));
            glows[i] = MakeLine("Bolt Glow " + i, material, Mathf.Max(0.02f, radius * 0.45f));
        }

        if (addLight && !DeviceDetection.IsMobile())
        {
            var go = new GameObject("MEP Lightning Light");
            go.transform.SetParent(root, false);
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.4f, 0.75f, 1f);
            glowLight.range = Mathf.Max(1.2f, half * 3f);
            glowLight.intensity = 1.5f;
            glowLight.shadows = LightShadows.None;
        }

        Jump();
    }

    LineRenderer MakeLine(string lineName, Material material, float width)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(root, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = Points;
        line.widthMultiplier = width;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        if (material != null) line.sharedMaterial = material;
        return line;
    }

    void Update()
    {
        if (root == null) return;

        // only while the glass is shown (the MEP is out)
        bool shown = glass.enabled && glass.gameObject.activeInHierarchy;
        if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
        if (!shown) return;

        if (Time.time >= nextJump)
        {
            nextJump = Time.time + jumpEvery * Random.Range(0.6f, 1.6f);
            Jump();
        }

        if (glowLight != null)
            glowLight.intensity = 1.2f + Random.value * 1.6f;
    }

    // New jagged shapes for every bolt
    void Jump()
    {
        for (int i = 0; i < bolts; i++)
        {
            float phase = Random.value * 6.28f;
            float side = Random.Range(0.4f, 1f) * radius * wander;

            for (int p = 0; p < Points; p++)
            {
                float t = p / (float)(Points - 1);
                float z = Mathf.Lerp(-half, half, t);

                // wanders away from the middle line, and is pulled back to it at both ends
                float ends = Mathf.Sin(t * Mathf.PI);
                float angle = phase + t * Random.Range(2f, 5f) + Random.Range(-0.6f, 0.6f);
                float offset = side * ends * Random.Range(0.3f, 1f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * offset, Mathf.Sin(angle) * offset, z);

                cores[i].SetPosition(p, position);
                glows[i].SetPosition(p, position);
            }

            // a bolt flashes bright, or is nearly gone
            float flash = Random.value < 0.25f ? Random.Range(0.1f, 0.4f) : Random.Range(0.7f, 1.3f);
            Color core = Color.Lerp(color, Color.white, 0.7f) * (brightness * flash);
            Color haze = color * (brightness * 0.45f * flash);
            core.a = 1f;
            haze.a = 1f;
            SetColor(cores[i], core);
            SetColor(glows[i], haze);
        }
    }

    void SetColor(LineRenderer line, Color c)
    {
        line.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        line.SetPropertyBlock(block);
    }
}
