using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Between 20:00 and 6:00 the electricity fails a random number of times (2 to 5 each night),
// at random moments. Every lamp (ScaryLamp) flickers, goes dark for a while, then flickers back on.
public class PowerOutage : MonoBehaviour
{
    [Header("When (game clock hours, 24h)")]
    public float startHour = 20f;
    public float endHour = 6f;

    [Header("Outages each night")]
    [Tooltip("A random number between Min and Max happens every night")]
    public int outagesMin = 2;
    public int outagesMax = 5;
    [Tooltip("Seconds the house stays dark: random between these")]
    public float minDuration = 15f;
    public float maxDuration = 40f;
    [Tooltip("The first outage can not happen before this many game hours after the game starts (the game starts with normal light)")]
    public float graceHoursAtStart = 1.5f;
    [Tooltip("No outage in the last game hours of the night")]
    public float edgeHours = 0.4f;
    [Tooltip("At least this many game hours between two outages")]
    public float minGapHours = 0.7f;

    [Header("Flicker")]
    [Tooltip("Seconds the lights flicker before they go out and before they come back")]
    public float flickerSeconds = 1.5f;

    [Header("Sounds (optional)")]
    public AudioClip powerOffClip;
    public AudioClip powerOnClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Test")]
    [Tooltip("Tick in Play mode to start an outage right now")]
    public bool startOutageNow;

    private WallClockController clock;
    private readonly List<float> schedule = new List<float>();
    private int next;
    private bool wasNight;
    private Coroutine routine;
    private AudioSource audioSource;

    public bool IsOut => ScaryLamp.PowerOut;

    void Start()
    {
        clock = FindFirstObjectByType<WallClockController>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void OnDisable()
    {
        ScaryLamp.PowerOut = false;
    }

    void Update()
    {
        if (clock == null) return;

        float length = (endHour - startHour + 24f) % 24f;
        float since = (clock.GameHour24 - startHour + 24f) % 24f;
        bool night = length > 0f && since < length;

        if (night && !wasNight)
        {
            PlanNight(length, since);
        }
        else if (!night && wasNight)
        {
            // The night is over: power is back
            schedule.Clear();
            if (routine != null) StopCoroutine(routine);
            routine = null;
            ScaryLamp.PowerOut = false;
        }
        wasNight = night;

        if (startOutageNow)
        {
            startOutageNow = false;
            if (routine == null) routine = StartCoroutine(OutageRoutine());
        }

        if (night && routine == null && next < schedule.Count && since >= schedule[next])
        {
            next++;
            routine = StartCoroutine(OutageRoutine());
        }
    }

    // Picks tonight's random outage moments (hours since the night began)
    void PlanNight(float length, float since)
    {
        schedule.Clear();
        next = 0;

        int lo = Mathf.Max(0, Mathf.Min(outagesMin, outagesMax));
        int hi = Mathf.Max(lo, Mathf.Max(outagesMin, outagesMax));
        int count = Random.Range(lo, hi + 1);

        // Random moments anywhere in the night, but never right at the start of the game
        float from = Mathf.Max(since + graceHoursAtStart, edgeHours);
        float to = length - edgeHours;
        if (to <= from) from = Mathf.Max(0f, to - 0.5f);

        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float t = Random.Range(from, to);

                bool tooClose = false;
                foreach (var other in schedule)
                {
                    if (Mathf.Abs(other - t) < minGapHours) { tooClose = true; break; }
                }
                if (tooClose) continue;

                schedule.Add(t);
                break;
            }
        }

        schedule.Sort();

        var text = new System.Text.StringBuilder();
        foreach (var t in schedule)
            text.Append(((startHour + t) % 24f).ToString("F1")).Append("h  ");
        Debug.Log("[PowerOutage] Tonight " + schedule.Count + " outage(s) at: " + text);
    }

    IEnumerator OutageRoutine()
    {
        Play(powerOffClip);
        yield return Flicker(flickerSeconds, true);

        float duration = Random.Range(Mathf.Min(minDuration, maxDuration), Mathf.Max(minDuration, maxDuration));
        Debug.Log("[PowerOutage] The electricity is out for " + duration.ToString("F0") + "s");
        yield return new WaitForSeconds(duration);

        Play(powerOnClip);
        yield return Flicker(flickerSeconds, false);

        routine = null;
    }

    // Lamps jump on and off, then settle (off = the power is gone, on = it is back)
    IEnumerator Flicker(float seconds, bool endOff)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            ScaryLamp.PowerOut = Random.value < 0.5f;
            float wait = Random.Range(0.04f, 0.18f);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }
        ScaryLamp.PowerOut = endOff;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, volume);
    }
}
