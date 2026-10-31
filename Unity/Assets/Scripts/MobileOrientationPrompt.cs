using UnityEngine;
using UnityEngine.UI;

public class MobileOrientationPrompt : MonoBehaviour
{
    [Header("UI")]
    public GameObject rotatePhonePanel;

    [Header("Settings")]
    public bool landscapeRequired = true;

    private void Start()
    {
        CheckOrientation();
    }

    private void Update()
    {
        CheckOrientation();
    }

    private void CheckOrientation()
    {
        bool isPortrait = Screen.height > Screen.width;

        if (landscapeRequired)
        {
            // Show panel when phone is portrait
            rotatePhonePanel.SetActive(isPortrait);

            // Allow the browser/device to rotate naturally
            Screen.orientation = ScreenOrientation.AutoRotation;

            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
        }
    }
}