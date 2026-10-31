using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The player's torch (flashlight). Press T to turn it on and off.
// It is a spot light that follows the camera. Add with Tools > Ghost > Setup Night Fog, Torch And Power Outages.
public class Torch : MonoBehaviour
{
    public static bool IsOn { get; private set; }

    [Header("Key")]
    public KeyCode toggleKey = KeyCode.T;
    public bool startOn = false;
    [Tooltip("On phones a TORCH button turns it on and off (there is no T key)")]
    public bool showMobileButton = true;

    [Header("Light")]
    [Tooltip("Optional: your own light. Empty = a spot light is created on the camera")]
    public Light torchLight;
    public float beamRange = 34f;
    [Tooltip("Width of the whole beam (degrees)")]
    public float beamAngle = 72f;
    [Tooltip("Width of the bright center (degrees). Smaller = softer edge")]
    public float beamInnerAngle = 34f;
    public float beamPower = 30f;
    public Color color = new Color(1f, 0.93f, 0.78f, 1f);
    [Tooltip("Shadows are nicer but cost speed (and need Additional Light shadows in the URP asset)")]
    public LightShadows shadows = LightShadows.None;
    [Tooltip("Where the torch is held, relative to the camera (right / down / forward)")]
    public Vector3 localPosition = new Vector3(0.2f, -0.15f, 0.3f);
    [Tooltip("How much the beam wobbles in brightness (0 = steady)")]
    public float flicker = 0.04f;

    [Header("Sound (optional)")]
    public AudioClip toggleClip;
    [Range(0f, 1f)] public float volume = 0.8f;

    private Camera playerCamera;
    private float baseIntensity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        IsOn = false;
    }

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) playerCamera = Camera.main;

        if (torchLight == null && playerCamera != null)
        {
            var go = new GameObject("TorchLight");
            go.transform.SetParent(playerCamera.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;

            torchLight = go.AddComponent<Light>();
            torchLight.type = LightType.Spot;
        }

        if (torchLight != null)
        {
            torchLight.range = beamRange;
            torchLight.spotAngle = beamAngle;
            torchLight.innerSpotAngle = Mathf.Min(beamInnerAngle, beamAngle);
            torchLight.intensity = beamPower;
            torchLight.color = color;
            torchLight.shadows = shadows;
        }

        baseIntensity = beamPower;
        SetOn(startOn);
        BuildMobileButton();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !UiLock.Open)
            Toggle();

        if (IsOn && torchLight != null && flicker > 0f)
            torchLight.intensity = baseIntensity * (1f + (Mathf.PerlinNoise(Time.time * 9f, 0.3f) - 0.5f) * 2f * flicker);
    }

    public void Toggle()
    {
        SetOn(!IsOn);

        // your own clip if you set one, otherwise the click of Resources/PlayerSfx/TorchToggle
        if (toggleClip != null && playerCamera != null)
            AudioSource.PlayClipAtPoint(toggleClip, playerCamera.transform.position, volume);
        else
            PlayerSfx.Play("TorchToggle", volume);
    }

    void SetOn(bool on)
    {
        IsOn = on;
        if (torchLight != null)
        {
            torchLight.enabled = on;
            if (on) torchLight.intensity = baseIntensity;
        }
    }

    void OnDisable()
    {
        IsOn = false;
    }

    private bool mobileButtonBuilt;

    // Called by MobileControls: a phone seen late (Device Simulator) still gets its TORCH button
    public void EnsureMobileButton()
    {
        if (!mobileButtonBuilt) BuildMobileButton();
    }

    // A small TORCH button for phones
    void BuildMobileButton()
    {
        if (!showMobileButton || !DeviceDetection.IsMobile()) return;
        mobileButtonBuilt = true;

        var canvasGO = new GameObject("TorchCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var button = new GameObject("TorchButton", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(canvasGO.transform, false);

        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.anchoredPosition = new Vector2(-30f, -310f);

        button.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        button.GetComponent<Button>().onClick.AddListener(Toggle);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(button.transform, false);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "TORCH";
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        var labelRt = (RectTransform)labelGO.transform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
    }
}
