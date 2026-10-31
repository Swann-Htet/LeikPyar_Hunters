using UnityEngine;

// One place for the on-screen interaction text, so the gun, generator and inventory never
// fight over it. Call Request() every frame the message should be visible.
// The HUD (HudPrompt) draws it: a key circle and the words, low on the screen.
public class InteractPrompt : MonoBehaviour
{
    private static InteractPrompt instance;
    private static string message;
    private static int requestFrame = -1;

    public static void Request(string text)
    {
        if (instance == null)
            instance = new GameObject("InteractPrompt").AddComponent<InteractPrompt>();

        message = text;
        requestFrame = Time.frameCount;
    }

    void LateUpdate()
    {
        if (requestFrame == Time.frameCount)
            HudPrompt.Show(message);
    }
}
