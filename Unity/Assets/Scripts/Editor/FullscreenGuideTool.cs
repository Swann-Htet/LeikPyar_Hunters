using UnityEditor;
using UnityEngine;

// Tools > Ghost > Preview The Full Screen Guide (Android) / (iPhone)
// Shows the full screen prompt of the phones with its moving picture (FullscreenGuide) in the Game window, so you can see it without making a
// WebGL build. Press Play first. The prompt pauses the game; its button closes it.
// Tip: set the Game window to a phone shape (the drop-down at the top left of the Game window: add 1080 x 2400 for a tall phone) to see the
// vertical layout, and Full HD for the sideways one.
public static class FullscreenGuideTool
{
    [MenuItem("Tools/Ghost/Preview The Full Screen Guide (Android)")]
    static void Android() { Run(false); }

    [MenuItem("Tools/Ghost/Preview The Full Screen Guide (iPhone)")]
    static void Iphone() { Run(true); }

    static void Run(bool ios)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Full screen guide", "Press Play first (the prompt is made in the running game), then run this again.", "OK");
            return;
        }
        MobileFullscreenPrompt.Preview(ios);
    }
}
