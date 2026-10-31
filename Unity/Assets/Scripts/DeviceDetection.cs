using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class DeviceDetection
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int JS_IsMobileBrowser();
#endif

    private static bool? cached;

#if UNITY_EDITOR
    // Tools > Ghost > Mobile UI In Editor: shows the phone controls in the Game view of a PC
    public const string EditorPref = "Ghost.MobileUiInEditor";
#endif

    // The letters on the buttons (K, P, C, V, N...) and the keyboard shortcuts: on a computer always; in the Editor also while the phone
    // controls are shown (the Editor is on a computer, a touch screen laptop says "phone" there)
    public static bool KeyHints
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return !IsMobile();
#endif
        }
    }

    public static bool IsMobile()
    {
#if UNITY_EDITOR
        // Not cached in the editor: the Device Simulator (or the menu above) can be switched at any time
        // UnityEngine.Device.* gives the values of the simulated phone from the very first frame (the plain Application /
        // SystemInfo can still say "PC" while the scripts start, so the joystick and buttons would be missed)
        return Input.touchSupported
            || UnityEngine.Device.Application.isMobilePlatform
            || UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld
            || UnityEditor.EditorPrefs.GetBool(EditorPref, false);
#else
        if (cached.HasValue)
            return cached.Value;

#if UNITY_WEBGL
        cached = JS_IsMobileBrowser() == 1;
#elif UNITY_ANDROID || UNITY_IOS
        cached = true;
#else
        cached = false;
#endif
        return cached.Value;
#endif
    }
}
