using UnityEngine;

// Put on the player's camera by the Inventory. If anything ever switches the camera off or destroys it
// (the Game view then says "No cameras rendering"), this logs a red error with the exact code that did it.
public class CameraWatch : MonoBehaviour
{
    private static bool quitting;

    void OnApplicationQuit()
    {
        quitting = true;
    }

    // A scene change destroys the camera by itself: that is no error. Only a camera that goes while its scene stays is reported,
    // and only as a warning (a red error would also pause the Editor when "Error Pause" is on).
    bool SceneIsGoing => !gameObject.scene.isLoaded;

    void OnDisable()
    {
        if (quitting || SceneIsGoing) return;
        Debug.LogWarning("[CameraWatch] '" + name + "' was DISABLED. Who did it:\n" + System.Environment.StackTrace, this);
    }

    void OnDestroy()
    {
        if (quitting || SceneIsGoing) return;
        Debug.LogWarning("[CameraWatch] '" + name + "' was DESTROYED. Who did it:\n" + System.Environment.StackTrace);
    }
}
