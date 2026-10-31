using UnityEngine;
using UnityEngine.AI;

// The Mirage: a wisp of glowing teal mist with a slowly turning ring (a different look from the baby ghost's Orb).
//  - CreateVisual() builds it from code, so the GhostSpawner has a Mirage even before you add your own model
//    (your own prefab in GhostSpawner > Mirage Template is used instead, and it gets this behaviour too)
//  - While the gun is weak (Beam Power at or below "Runs Away Below Power" on the GhostGun, 20% by default) the Mirage runs
//    away from the player over the NavMesh and cannot be pulled. A stronger gun (upgrade it) lets you catch it.
public class MirageGhost : MonoBehaviour
{
    [Header("Run away from a weak gun")]
    [Tooltip("It starts to run when the player is closer than this (meters)")]
    public float fleeDistance = 9f;
    public float runSpeed = 3.4f;
    [Tooltip("Meters above the floor (it is read from the spot it appears at)")]
    public float hover = 0.5f;

    [Header("Look")]
    public Transform visual;
    public float bobAmount = 0.06f;
    public float bobSpeed = 2f;

    [Header("Ghost animation (your own model)")]
    [Tooltip("How far the model floats up and down (meters)")]
    public float floatAmount = 0.12f;
    [Tooltip("How much it sways from side to side and back and forth (degrees)")]
    public float swayAngle = 4f;
    [Tooltip("How much it leans forward while it runs (degrees)")]
    public float runLean = 14f;

    private Transform model;                       // the model's own root (a child with a renderer), when there is no built visual
    private Vector3 modelBasePosition;
    private Quaternion modelBaseRotation;
    private Vector3 modelBaseScale;
    private float lean;
    private Light glow;
    private float visualBaseY;
    private float lastMoveTime = -10f;
    private float cornerSince = -1f;
    private bool hoverRead;

    // True while it is running away (the gun can not aim at it then)
    public bool IsRunning => Time.time - lastMoveTime < 0.35f;

    [Header("Look of your model (the .glb)")]
    [Tooltip("The material in the .glb file is unlit white, so lights can not make it darker. This multiplies its colour: 1 = as it comes (very bright), 0.5 = calmer")]
    [Range(0.1f, 1.5f)] public float modelBrightness = 0.5f;
    [Tooltip("1 = solid, lower = more see-through (the file has 0.66). A higher number means less glass")]
    [Range(0.05f, 1f)] public float modelOpacity = 0.85f;
    [Tooltip("The colour the model is tinted")]
    public Color modelTint = new Color(0.8f, 0.87f, 0.95f, 1f);

    // The colour of a material of the .glb (the glTF shader calls it baseColorFactor, a normal URP shader _BaseColor)
    void DimModel()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            var mats = r.materials;                 // this copy of the Mirage has its own materials
            foreach (var m in mats)
            {
                if (m == null) continue;
                foreach (var property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                {
                    if (!m.HasProperty(property)) continue;
                    m.SetColor(property, new Color(modelTint.r * modelBrightness, modelTint.g * modelBrightness, modelTint.b * modelBrightness, modelOpacity));
                    break;
                }
            }
            r.materials = mats;
        }
    }

    void Awake()
    {
        if (visual != null) visualBaseY = visual.localPosition.y;
        glow = GetComponentInChildren<Light>(true);

        // your own model: everything under the root is put under one pivot, and the pivot is animated
        // (a skinned model must move as a whole: its bones and its mesh are different children)
        if (visual == null && wraith == null && transform.childCount > 0 && GetComponentInChildren<Renderer>(true) != null &&
            GetComponentInChildren<ParticleSystem>(true) == null)   // the Mirage built from code has its own animation
        {
            DimModel();

            // the clip inside the model file is a dance: a calm idle pose takes its place (see IdlePose)
            foreach (var dance in GetComponentsInChildren<AnimatedModel>(true))
            {
                dance.enabled = false;
                if (dance.GetComponent<IdlePose>() == null) dance.gameObject.AddComponent<IdlePose>();
            }

            var pivot = new GameObject("Model Pivot").transform;
            pivot.SetParent(transform, false);

            var kids = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
                if (child != pivot) kids.Add(child);
            foreach (var kid in kids) kid.SetParent(pivot, true);

            model = pivot;
            modelBasePosition = pivot.localPosition;
            modelBaseRotation = pivot.localRotation;
            modelBaseScale = pivot.localScale;
        }
    }

    void Update()
    {
        // not shown tonight: nothing to do
        if (!IsShown()) { hoverRead = false; return; }

        if (!hoverRead) ReadHover();

        Animate();

        var gun = GhostGun.Instance;
        if (gun == null || gun.PlayerRoot == null) return;

        bool weak = gun.BeamPower <= gun.mirageRunsAwayBelowPower;
        if (!weak) { cornerSince = -1f; return; }

        Vector3 player = gun.PlayerRoot.position;
        Vector3 away = transform.position - player;
        away.y = 0f;
        float distance = away.magnitude;
        if (distance > fleeDistance) return;
        if (distance < 0.05f) away = Random.insideUnitSphere;
        away.y = 0f;
        away.Normalize();

        if (TryStep(away))
        {
            lastMoveTime = Time.time;
            cornerSince = -1f;
        }
        else if (cornerSince < 0f)
        {
            cornerSince = Time.time;   // cornered: it stops, and shows why
        }

        // tell the player why it can not be pulled
        if (IsRunning && gun.IsHeld && distance < fleeDistance && FacingMe(gun))
            InteractPrompt.Request("The Mirage runs away from a weak gun - upgrade the gun");
    }

    bool IsShown()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
        return false;
    }

    bool FacingMe(GhostGun gun)
    {
        var cam = gun.playerCamera != null ? gun.playerCamera.transform : null;
        if (cam == null) return false;
        Vector3 to = transform.position - cam.position;
        return Vector3.Angle(cam.forward, to) < 30f;
    }

    // How high above the floor it floats
    void ReadHover()
    {
        hoverRead = true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            hover = Mathf.Clamp(transform.position.y - hit.position.y, 0.1f, 3f);
    }

    // One step away from the player over the NavMesh, bending round walls. False = no way out
    bool TryStep(Vector3 away)
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit here, 3f, NavMesh.AllAreas)) return false;

        float length = runSpeed * Time.deltaTime;
        float[] turns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 130f, -130f };

        foreach (float turn in turns)
        {
            Vector3 dir = Quaternion.Euler(0f, turn, 0f) * away;
            Vector3 target = here.position + dir * Mathf.Max(0.3f, length * 4f);

            if (!NavMesh.SamplePosition(target, out NavMeshHit there, 0.8f, NavMesh.AllAreas)) continue;
            if (NavMesh.Raycast(here.position, there.position, out NavMeshHit blocked, NavMesh.AllAreas)) continue;

            Vector3 next = Vector3.MoveTowards(here.position, there.position, length);
            transform.position = next + Vector3.up * hover;
            return true;
        }
        return false;
    }

    void Animate()
    {
        if (visual != null)
        {
            Vector3 p = visual.localPosition;
            p.y = visualBaseY + Mathf.Sin(Time.time * bobSpeed + GetInstanceID() * 0.1f) * bobAmount;
            visual.localPosition = p;
        }

        // your own model floats, sways like in a slow wind, breathes, leans forward when it runs, and now and then jerks (a ghost's glitch)
        if (model != null)
        {
            float t = Time.time + GetInstanceID() * 0.01f;
            float bob = Mathf.Sin(t * 1.7f) * floatAmount;
            model.localPosition = modelBasePosition + Vector3.up * bob;

            lean = Mathf.Lerp(lean, IsRunning ? runLean : 0f, 5f * Time.deltaTime);
            float jerk = Mathf.PerlinNoise(t * 0.9f, 3.1f) > 0.86f ? Mathf.Sin(t * 40f) * 2.5f : 0f;
            model.localRotation = modelBaseRotation * Quaternion.Euler(
                Mathf.Sin(t * 0.9f) * swayAngle + lean,
                Mathf.Sin(t * 0.5f) * swayAngle * 1.5f + jerk,
                Mathf.Cos(t * 0.8f) * swayAngle);

            float breathe = 1f + Mathf.Sin(t * 1.3f) * 0.015f;
            model.localScale = new Vector3(modelBaseScale.x * breathe, modelBaseScale.y * (2f - breathe), modelBaseScale.z * breathe);
        }

        // the wraith sways a little, as if it floated in a slow wind
        if (wraith != null)
        {
            float tt = Time.time * 1.1f + GetInstanceID() * 0.01f;
            wraith.localRotation = Quaternion.Euler(Mathf.Sin(tt) * swayDegrees, 0f, Mathf.Cos(tt * 0.8f) * swayDegrees);
        }

        // the light shimmers
        if (glow != null)
            glow.intensity = 1.5f + Mathf.Sin(Time.time * 7f) * 0.25f + Mathf.PerlinNoise(Time.time * 3f, GetInstanceID() * 0.01f) * 0.5f;
    }

    // ---------------------------------------------------------------- the look, made from code

    public static GameObject CreateVisual(string name, Transform parent)
    {
        var root = new GameObject(name);
        if (parent != null) root.transform.SetParent(parent, false);

        var visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(root.transform, false);
        visualGo.transform.localPosition = new Vector3(0f, 0.4f, 0f);

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = SoftDot();

        // the mist: slowly drifting soft puffs
        var mist = NewSystem("Mist", visualGo.transform, material);
        var main = mist.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 1f, 0.92f, 0.5f), new Color(0.3f, 0.7f, 1f, 0.38f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 70;
        var emission = mist.emission;
        emission.rateOverTime = 34f;
        var shape = mist.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        var noise = mist.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.9f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        SizeAndFade(mist);

        // the bright heart
        var core = NewSystem("Core", visualGo.transform, material);
        var coreMain = core.main;
        coreMain.loop = true;
        coreMain.startLifetime = 1.6f;
        coreMain.startSpeed = 0f;
        coreMain.startSize = 0.42f;
        coreMain.startColor = new Color(0.85f, 1f, 1f, 0.75f);
        coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        coreMain.maxParticles = 3;
        var coreEmission = core.emission;
        coreEmission.rateOverTime = 1.8f;
        var coreShape = core.shape;
        coreShape.enabled = false;
        SizeAndFade(core);

        // a ring of sparks turning around it
        var ring = NewSystem("Ring", visualGo.transform, material);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lies flat
        var ringMain = ring.main;
        ringMain.loop = true;
        ringMain.startLifetime = 1.8f;
        ringMain.startSpeed = 0f;
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        ringMain.startColor = new Color(0.7f, 1f, 1f, 0.9f);
        ringMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        ringMain.maxParticles = 40;
        var ringEmission = ring.emission;
        ringEmission.rateOverTime = 22f;
        var ringShape = ring.shape;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 0.45f;
        ringShape.radiusThickness = 0f;     // on the edge only
        var ringVelocity = ring.velocityOverLifetime;
        ringVelocity.enabled = true;
        ringVelocity.space = ParticleSystemSimulationSpace.Local;
        ringVelocity.x = 0f;
        ringVelocity.y = 0f;
        ringVelocity.z = 0f;
        ringVelocity.orbitalZ = 1.8f;
        SizeAndFade(ring);

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(visualGo.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.4f, 0.95f, 0.9f);
        light.range = 3.5f;
        light.intensity = 1.5f;
        light.shadows = LightShadows.None;

        var mirage = root.AddComponent<MirageGhost>();
        mirage.visual = visualGo.transform;
        mirage.wraith = BuildWraith(visualGo.transform);
        return root;
    }

    // ---------------------------------------------------------------- the level 2 ghost: a tall hooded wraith made of mist

    [Header("Wraith (the body)")]
    public Transform wraith;
    [Tooltip("How far the wraith sways (degrees)")]
    public float swayDegrees = 4f;

    // A torn cloak, a hood and two glowing eyes. The materials are in Resources (Mirage_Cloak, Orb_Glow)
    static Transform BuildWraith(Transform parent)
    {
        var body = new GameObject("Wraith").transform;
        body.SetParent(parent, false);

        var cloakBase = Resources.Load<Material>("Mirage/Mirage_Cloak");
        Material cloak = null;
        if (cloakBase != null)
        {
            cloak = new Material(cloakBase);
            cloak.SetTexture("_BaseMap", CloakGradient());
        }

        var cloakGo = new GameObject("Cloak");
        cloakGo.transform.SetParent(body, false);
        cloakGo.AddComponent<MeshFilter>().sharedMesh = CloakMesh();
        var cloakRenderer = cloakGo.AddComponent<MeshRenderer>();
        if (cloak != null) cloakRenderer.sharedMaterial = cloak;
        cloakRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cloakRenderer.receiveShadows = false;

        // the hood
        var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hood.name = "Hood";
        Object.Destroy(hood.GetComponent<Collider>());
        hood.transform.SetParent(body, false);
        hood.transform.localPosition = new Vector3(0f, 1.02f, 0f);
        hood.transform.localScale = new Vector3(0.3f, 0.36f, 0.32f);
        var hoodRenderer = hood.GetComponent<MeshRenderer>();
        if (cloak != null) hoodRenderer.sharedMaterial = cloak;
        hoodRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hoodRenderer.receiveShadows = false;

        // two glowing eyes that always look at the player's camera
        var glow = Resources.Load<Material>("Orb/Orb_Glow");
        for (int i = -1; i <= 1; i += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Quad);
            eye.name = "Eye";
            Object.Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(body, false);
            eye.transform.localPosition = new Vector3(0.07f * i, 1.04f, 0.15f);
            eye.transform.localScale = Vector3.one * 0.14f;

            var eyeRenderer = eye.GetComponent<MeshRenderer>();
            if (glow != null) eyeRenderer.sharedMaterial = glow;
            eyeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            eyeRenderer.receiveShadows = false;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1.2f, 2.4f, 2.6f, 1f));
            block.SetColor("_Color", new Color(1.2f, 2.4f, 2.6f, 1f));
            eyeRenderer.SetPropertyBlock(block);

            eye.AddComponent<MirageEyeFacing>();
        }

        return body;
    }

    static Mesh cloakMesh;

    // A cone that is wide at the bottom with a torn, uneven edge
    static Mesh CloakMesh()
    {
        if (cloakMesh != null) return cloakMesh;

        const int segments = 22;
        const int rings = 10;
        var vertices = new Vector3[(segments + 1) * (rings + 1)];
        var uvs = new Vector2[vertices.Length];

        for (int r = 0; r <= rings; r++)
        {
            float h = r / (float)rings;
            float y = Mathf.Lerp(-0.6f, 1.0f, h);
            float radius = Mathf.Lerp(0.5f, 0.12f, Mathf.Pow(h, 0.7f));

            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float noise = Hash(s % segments);

                // the lower edge is ragged: some strips hang lower and wider
                float rag = r == 0 ? 0.8f + 0.4f * noise : (r == 1 ? 0.95f + 0.1f * noise : 1f);
                float yy = r == 0 ? y - 0.22f * noise : y;

                int index = r * (segments + 1) + s;
                vertices[index] = new Vector3(Mathf.Cos(a) * radius * rag, yy, Mathf.Sin(a) * radius * rag);
                uvs[index] = new Vector2(s / (float)segments, h);
            }
        }

        var triangles = new int[segments * rings * 6];
        int t = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = r * (segments + 1) + s;
                int i1 = i0 + 1;
                int i2 = i0 + segments + 1;
                int i3 = i2 + 1;
                triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
            }
        }

        cloakMesh = new Mesh { name = "Mirage Cloak", vertices = vertices, uv = uvs, triangles = triangles };
        cloakMesh.RecalculateBounds();
        cloakMesh.RecalculateNormals();
        return cloakMesh;
    }

    static float Hash(int n)
    {
        float v = Mathf.Sin(n * 12.9898f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    static Texture2D cloakGradient;

    // The cloak fades to nothing at its torn bottom and is solid up to the hood
    static Texture2D CloakGradient()
    {
        if (cloakGradient != null) return cloakGradient;

        const int h = 64;
        cloakGradient = new Texture2D(4, h, TextureFormat.RGBA32, false);
        cloakGradient.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.45f));
            for (int x = 0; x < 4; x++) cloakGradient.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        cloakGradient.Apply();
        return cloakGradient;
    }

    static ParticleSystem NewSystem(string name, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var main = system.main;
        main.playOnAwake = true;
        main.prewarm = true;
        main.duration = 5f;
        return system;
    }

    // swells in, shrinks out, and fades at both ends
    static void SizeAndFade(ParticleSystem system)
    {
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.2f)));

        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
    }

    static Texture2D softDot;

    static Texture2D SoftDot()
    {
        if (softDot != null) return softDot;

        const int n = 32;
        softDot = new Texture2D(n, n, TextureFormat.RGBA32, false);
        softDot.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        softDot.Apply();
        return softDot;
    }
}

// An eye of the wraith: always turned to the camera
public class MirageEyeFacing : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
