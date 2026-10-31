using UnityEngine;
using UnityEngine.SceneManagement;

// The sound of the place: a Day loop by day and a Night loop by night, always playing, and a slow change between them at dusk and dawn.
//   Resources/Ambient/Day.ogg   and   Resources/Ambient/Night.ogg   (made from "Day enviroment" and "Night enviroment" in Assets/SFX)
// "Night" is the same as everywhere else in the game: from the hour the night fog starts (8 PM) to the hour it ends (6 AM).
// It makes itself in the game scene (nothing to set up). The old "BG" sound of the scene that played the Night loop all day is switched off:
// this takes its place.
public class DayNightAmbience : MonoBehaviour
{
    [Range(0f, 1f)] public float dayVolume = 0.3f;
    [Range(0f, 1f)] public float nightVolume = 0.3f;
    [Tooltip("Seconds the sound takes to change from Day to Night (and back)")]
    public float fadeSeconds = 3f;

    private WallClockController clock;
    private NightFog fog;
    private AudioSource day, night;
    private float dayLevel, nightLevel;      // 0..1, how much of each is heard now

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
        if (FindFirstObjectByType<WallClockController>() == null || FindFirstObjectByType<DayNightAmbience>() != null) return;
        new GameObject("Day Night Ambience (auto)").AddComponent<DayNightAmbience>();
    }

    void Start()
    {
        clock = FindFirstObjectByType<WallClockController>();
        fog = FindFirstObjectByType<NightFog>();

        var dayClip = Resources.Load<AudioClip>("Ambient/Day");
        var nightClip = Resources.Load<AudioClip>("Ambient/Night");
        if (dayClip == null && nightClip == null) { enabled = false; return; }

        // the old sound of the scene (the Night loop on the object "BG", on all day) is not needed any more
        foreach (var other in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == null || other.clip == null || other.transform.IsChildOf(transform)) continue;
            string clipName = other.clip.name.ToLowerInvariant();
            if (clipName.Contains("enviroment") || clipName.Contains("environment"))
            {
                other.Stop();
                other.enabled = false;
            }
        }

        day = MakeSource("Day", dayClip);
        night = MakeSource("Night", nightClip);

        // the right loop is heard from the first moment
        bool isNight = IsNight();
        dayLevel = isNight ? 0f : 1f;
        nightLevel = isNight ? 1f : 0f;
        Apply();
    }

    AudioSource MakeSource(string label, AudioClip clip)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        if (clip != null) source.Play();
        return source;
    }

    bool IsNight()
    {
        if (clock == null) return false;

        float start = fog != null ? fog.fogStartHour : 20f;
        float end = fog != null ? fog.fogEndHour : 6f;
        float hour = clock.GameHour24;
        return start <= end ? hour >= start && hour < end : hour >= start || hour < end;
    }

    void Update()
    {
        if (day == null && night == null) return;

        bool isNight = IsNight();
        float step = Time.unscaledDeltaTime / Mathf.Max(0.1f, fadeSeconds);
        dayLevel = Mathf.MoveTowards(dayLevel, isNight ? 0f : 1f, step);
        nightLevel = Mathf.MoveTowards(nightLevel, isNight ? 1f : 0f, step);
        Apply();
    }

    void Apply()
    {
        if (day != null) day.volume = dayLevel * dayVolume;
        if (night != null) night.volume = nightLevel * nightVolume;
    }
}
