using UnityEngine;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Player")]
    public Camera playerCamera;

    [Header("Interaction")]
    public float doorReach = 4.8f;

    [Header("UI")]
    public TextMeshProUGUI interactionText;

    private DoorInteraction currentDoor;

    void Start()
    {
        if (interactionText != null)
        {
            interactionText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (UiLock.Open) return;

        CheckForDoor();

       if (Input.GetKeyDown(KeyCode.F) || MobileInput.GetInteract())
        {
            Interact();
        }
    }

    void CheckForDoor()
    {
        currentDoor = null;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        RaycastHit hit;

        if (Physics.Raycast(
            ray,
            out hit,
            doorReach
        ))
        {
            DoorInteraction door =
                hit.collider.GetComponentInParent<DoorInteraction>();

            if (door != null)
            {
                currentDoor = door;

                ShowInteractionText(
                    door.IsOpen()
                        ? "[F] Close Door"
                        : "[F] Open Door"
                );

                return;
            }
        }

        HideInteractionText();
    }

    void Interact()
    {
        if (currentDoor != null)
        {
            currentDoor.Interact();
        }
    }

    void ShowInteractionText(string message)
    {
        // The HUD draws the prompt (HudPrompt): a key circle and the words, low on the screen
        HudPrompt.Show(message);

        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }

    void HideInteractionText()
    {
        if (interactionText == null)
            return;

        interactionText.gameObject.SetActive(false);
    }
}