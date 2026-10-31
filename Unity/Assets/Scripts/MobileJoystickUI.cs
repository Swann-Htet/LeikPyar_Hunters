using UnityEngine;

public class MobileJoystickUI : MonoBehaviour
{
    void Start()
    {
        // Show joystick only on mobile browsers/devices
        gameObject.SetActive(DeviceDetection.IsMobile());
    }
}
