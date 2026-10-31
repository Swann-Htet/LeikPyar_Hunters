using UnityEngine;

public class MobileInput : MonoBehaviour
{
    // The press stays valid for the whole frame, so several scripts
    // (doors, baby ghost) can all read it without one consuming it.
    private static int interactFrame = -1;
    private static int gunFrame = -1;
    private static int jumpFrame = -1;
    private static int scannerFrame = -1;
    private static int powerFrame = -1;
    private static int rangeFrame = -1;

    public void PressInteract()
    {
        interactFrame = Time.frameCount;
    }

    public static bool GetInteract()
    {
        return interactFrame == Time.frameCount;
    }

    // The GUN button (the G key on a PC)
    public static void PressGun() { gunFrame = Time.frameCount; }
    public static bool GetGun() { return gunFrame == Time.frameCount; }

    // The SCANNER buttons (the R, O and V keys on a PC): take it out / put it away, switch it on / off, change its range
    public static void PressScanner() { scannerFrame = Time.frameCount; }
    public static bool GetScanner() { return scannerFrame == Time.frameCount; }
    public static void PressPower() { powerFrame = Time.frameCount; }
    public static bool GetPower() { return powerFrame == Time.frameCount; }
    public static void PressRange() { rangeFrame = Time.frameCount; }
    public static bool GetRange() { return rangeFrame == Time.frameCount; }

    // The JUMP button (the space key on a PC)
    public static void PressJump() { jumpFrame = Time.frameCount; }
    public static bool GetJump() { return jumpFrame == Time.frameCount; }
}
