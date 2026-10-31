using System;
using UnityEngine;

// The player's settings, saved on the device (PlayerPrefs). The Settings panel changes them,
// the game reads them (mouse look, field of view, volume, quality...).
public static class GameSettings
{
    public static event Action Changed;

    private const string MouseKey = "set_mouse";
    private const string TouchKey = "set_touch";
    private const string InvertKey = "set_invert";
    private const string FovKey = "set_fov";
    private const string BrightKey = "set_bright";
    private const string VolumeKey = "set_volume";
    private const string QualityKey = "set_quality";
    private const string FpsKey = "set_fps";
    private const string BatteryKey = "set_battery";

    public const float MinSensitivity = 0.1f;
    public const float MaxSensitivity = 5f;
    public const float MinFov = 50f;
    public const float MaxFov = 100f;
    public const float MinBrightness = 0.5f;
    public const float MaxBrightness = 1.5f;

    // 1 = the sensitivity set on the PlayerController. 2 = twice as fast
    public static float MouseSensitivity
    {
        get
        {
            if (mouseCached < 0f) mouseCached = Mathf.Clamp(PlayerPrefs.GetFloat(MouseKey, 1f), MinSensitivity, MaxSensitivity);
            return mouseCached;
        }
        set
        {
            mouseCached = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            SetFloat(MouseKey, mouseCached);
        }
    }

    private static float mouseCached = -1f;     // the look reads this every frame: it is kept here, not read from the device each time

    // Same for the finger on a phone
    public static float TouchSensitivity
    {
        get => PlayerPrefs.GetFloat(TouchKey, 1f);
        set => SetFloat(TouchKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
    }

    public static bool InvertY
    {
        get => PlayerPrefs.GetInt(InvertKey, 0) == 1;
        set => SetInt(InvertKey, value ? 1 : 0);
    }

    // Degrees. Below 0 = the camera keeps the field of view it has in the scene
    public static float FieldOfView
    {
        get => PlayerPrefs.GetFloat(FovKey, -1f);
        set => SetFloat(FovKey, Mathf.Clamp(value, MinFov, MaxFov));
    }

    // 1 = normal. Lower = darker, higher = brighter
    public static float Brightness
    {
        get => PlayerPrefs.GetFloat(BrightKey, 1f);
        set => SetFloat(BrightKey, Mathf.Clamp(value, MinBrightness, MaxBrightness));
    }

    // 0 to 1, all sounds
    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            SetFloat(VolumeKey, Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
        }
    }

    // Index in Project Settings > Quality. Below 0 = the default of the build
    public static int Quality
    {
        get => PlayerPrefs.GetInt(QualityKey, -1);
        set
        {
            SetInt(QualityKey, value);
            ApplyQuality();
        }
    }

    // The frame rate at the top of the screen (on by default, like in PUBG)
    public static bool ShowFps
    {
        get => PlayerPrefs.GetInt(FpsKey, 1) == 1;
        set => SetInt(FpsKey, value ? 1 : 0);
    }

    // The battery next to it (only shown when the device tells its battery)
    public static bool ShowBattery
    {
        get => PlayerPrefs.GetInt(BatteryKey, 1) == 1;
        set => SetInt(BatteryKey, value ? 1 : 0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Apply();
    }

    // Puts the saved volume and quality into effect (every scene)
    public static void Apply()
    {
        AudioListener.volume = Mathf.Clamp01(Volume);
        ApplyQuality();
    }

    static void ApplyQuality()
    {
        int quality = Quality;
        if (quality < 0 || quality >= QualitySettings.names.Length) return;
        if (quality == QualitySettings.GetQualityLevel()) return;

        QualitySettings.SetQualityLevel(quality, true);
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(MouseKey);
        mouseCached = -1f;
        PlayerPrefs.DeleteKey(TouchKey);
        PlayerPrefs.DeleteKey(InvertKey);
        PlayerPrefs.DeleteKey(FovKey);
        PlayerPrefs.DeleteKey(BrightKey);
        PlayerPrefs.DeleteKey(VolumeKey);
        PlayerPrefs.DeleteKey(QualityKey);
        PlayerPrefs.DeleteKey(FpsKey);
        PlayerPrefs.DeleteKey(BatteryKey);

        AudioListener.volume = 1f;
        Save();
        Changed?.Invoke();
    }

    // Written to the device (called when the panel closes, not on every slider tick)
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        Changed?.Invoke();
    }

    static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        Changed?.Invoke();
    }
}
