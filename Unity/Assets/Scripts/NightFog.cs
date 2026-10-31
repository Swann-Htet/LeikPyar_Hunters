using UnityEngine;

// Night: from 20:00 (8 PM) to 6:00 AM on the wall clock.
//  - Power ON  : the house is lit, the air is a pale haze (nearly white fog), ambient light is fairly bright
//  - Power OUT : (see PowerOutage) black fog, almost no ambient light - only the torch (T) helps
// The two looks blend smoothly, so the lights flickering also flickers the room.
// Add with Tools > Ghost > Setup Night Fog, Torch And Power Outages (it also switches fog on in the
// scene's lighting, which the WebGL build needs to keep the fog shader).
public class NightFog : MonoBehaviour
{
    [Header("When (game clock hours, 24h)")]
    [Tooltip("The night starts at this hour (20 = 8:00 PM)")]
    public float fogStartHour = 20f;
    [Tooltip("...and ends at this hour (6 = 6:00 AM)")]
    public float fogEndHour = 6f;
    [Tooltip("Real seconds to fade in and out when the hour changes (the game starts already at night if it starts at night)")]
    public float fadeSeconds = 15f;

    [Header("Night - power ON (lights work, pale haze)")]
    [Tooltip("Nearly white: the fog is lit by the house lights")]
    public Color litFogColor = new Color(0.78f, 0.8f, 0.84f, 1f);
    [Tooltip("Beyond this distance (meters) the haze hides things. Bigger = clearer")]
    public float litVisibleDistance = 28f;
    [Range(0f, 1f)] public float litAmbient = 0.6f;
    [Range(0f, 1f)] public float litReflections = 0.6f;

    [Header("Night - power OUT (dark)")]
    [Tooltip("Dark blue-grey looks more natural than pure black")]
    public Color fogColor = new Color(0.025f, 0.03f, 0.04f, 1f);
    [Tooltip("Beyond this distance (meters) almost nothing is visible. Smaller = you see less")]
    public float visibleDistance = 11f;
    [Tooltip("The torch pushes the dark fog back by this much (meters)")]
    public float torchExtraDistance = 8f;
    [Tooltip("Makes the torch's push on the fog stronger (1 = as above, 2 = twice as far)")]
    public float torchFogBoost = 2.2f;
    [Range(0f, 1f)] public float nightAmbient = 0.05f;
    [Range(0f, 1f)] public float nightReflections = 0.1f;

    [Header("Day look (basically no fog)")]
    public float dayDensity = 0.0003f;
    public Color dayFogColor = new Color(0.5f, 0.5f, 0.55f, 1f);

    [Header("Test")]
    [Tooltip("Tick in Play mode to see the night at any time")]
    public bool forceNight;

    private WallClockController clock;
    private float night;        // 0 = day, 1 = night
    private float outFactor;    // 0 = power on, 1 = power out
    private float torchFactor;
    private bool started;

    // What the scene looked like before the night
    private float dayAmbientIntensity;
    private float dayReflectionIntensity;
    private Color dayAmbientLight, dayAmbientSky, dayAmbientEquator, dayAmbientGround;
    private float lastAmbient = -1f;
    private float nextEnvironmentUpdate;

    void Start()
    {
        clock = FindFirstObjectByType<WallClockController>();

        dayAmbientIntensity = RenderSettings.ambientIntensity;
        dayReflectionIntensity = RenderSettings.reflectionIntensity;
        dayAmbientLight = RenderSettings.ambientLight;
        dayAmbientSky = RenderSettings.ambientSkyColor;
        dayAmbientEquator = RenderSettings.ambientEquatorColor;
        dayAmbientGround = RenderSettings.ambientGroundColor;

        if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.ExponentialSquared)
            Debug.LogWarning("[NightFog] Fog is not set up in this scene's lighting. Run Tools > Ghost > Setup Night Fog, Torch And Power Outages once, " +
                             "otherwise the fog may be missing in the WebGL build.");
    }

    void OnDisable()
    {
        if (!started) return;

        // Back to the day look when this stops
        night = 0f;
        outFactor = 0f;
        Apply();
    }

    void Update()
    {
        float nightTarget = IsNight() ? 1f : 0f;
        float outTarget = ScaryLamp.PowerOut ? 1f : 0f;

        // The first frame jumps straight to the right state, later changes fade
        if (!started)
        {
            night = nightTarget;
            outFactor = outTarget;
            started = true;
        }
        else
        {
            night = Mathf.MoveTowards(night, nightTarget, Time.deltaTime / Mathf.Max(0.1f, fadeSeconds));
            outFactor = Mathf.MoveTowards(outFactor, outTarget, Time.deltaTime * 8f); // quick, so lamp flicker flickers the room
        }

        torchFactor = Mathf.MoveTowards(torchFactor, Torch.IsOn ? 1f : 0f, Time.deltaTime / 0.6f);

        Apply();
    }

    bool IsNight()
    {
        if (forceNight) return true;
        if (clock == null) return false;

        float hour = clock.GameHour24;
        float length = (fogEndHour - fogStartHour + 24f) % 24f;
        float sinceStart = (hour - fogStartHour + 24f) % 24f;

        return length > 0f && sinceStart < length;
    }

    void Apply()
    {
        // The night look: between "power on" and "power out"
        float darkDistance = visibleDistance + torchExtraDistance * torchFogBoost * torchFactor;
        float nightDistance = Mathf.Lerp(litVisibleDistance, darkDistance, outFactor);
        float nightDensity = 1.73f / Mathf.Max(2f, nightDistance); // exp-squared: about 95% hidden at that distance
        Color nightColor = Color.Lerp(litFogColor, fogColor, outFactor);

        // ...blended with the day look
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(dayDensity, nightDensity, night);
        RenderSettings.fogColor = Color.Lerp(dayFogColor, nightColor, Mathf.Clamp01(night * 3f));

        // Ambient light and reflections
        float ambientNight = Mathf.Lerp(litAmbient, nightAmbient, outFactor);
        float reflectionNight = Mathf.Lerp(litReflections, nightReflections, outFactor);
        float ambient = Mathf.Lerp(1f, ambientNight, night);

        RenderSettings.ambientIntensity = dayAmbientIntensity * ambient;
        RenderSettings.ambientLight = Dim(dayAmbientLight, ambient);
        RenderSettings.ambientSkyColor = Dim(dayAmbientSky, ambient);
        RenderSettings.ambientEquatorColor = Dim(dayAmbientEquator, ambient);
        RenderSettings.ambientGroundColor = Dim(dayAmbientGround, ambient);
        RenderSettings.reflectionIntensity = dayReflectionIntensity * Mathf.Lerp(1f, reflectionNight, night);

        // The sky lighting is recalculated only now and then (it is slow)
        bool changed = Mathf.Abs(ambient - lastAmbient) > 0.03f;
        bool settled = !Mathf.Approximately(ambient, lastAmbient) && (night <= 0f || night >= 1f) && (outFactor <= 0f || outFactor >= 1f);
        if ((changed || settled) && Time.unscaledTime >= nextEnvironmentUpdate)
        {
            lastAmbient = ambient;
            nextEnvironmentUpdate = Time.unscaledTime + 0.15f;
            DynamicGI.UpdateEnvironment();
        }
    }

    static Color Dim(Color color, float factor)
    {
        return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}
