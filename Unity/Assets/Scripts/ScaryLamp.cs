using System.Collections;
using UnityEngine;

public class ScaryLamp : MonoBehaviour
{
    [Header("Lights")]
    [SerializeField] private Light lampLight;
    [SerializeField] private Light roomLight;

    [Header("Normal Intensity")]
    [SerializeField] private float lampNormalIntensity = 4f;
    [SerializeField] private float roomNormalIntensity = 2f;

    [Header("Flicker")]
    [SerializeField] private float minFlicker = 0f;
    [SerializeField] private float maxFlicker = 4f;

    [Header("Scary Event Timing")]
    [SerializeField] private float minScaryDelay = 3f;
    [SerializeField] private float maxScaryDelay = 10f;

    [Header("Blackout")]
    [SerializeField] private float blackoutDuration = 0.8f;

    [Header("Flash")]
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private float flashMultiplier = 2f;

    private bool scaryEventRunning = false;
    private bool forcedOff = false;

    public static readonly System.Collections.Generic.List<ScaryLamp> All =
        new System.Collections.Generic.List<ScaryLamp>();

    // True while the house has no electricity: every lamp is dark (set by PowerOutage)
    public static bool PowerOut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPower()
    {
        PowerOut = false;
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    // Ghost spawn: keeps this lamp completely off until released.
    // Position of the actual light bulb(s); the component may sit on a far-away parent.
    public Vector3 LightPosition
    {
        get
        {
            if (lampLight != null && roomLight != null)
                return (lampLight.transform.position + roomLight.transform.position) * 0.5f;
            if (lampLight != null) return lampLight.transform.position;
            if (roomLight != null) return roomLight.transform.position;
            return transform.position;
        }
    }

    public void SetForcedOff(bool off)
    {
        forcedOff = off;
        SetLights(lampNormalIntensity, roomNormalIntensity);
    }

    // Ghost spawn: play the scary glitch sequence once.
    public void TriggerGlitch()
    {
        if (!scaryEventRunning && !forcedOff)
            StartCoroutine(ScaryFlash());
    }

    private void Start()
    {
        if (lampLight == null)
            lampLight = GetComponentInChildren<Light>();

        SetLights(
            lampNormalIntensity,
            roomNormalIntensity
        );

        StartCoroutine(ScaryLoop());
    }

    private void Update()
    {
        if (scaryEventRunning)
            return;

        // Normal small random flicker
        if (Random.value < 0.015f)
        {
            float lampIntensity = Random.Range(
                minFlicker,
                maxFlicker
            );

            // Room light is weaker than the lamp
            float roomIntensity = lampIntensity * 0.5f;

            SetLights(
                lampIntensity,
                roomIntensity
            );
        }
        else
        {
            SetLights(
                lampNormalIntensity,
                roomNormalIntensity
            );
        }
    }

    private IEnumerator ScaryLoop()
    {
        while (true)
        {
            float delay = Random.Range(
                minScaryDelay,
                maxScaryDelay
            );

            yield return new WaitForSeconds(delay);

            yield return StartCoroutine(ScaryFlash());
        }
    }

    private IEnumerator ScaryFlash()
    {
        scaryEventRunning = true;

        // ==========================================
        // 1. Unstable flickering
        // ==========================================

        for (int i = 0; i < 10; i++)
        {
            float lampIntensity = Random.Range(
                0f,
                lampNormalIntensity
            );

            float roomIntensity = lampIntensity * 0.4f;

            SetLights(
                lampIntensity,
                roomIntensity
            );

            yield return new WaitForSeconds(
                Random.Range(0.03f, 0.12f)
            );
        }

        // ==========================================
        // 2. COMPLETE BLACKOUT
        // ==========================================

        SetLights(0f, 0f);

        yield return new WaitForSeconds(
            blackoutDuration
        );

        // ==========================================
        // 3. BIG FLASH
        // ==========================================

        SetLights(
            lampNormalIntensity * flashMultiplier,
            roomNormalIntensity * 2f
        );

        yield return new WaitForSeconds(
            flashDuration
        );

        // ==========================================
        // 4. BLACKOUT AGAIN
        // ==========================================

        SetLights(0f, 0f);

        yield return new WaitForSeconds(0.15f);

        // ==========================================
        // 5. RAPID FLICKERING
        // ==========================================

        for (int i = 0; i < 15; i++)
        {
            float lampIntensity = Random.Range(
                0f,
                lampNormalIntensity * 1.5f
            );

            float roomIntensity = lampIntensity * 0.4f;

            SetLights(
                lampIntensity,
                roomIntensity
            );

            yield return new WaitForSeconds(
                Random.Range(0.02f, 0.08f)
            );
        }

        // ==========================================
        // 6. RETURN TO NORMAL
        // ==========================================

        SetLights(
            lampNormalIntensity,
            roomNormalIntensity
        );

        scaryEventRunning = false;
    }

    private void SetLights(
        float lampIntensity,
        float roomIntensity
    )
    {
        // The ghost turned this lamp off, or the whole house has no electricity (PowerOutage)
        if (forcedOff || PowerOut)
        {
            lampIntensity = 0f;
            roomIntensity = 0f;
        }

        if (lampLight != null)
            lampLight.intensity = lampIntensity;

        if (roomLight != null)
            roomLight.intensity = roomIntensity;
    }
}