using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Look around by dragging a finger over this area (phones).
// Smooth: the finger movement is not applied in one jump per touch event (touch events come in uneven bursts, which makes
// the picture stutter). It is collected and eased into the camera a little every frame. The speed also does not depend
// on the phone's screen size.
public class MobileCameraLook : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Player")]
    public Transform player;

    [Header("Camera")]
    public Transform playerCamera;

    [Header("Settings")]
    public float sensitivity = 0.08f;
    [Tooltip("Higher = follows the finger faster, lower = smoother. 25-40 feels good")]
    public float smoothing = 30f;

    public float minVerticalAngle = -80f;
    public float maxVerticalAngle = 80f;

    private bool isMobile;
    private int pointerId = int.MinValue;
    private float pendingYaw;
    private float pendingPitch;

    void Awake()
    {
        // A fully transparent area is not "seen" by the UI (it would never get a touch): it must stay in the raycast
        var renderer = GetComponent<CanvasRenderer>();
        if (renderer != null) renderer.cullTransparentMesh = false;

        var graphic = GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
    }

    void Start()
    {
        isMobile = DeviceDetection.IsMobile();

        // Not a touch device: this drag zone would only block clicks meant for
        // the 3D world/UI, so disable it entirely.
        if (!isMobile)
        {
            gameObject.SetActive(false);
            return;
        }

        if (playerCamera != null)
        {
            float initialPitch = playerCamera.localEulerAngles.x;
            if (initialPitch > 180f) initialPitch -= 360f;
            CameraLookState.Pitch = initialPitch;
        }
    }

    void OnDisable()
    {
        pointerId = int.MinValue;
        pendingYaw = pendingPitch = 0f;
    }

    static bool Blocked
    {
        get { return HideSystem.PlayerHidden || UiLock.Open || (Inventory.Instance != null && Inventory.Instance.IsOpen); }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Only one finger steers the camera: the first one on this area
        if (pointerId == int.MinValue) pointerId = eventData.pointerId;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId) pointerId = int.MinValue;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isMobile || Blocked) return;
        if (pointerId != int.MinValue && eventData.pointerId != pointerId) return;

        // Same finger travel = same turn on every phone (the old pixel-based speed was tuned for a 1080 px high screen)
        float screen = 1080f / Mathf.Max(1f, Screen.height);
        float speed = sensitivity * GameSettings.TouchSensitivity * screen;

        // one drag step is never more than a few centimetres of finger: a bigger one is a glitch of the touch screen
        Vector2 step = Vector2.ClampMagnitude(eventData.delta, 120f);

        pendingYaw = Mathf.Clamp(pendingYaw + step.x * speed, -90f, 90f);
        pendingPitch = Mathf.Clamp(pendingPitch + step.y * speed * (GameSettings.InvertY ? -1f : 1f), -90f, 90f);
    }

    void Update()
    {
        if (!isMobile || player == null || playerCamera == null) return;

        // a finger that was lifted without telling us (a panel opened, the page lost the touch) must not hold the look
        if (pointerId != int.MinValue && Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            pointerId = int.MinValue;
            pendingYaw = pendingPitch = 0f;
        }

        if (Blocked)
        {
            pendingYaw = pendingPitch = 0f;
            return;
        }

        if (Mathf.Abs(pendingYaw) < 0.0005f && Mathf.Abs(pendingPitch) < 0.0005f) return;

        // Take a part of what is waiting: quick at the start of a swipe, soft at the end
        float take = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        float yaw = pendingYaw * take;
        float pitch = pendingPitch * take;
        pendingYaw -= yaw;
        pendingPitch -= pitch;

        // Rotate player horizontally
        player.Rotate(Vector3.up * yaw);

        // Rotate camera vertically (shared state, doesn't reset between zones)
        CameraLookState.Pitch = Mathf.Clamp(CameraLookState.Pitch - pitch, minVerticalAngle, maxVerticalAngle);
        playerCamera.localRotation = Quaternion.Euler(CameraLookState.Pitch, 0f, 0f);
    }
}
