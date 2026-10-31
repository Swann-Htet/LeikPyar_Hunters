using UnityEditor;

// Tools > Ghost > Mobile UI In Editor
// Tick it and press Play: the Game view of the PC shows the phone controls (joystick, buttons, touch look),
// the same as the Device Simulator does. Un-tick it for the normal PC view.
public static class MobileUiMenu
{
    const string MenuPath = "Tools/Ghost/Mobile UI In Editor";

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(DeviceDetection.EditorPref, false);
        EditorPrefs.SetBool(DeviceDetection.EditorPref, on);
    }

    [MenuItem(MenuPath, true)]
    static bool Validate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(DeviceDetection.EditorPref, false));
        return !EditorApplication.isPlaying;
    }
}
