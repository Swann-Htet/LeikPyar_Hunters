using UnityEngine;
using UnityEngine.Rendering;

// Changes the sky with the game clock: the clear daytime sky in the morning and day, the deep starry sky at night.
// The night hours are the ones of the NightFog (8 PM - 6 AM) unless you set your own.
// Add it with Tools > Ghost > Setup Sky (it also makes the two sky materials).
public class SkyCycle : MonoBehaviour
{
    [Header("Skies (Skybox/Panoramic materials)")]
    public Material morningSky;
    public Material nightSky;

    [Header("When is it night? (game clock, 24h)")]
    [Tooltip("On: use the hours of the NightFog. Off: use the two hours below")]
    public bool useNightFogHours = true;
    public float nightStartHour = 20f;
    public float nightEndHour = 6f;

    [Header("Light in the rooms (dark, only a little blue)")]
    [Tooltip("On: the room light is these three colours and does NOT come from the sky picture. " +
             "(A starry night sky is almost black, so with the sky as the light source the whole house goes black.) " +
             "The NightFog still dims these colours at night and when the power is out")]
    public bool fixedAmbient = true;
    public Color ambientTop = new Color(0.30f, 0.36f, 0.52f, 1f);
    public Color ambientMid = new Color(0.20f, 0.23f, 0.32f, 1f);
    public Color ambientBottom = new Color(0.09f, 0.09f, 0.12f, 1f);
    [Tooltip("Multiplies the three colours above. Lower = darker walls, higher = brighter rooms")]
    [Range(0f, 4f)] public float roomLift = 1.6f;

    [Header("Night look (sent to the NightFog when the game starts)")]
    public bool tuneNightFog = true;
    [Tooltip("Dark haze with a small blue tint")]
    public Color hazeTint = new Color(0.05f, 0.06f, 0.09f, 1f);
    [Tooltip("Meters until the haze hides things (power ON at night)")]
    public float litVisibleDistance = 45f;
    [Range(0f, 1f)] public float litRoomLight = 0.9f;

    [Header("Sun / moon (the directional light)")]
    public bool tuneSun = true;
    [Tooltip("At night the sun light becomes a cold, softly blue moon light")]
    public Color moonShade = new Color(0.62f, 0.72f, 0.95f, 1f);
    public float moonStrength = 1.7f;
    public float sunFadeSeconds = 15f;

    [Header("Light from the sky")]
    [Tooltip("The ambient light of the scene follows the sky when it changes (only matters if Environment > Lighting > Source is Skybox)")]
    public bool updateEnvironment = true;

    private WallClockController clock;
    private NightFog fog;
    private Material current;

    private Light sun;
    private Color daySunColor;
    private float daySunIntensity;
    private float moon;     // 0 = day, 1 = night

    void Awake()
    {
        // Before every Start(): NightFog remembers these colours as its "day" look
        ApplyAmbient();

        if (tuneNightFog)
        {
            var nightFog = FindFirstObjectByType<NightFog>();
            if (nightFog != null)
            {
                nightFog.litFogColor = hazeTint;
                nightFog.litVisibleDistance = litVisibleDistance;
                nightFog.litAmbient = litRoomLight;
            }
        }

        sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && (sun == null || l.intensity > sun.intensity)) sun = l;
        }
        if (sun != null)
        {
            daySunColor = sun.color;
            daySunIntensity = sun.intensity;
        }
    }

    void OnValidate()
    {
        if (fixedAmbient && Application.isPlaying) ApplyAmbient();
    }

    public void ApplyAmbient()
    {
        if (!fixedAmbient) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.ambientSkyColor = ambientTop * roomLift;
        RenderSettings.ambientEquatorColor = ambientMid * roomLift;
        RenderSettings.ambientGroundColor = ambientBottom * roomLift;
    }

    // A phone's browser can not always read the HDR sky pictures (they are float pictures: some GPUs show garbage or odd flat shapes
    // instead of the sky). On a phone the skies use plain 8-bit pictures of the same sky (Resources/Sky), everything else of the
    // material (exposure, tint, mapping) stays.
    static Material PhoneSky(Material original, string resource)
    {
        if (original == null) return null;
        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null) return original;

        var copy = new Material(original) { name = original.name + " (phone)" };
        if (copy.HasProperty("_MainTex")) copy.SetTexture("_MainTex", texture);
        if (copy.HasProperty("_Tex")) copy.SetTexture("_Tex", texture);
        return copy;
    }

    void Start()
    {
        if (DeviceDetection.IsMobile())
        {
            nightSky = PhoneSky(nightSky, "Sky/night_ldr");
            morningSky = PhoneSky(morningSky, "Sky/morning_ldr");
        }

        clock = FindFirstObjectByType<WallClockController>();
        fog = FindFirstObjectByType<NightFog>();
        Apply(true);
    }

    void Update()
    {
        Apply(false);
        UpdateSun();
    }

    void UpdateSun()
    {
        if (!tuneSun || sun == null) return;

        moon = Mathf.MoveTowards(moon, IsNight() ? 1f : 0f, Time.deltaTime / Mathf.Max(0.1f, sunFadeSeconds));
        sun.color = Color.Lerp(daySunColor, moonShade, moon);
        sun.intensity = Mathf.Lerp(daySunIntensity, moonStrength, moon);
    }

    bool IsNight()
    {
        if (clock == null) return true;

        float start = nightStartHour;
        float end = nightEndHour;
        if (useNightFogHours && fog != null)
        {
            start = fog.fogStartHour;
            end = fog.fogEndHour;
        }

        float hour = clock.GameHour24;
        return start <= end
            ? hour >= start && hour < end
            : hour >= start || hour < end;
    }

    void Apply(bool force)
    {
        Material wanted = IsNight() ? nightSky : morningSky;
        if (wanted == null) wanted = IsNight() ? morningSky : nightSky;
        if (wanted == null || (!force && wanted == current)) return;

        current = wanted;
        RenderSettings.skybox = wanted;

        if (updateEnvironment) DynamicGI.UpdateEnvironment();
    }
}
