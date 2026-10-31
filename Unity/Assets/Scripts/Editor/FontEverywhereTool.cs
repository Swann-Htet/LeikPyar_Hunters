using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts the MetalMania font on every TextMeshPro text: the ones in the open scene, and (as the TMP default)
// every text that is created while the game runs (prompts, buttons, backpack...).
public static class FontEverywhereTool
{
    private const string FontName = "MetalMania-Regular SDF";

    [MenuItem("Tools/Ghost/Use MetalMania Font Everywhere")]
    public static void Apply()
    {
        TMP_FontAsset font = null;
        foreach (var guid in AssetDatabase.FindAssets(FontName + " t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == FontName)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                break;
            }
        }

        if (font == null)
        {
            EditorUtility.DisplayDialog("Font", "Font '" + FontName + "' not found in the project.", "OK");
            return;
        }

        // 1) Default font for texts created by scripts while the game runs
        string defaultResult = "TMP default font not changed";
        var settings = TMP_Settings.instance;
        if (settings != null)
        {
            var serialized = new SerializedObject(settings);
            var property = serialized.FindProperty("m_defaultFontAsset");
            if (property != null)
            {
                property.objectReferenceValue = font;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                defaultResult = "TMP default font = " + font.name;
            }
        }

        // 2) Every text in the open scene
        int count = 0;
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font == font) continue;

            Undo.RecordObject(text, "MetalMania font");
            text.font = font;
            EditorUtility.SetDirty(text);
            count++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("FontEverywhereTool: " + count + " text(s) in the scene now use " + font.name + ". " + defaultResult + ". Save the scene.");
    }
}
