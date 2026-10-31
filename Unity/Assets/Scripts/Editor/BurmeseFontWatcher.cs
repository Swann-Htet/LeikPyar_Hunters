using UnityEditor;

// When Tools/burmese/build_burmese.py makes a new BurmeseClusters.ttf (for example after the font MM.ttf was chosen),
// the TextMeshPro font asset is rebuilt by itself, so no menu has to be clicked.
public class BurmeseFontWatcher : AssetPostprocessor
{
    const string FontPath = "Assets/FONTS/Burmese/BurmeseClusters.ttf";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (path != FontPath) continue;

            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) BurmeseSetupTool.Rebuild();
            };
            return;
        }
    }
}
