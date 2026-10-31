using UnityEngine;
using TMPro;
using System;

public class WallClockController : MonoBehaviour
{
    [Header("Clock Hands")]
    public Transform hourHand;
    public Transform minuteHand;
    public Transform pendulum;

    [Header("Time Display")]
    public TMP_Text timeText;

    [Header("Clock Settings")]
    [Tooltip("1 real minute = 1 game hour")]
    public float realSecondsPerGameHour = 60f;

    [Tooltip("Starting game time")]
    [Range(0, 23)]
    public int startHour = 12;

    [Range(0, 59)]
    public int startMinute = 0;

    [Header("Pendulum")]
    public float pendulumAngle = 7f;
    public float pendulumSpeed = 1f;

    [Header("Hourly Sound")]
    public AudioSource audioSource;
    public AudioClip hourlyMP3;

    private float elapsedRealTime;
    private float gameTimeInMinutes;

    // Store the previous hour
    private int previousHour;

    private const float Hour12Z = 270f;
    private const float Minute12Z = 52f;

    public float GameHour24 => gameTimeInMinutes / 60f;

    public void SetGameTime(int hour, int minute)
    {
        gameTimeInMinutes = hour * 60f + minute;
        previousHour = Mathf.FloorToInt(gameTimeInMinutes / 60f);
        UpdateClockVisuals();
    }

    void Start()
    {
        gameTimeInMinutes = startHour * 60f + startMinute;

        // Set initial hour
        previousHour = Mathf.FloorToInt(gameTimeInMinutes / 60f);

        UpdateClockVisuals();
    }

    void Update()
    {
        // 1 real minute = 1 game hour
        elapsedRealTime += Time.deltaTime;

        float gameMinutesPerRealSecond = 60f / realSecondsPerGameHour;

        gameTimeInMinutes += gameMinutesPerRealSecond * Time.deltaTime;

        // Keep time between 0 and 24 hours
        if (gameTimeInMinutes >= 1440f)
        {
            gameTimeInMinutes -= 1440f;
        }

        // =========================
        // CHECK HOUR CHANGE
        // =========================

        int currentHour = Mathf.FloorToInt(gameTimeInMinutes / 60f);

        if (currentHour != previousHour)
        {
            PlayHourlySound();

            previousHour = currentHour;
        }

        UpdateClockVisuals();
        UpdatePendulum();
    }

    void PlayHourlySound()
    {
        if (audioSource != null && hourlyMP3 != null)
        {
            audioSource.PlayOneShot(hourlyMP3);
        }
    }

    void UpdateClockVisuals()
    {
        float totalMinutes = gameTimeInMinutes;

        int hour24 = Mathf.FloorToInt(totalMinutes / 60f);
        int minute = Mathf.FloorToInt(totalMinutes % 60f);

        // =========================
        // HOUR HAND
        // =========================

        // 12 = Z 270
        // 3  = Z 0
        // Clockwise direction

        float hourRotation = 270f + (totalMinutes / 60f) * 30f;
        hourRotation %= 360f;

        hourHand.localRotation = Quaternion.Euler(
            hourHand.localEulerAngles.x,
            hourHand.localEulerAngles.y,
            hourRotation
        );

        // =========================
        // MINUTE HAND
        // =========================

        // 12 = Z 52
        // 3  = Z 322
        // Clockwise direction

        float minuteRotation = 321f + (totalMinutes % 60f) * 6f;
        minuteRotation %= 360f;

        minuteHand.localRotation = Quaternion.Euler(
            minuteHand.localEulerAngles.x,
            minuteHand.localEulerAngles.y,
            minuteRotation
        );

        // =========================
        // TEXT
        // =========================

        int displayHour = hour24 % 12;

        if (displayHour == 0)
            displayHour = 12;

        string period = hour24 >= 12 ? "PM" : "AM";

        if (timeText != null)
        {
            timeText.text = $"{displayHour:00}:{minute:00} {period}";
        }
    }

    void UpdatePendulum()
    {
        // Smooth pendulum movement:
        // +7 -> -7 -> +7

        float angle = Mathf.Sin(Time.time * pendulumSpeed) * pendulumAngle;

        Vector3 rotation = pendulum.localEulerAngles;

        rotation.z = angle;

        pendulum.localRotation = Quaternion.Euler(rotation);
    }
}