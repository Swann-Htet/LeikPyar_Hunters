using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float slowSpeed = 2f;

    [Header("Mobile Joystick")]
    public Joystick joystick;
    [Tooltip("Phones move faster than PC so the joystick doesn't feel slow (1 = the PC walk speed)")]
    public float phoneWalkSpeedScale = 1.3f;
    [Tooltip("Joystick reaches full speed sooner (1 = off)")]
    public float phoneJoystickBoost = 1.25f;

    [Header("PC WASD Hint")]
    public GameObject ctrlArrow;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Stairs")]
    [Tooltip("Tallest step the player can walk up (meters)")]
    public float stepOffset = 0.5f;
    [Tooltip("Steepest slope the player can walk up (degrees)")]
    public float slopeLimit = 60f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public Transform playerCamera;

    [Tooltip("A mouse move bigger than this in one frame is a glitch (the browser gives a big jump when the mouse is locked again): it is ignored")]
    public float maxMouseStep = 10f;

    private int ignoreLookFrames;
    private bool wasLocked;
    private bool wasBlocked;

    private CharacterController controller;
    private float verticalVelocity;
    private bool isMobile;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        controller.stepOffset = stepOffset;
        controller.slopeLimit = slopeLimit;

        isMobile = DeviceDetection.IsMobile();

        if (playerCamera != null)
        {
            float initialPitch = playerCamera.localEulerAngles.x;
            if (initialPitch > 180f) initialPitch -= 360f;
            CameraLookState.Pitch = initialPitch;

            // A non-uniformly scaled camera (e.g. Y = 0.67) skews the picture when it looks up or down
            playerCamera.localScale = Vector3.one;

            // Keep the near plane well inside the CharacterController's radius so it
            // can never poke through a wall the capsule is pressed up against.
            Camera cam = playerCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.1f;
            }
        }

        if (ctrlArrow != null)
        {
            ctrlArrow.SetActive(!isMobile);
        }

        // Only lock mouse on PC
        if (!isMobile)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // The Device Simulator can report the phone a moment after the game started
        bool mobileNow = DeviceDetection.IsMobile();
        if (mobileNow != isMobile)
        {
            isMobile = mobileNow;
            if (isMobile)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // PC Escape
        if (!isMobile && Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Nothing moves while the settings (or the upgrades) are open
        if (!UiLock.Open) Move();

        // PC mouse look only — mobile look is driven exclusively by the
        // CameraTouchArea drag zones (MobileCameraLook), never from here.
        // (not while the backpack is open: the mouse is free then)
        bool blocked = Inventory.BackpackOpen || UiLock.Open || HideSystem.PlayerHidden;
        if (wasBlocked && !blocked) ignoreLookFrames = 3;      // the first mouse move after a panel closes can be a big jump
        wasBlocked = blocked;

        if (!isMobile && !blocked)
        {
            Look();
        }
    }

    void Move()
    {
        float horizontal;
        float vertical;

        // =========================
        // MOBILE
        // =========================

        if (isMobile)
        {
            horizontal = joystick != null ? joystick.Horizontal : 0f;
            vertical = joystick != null ? joystick.Vertical : 0f;
        }
        // =========================
        // PC
        // =========================
        else
        {
            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");
        }

        Vector3 direction =
            transform.right * horizontal +
            transform.forward * vertical;

        if (isMobile)
        {
            direction = Vector3.ClampMagnitude(direction * phoneJoystickBoost, 1f);
        }
        else if (direction.magnitude > 1f)
        {
            // Prevent diagonal movement from being faster
            direction.Normalize();
        }

        // =========================
        // MOVEMENT SPEED
        // =========================

        float currentSpeed = walkSpeed;

        if (isMobile)
        {
            currentSpeed *= phoneWalkSpeedScale;
        }

        // Run
        if (!isMobile && Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = runSpeed;
        }
        // Slow walk
        else if (!isMobile && Input.GetKey(KeyCode.LeftControl))
        {
            currentSpeed = slowSpeed;
        }

        // =========================
        // HORIZONTAL MOVEMENT
        // =========================

        controller.Move(
            direction * currentSpeed * Time.deltaTime
        );

        // =========================
        // GROUND CHECK
        // =========================

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
            {
                verticalVelocity = -2f;
            }

            // PC jump
            if ((!isMobile && Input.GetKeyDown(KeyCode.Space)) || (isMobile && MobileInput.GetJump()))
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        // =========================
        // GRAVITY
        // =========================

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move(
            Vector3.up * verticalVelocity * Time.deltaTime
        );
    }

    void Look()
    {
        // The mouse is free (Esc in the browser, or the window lost the mouse): it must not turn the camera. A click takes it back.
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            wasLocked = false;
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                ignoreLookFrames = 3;
            }
            return;
        }

        // the mouse was just locked: the first moves are not the player's
        if (!wasLocked)
        {
            wasLocked = true;
            ignoreLookFrames = 3;
        }
        if (ignoreLookFrames > 0)
        {
            ignoreLookFrames--;
            return;
        }

        // The player's own setting (Settings panel) multiplies the sensitivity set here
        float sensitivity = mouseSensitivity * GameSettings.MouseSensitivity;

        float rawX = Input.GetAxis("Mouse X");
        float rawY = Input.GetAxis("Mouse Y");
        if (Mathf.Abs(rawX) > maxMouseStep || Mathf.Abs(rawY) > maxMouseStep) return;     // a glitch of the browser

        float mouseX = rawX * sensitivity;

        float mouseY = rawY * sensitivity * (GameSettings.InvertY ? -1f : 1f);

        // Player rotates horizontally
        transform.Rotate(Vector3.up * mouseX);

        // Camera rotates vertically (shared state with mobile touch look)
        CameraLookState.Pitch = Mathf.Clamp(
            CameraLookState.Pitch - mouseY,
            -80f,
            80f
        );

        playerCamera.localRotation =
            Quaternion.Euler(
                CameraLookState.Pitch,
                0f,
                0f
            );
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // Only lock mouse on PC
        if (hasFocus && !isMobile && !Inventory.BackpackOpen && !UiLock.Open)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
