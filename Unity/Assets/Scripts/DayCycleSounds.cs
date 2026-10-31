using UnityEngine;
using UnityEngine.SceneManagement;

// Sounds for the turns of the day:
//   morning (6 AM, the night ends)  - the rooster (Resources/Sfx/Rooster)
//   7 AM, the NPC comes             - the car horn (Resources/Sfx/car horn)
//   4 PM, the NPC goes back         - the car driving away (Resources/Sfx/car drive)
//   evening (8 PM, the night begins) - a dog barks (Resources/Sfx/Dog Bark)
// It makes itself in the game scene. Put your own clips in the slots to change them. If a file is missing, the old default sound is used
// (Dawn, Dusk, NpcArrive, NpcLeave in Resources/Sfx).
public class DayCycleSounds : MonoBehaviour
{
    [Header("Clips (empty = the default ones from Resources/Sfx)")]
    public AudioClip dawnClip;
    public AudioClip duskClip;
    public AudioClip npcArriveClip;
    public AudioClip npcLeaveClip;

    [Header("Volume")]
    [Range(0f, 1f)] public float dawnVolume = 0.7f;
    [Range(0f, 1f)] public float duskVolume = 0.75f;
    [Range(0f, 1f)] public float npcVolume = 0.9f;
    [Tooltip("The NPC sounds are heard from this far away (meters)")]
    public float npcHearDistance = 35f;

    private WallClockController clock;
    private NightFog fog;
    private AudioSource ambient;
    private AudioSource npcSource;
    private bool known;
    private bool wasNight;
    private bool npcWasHere;

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
        if (FindFirstObjectByType<WallClockController>() == null || FindFirstObjectByType<DayCycleSounds>() != null) return;
        new GameObject("Day Cycle Sounds (auto)").AddComponent<DayCycleSounds>();
    }

    void Start()
    {
        clock = FindFirstObjectByType<WallClockController>();
        fog = FindFirstObjectByType<NightFog>();

        ambient = Sfx.AddSource(gameObject, false, false, 1f);
        npcSource = Sfx.AddSource(gameObject, true, false, 1f);
        npcSource.maxDistance = npcHearDistance;
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
        if (clock == null) return;

        bool night = IsNight();
        var npc = NpcWorker.Instance;
        bool npcHere = npc != null && npc.IsPresent;

        // the first frame only learns the state: nothing plays at the start of the game
        if (!known)
        {
            known = true;
            wasNight = night;
            npcWasHere = npcHere;
            return;
        }

        if (night != wasNight)
        {
            wasNight = night;
            if (night) PlayAmbient(Pick(duskClip, "Dog Bark", "Dusk"), duskVolume);
            else
            {
                PlayAmbient(Pick(dawnClip, "Rooster", "Dawn"), dawnVolume);
                AgentStats.NightSurvived();      // the agent is alive at dawn: one more night, the score goes to the board
            }
        }

        if (npcHere != npcWasHere)
        {
            npcWasHere = npcHere;
            if (npc != null)
            {
                npcSource.transform.position = npc.transform.position;
                npcSource.volume = npcVolume;
                Sfx.Play(npcSource, npcHere ? Pick(npcArriveClip, "car horn", "NpcArrive") : Pick(npcLeaveClip, "car drive", "NpcLeave"));
            }
        }
    }

    // The clip you gave, else the new sound, else the old default one
    static AudioClip Pick(AudioClip assigned, string wanted, string oldDefault)
    {
        if (assigned != null) return assigned;
        var clip = Resources.Load<AudioClip>("Sfx/" + wanted);
        return clip != null ? clip : Resources.Load<AudioClip>("Sfx/" + oldDefault);
    }

    void PlayAmbient(AudioClip clip, float volume)
    {
        if (clip == null) return;
        ambient.volume = volume;
        Sfx.Play(ambient, clip);
    }
}
