using UnityEngine;
using UnityEngine.SceneManagement;

// Dust floating in the air of the house: small soft specks that drift slowly and glow a little in the lamp light.
// It is made by itself in the game scene (the one with the NightFog), and follows the player's camera, so the dust is
// always around you. To change it, add this component to any object yourself (the automatic one is then not made).
public class DustMotes : MonoBehaviour
{
    [Header("Dust")]
    [Tooltip("How many specks in the air around you")]
    public int count = 220;
    [Tooltip("Size of the box (meters) around the player where the dust floats")]
    public Vector3 area = new Vector3(16f, 5f, 16f);
    public Vector2 size = new Vector2(0.015f, 0.04f);
    [Range(0f, 1f)] public float brightness = 0.55f;
    public Color color = new Color(1f, 0.92f, 0.78f, 1f);

    [Header("Movement")]
    [Tooltip("Slow drift (meters per second)")]
    public float drift = 0.06f;
    [Tooltip("How much the air swirls the dust around")]
    public float swirl = 0.35f;

    // fewer specks on a phone
    int Count { get { return DeviceDetection.IsMobile() ? Mathf.Min(count, 80) : count; } }

    private ParticleSystem particles;
    private Transform follow;

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
        if (FindFirstObjectByType<NightFog>() == null) return;     // only the game scene
        if (FindFirstObjectByType<DustMotes>() != null) return;

        new GameObject("Dust Motes (auto)").AddComponent<DustMotes>();
    }

    void Start()
    {
        BuildParticles();
    }

    void BuildParticles()
    {
        particles = gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 22f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(color.r, color.g, color.b, brightness * 0.35f),
            new Color(color.r, color.g, color.b, brightness));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Count;
        main.gravityModifier = 0f;

        var emission = particles.emission;
        emission.rateOverTime = Count / 16f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        // a slow, random drift
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-drift, drift);
        velocity.y = new ParticleSystem.MinMaxCurve(-drift * 0.5f, drift);
        velocity.z = new ParticleSystem.MinMaxCurve(-drift, drift);

        var noise = particles.noise;
        noise.enabled = !DeviceDetection.IsMobile();   // a phone skips the swirl: less work
        noise.strength = swirl * 0.25f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.15f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        // fade in and out, so specks never pop
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = MakeMaterial();

        particles.Play();
    }

    // "Sprites/Default" is always in the build (also WebGL) and works with the particle colours
    static Material MakeMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        var material = new Material(shader);
        material.mainTexture = SoftDot();
        return material;
    }

    static Texture2D SoftDot()
    {
        const int n = 32;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        texture.Apply();
        return texture;
    }

    void LateUpdate()
    {
        if (follow == null)
        {
            var cam = Camera.main;
            if (cam != null) follow = cam.transform;
        }

        if (follow != null) transform.position = follow.position + Vector3.up * 0.3f;
    }
}
