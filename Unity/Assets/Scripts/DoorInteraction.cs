using UnityEngine;
using System.Collections;

public class DoorInteraction : MonoBehaviour
{
    [Header("Door Settings")]
    public Vector3 openRotation = new Vector3(0f, 90f, 0f);
    public float openDuration = 2f;

    [Header("Interaction")]
    public float interactionDistance = 3f;

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip closeSound;

    private AudioSource audioSource;

    private Quaternion closedRotation;
    private Quaternion openedRotation;

    private bool isOpen = false;
    private bool isMoving = false;

    void Start()
    {
        closedRotation = transform.localRotation;

        // Get the existing X, Y, Z values
        Vector3 currentRotation = transform.localEulerAngles;

        // Change ONLY the values you enter.
        // Each axis is independent.
        Vector3 targetRotation = new Vector3(
            currentRotation.x + openRotation.x,
            currentRotation.y + openRotation.y,
            currentRotation.z + openRotation.z
        );

        openedRotation = Quaternion.Euler(targetRotation);

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    public void Interact()
    {
        if (isMoving)
            return;

        if (isOpen)
        {
            StartCoroutine(CloseDoor());
        }
        else
        {
            StartCoroutine(OpenDoor());
        }
    }

    private IEnumerator OpenDoor()
    {
        isMoving = true;

        if (openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        Quaternion startRotation = transform.localRotation;

        float time = 0f;

        while (time < openDuration)
        {
            time += Time.deltaTime;

            float progress = Mathf.Clamp01(time / openDuration);
            progress = Mathf.SmoothStep(0f, 1f, progress);

            transform.localRotation = Quaternion.Slerp(
                startRotation,
                openedRotation,
                progress
            );

            yield return null;
        }

        transform.localRotation = openedRotation;

        isOpen = true;
        isMoving = false;
    }

    private IEnumerator CloseDoor()
    {
        isMoving = true;

        if (closeSound != null)
        {
            audioSource.PlayOneShot(closeSound);
        }

        Quaternion startRotation = transform.localRotation;

        float time = 0f;

        while (time < openDuration)
        {
            time += Time.deltaTime;

            float progress = Mathf.Clamp01(time / openDuration);
            progress = Mathf.SmoothStep(0f, 1f, progress);

            transform.localRotation = Quaternion.Slerp(
                startRotation,
                closedRotation,
                progress
            );

            yield return null;
        }

        transform.localRotation = closedRotation;

        isOpen = false;
        isMoving = false;
    }

    // Keep this because your PlayerInteraction.cs uses IsOpen()
    public bool IsOpen()
    {
        return isOpen;
    }
}