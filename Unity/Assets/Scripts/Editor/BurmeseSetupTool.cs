using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Ghost > Setup Burmese Language
//  1. makes the TextMeshPro font asset "BurmeseFont SDF" from the generated font (Assets/FONTS/Burmese/BurmeseClusters.ttf)
//  2. adds a language button (မြန်မာ / ENGLISH) to the top right of the intro menu, if the intro scene is open
// The texts and the font are made by Tools/burmese/build_burmese.py (see Localization.cs).
public static class BurmeseSetupTool
{
    const string FontPath = "Assets/FONTS/Burmese/BurmeseClusters.ttf";
    const string MapPath = "Assets/Resources/Burmese/BurmeseMap.json";
    const string AssetFolder = "Assets/Resources/Burmese";
    const string AssetPath = "Assets/Resources/Burmese/BurmeseFont SDF.asset";

    [System.Serializable] class CharsFile { public string chars; }

    [MenuItem("Tools/Ghost/Setup Burmese Language")]
    public static void Setup()
    {
        AssetDatabase.Refresh();

        var fontAsset = EnsureFontAsset(false);
        if (fontAsset == null) return;

        string buttonResult = "No intro menu in the open scene (open IntroScene and run this again to add the language button).";
        var menu = Object.FindFirstObjectByType<IntroMenu>();
        if (menu != null)
        {
            // Rebuild the menu first: it repairs components that lost their script and gives the menu its effects back
            HorrorMenuTool.Build();
            BuildLanguageButton(menu);
            buttonResult = "Language button added to the intro menu (top right).";
        }

        Debug.Log("BurmeseSetupTool: the Burmese font is ready. " + buttonResult + " Save the scene.");
    }

    [MenuItem("Tools/Ghost/Rebuild Burmese Font Asset")]
    public static void Rebuild()
    {
        AssetDatabase.Refresh();
        EnsureFontAsset(true);
    }

    static TMP_FontAsset EnsureFontAsset(bool recreate)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (existing != null && !recreate) return existing;
        if (existing != null) AssetDatabase.DeleteAsset(AssetPath);

        var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (source == null)
        {
            EditorUtility.DisplayDialog("Burmese language",
                "The font " + FontPath + " is missing.\nRun Tools/burmese/build_burmese.py first.", "OK");
            return null;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(source);
        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("Burmese language", "TextMeshPro could not read " + FontPath + ".", "OK");
            return null;
        }

        fontAsset.name = "BurmeseFont SDF";
        fontAsset.isMultiAtlasTexturesEnabled = true;

        // Put every Burmese syllable glyph in the atlas now (the font can also add glyphs while the game runs)
        string chars = ReadChars();
        if (chars.Length > 0)
        {
            fontAsset.TryAddCharacters(chars, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning("BurmeseSetupTool: " + missing.Length + " glyphs could not be added to the atlas.");
        }

        // Latin letters, numbers and symbols come from the game font
        var fallback = FindGameFont();
        if (fallback != null) fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Burmese");

        AssetDatabase.CreateAsset(fontAsset, AssetPath);

        if (fontAsset.material != null)
        {
            fontAsset.material.name = "BurmeseFont SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures != null)
        {
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                if (fontAsset.atlasTextures[i] == null) continue;
                fontAsset.atlasTextures[i].name = "BurmeseFont SDF Atlas" + (i > 0 ? " " + i : "");
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("BurmeseSetupTool: created " + AssetPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
    }

    static string ReadChars()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath);
        if (asset == null) return "";

        var file = JsonUtility.FromJson<CharsFile>(asset.text);
        return file != null && file.chars != null ? file.chars : "";
    }

    static TMP_FontAsset FindGameFont()
    {
        foreach (var guid in AssetDatabase.FindAssets("MetalMania-Regular SDF t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == "MetalMania-Regular SDF")
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
        return null;
    }

    // ---------------- the language button ----------------

    static void BuildLanguageButton(IntroMenu menu)
    {
        Canvas canvas = menu.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        var scaler = canvas.GetComponent<CanvasScaler>();
        float k = scaler != null && scaler.referenceResolution.x > 100f ? scaler.referenceResolution.x / 1920f : 1f;

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Language button");
        // Components whose script could not be found (they were saved while their class sat in another file)
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);


        Transform existing = null;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == "LanguageButton") { existing = t; break; }

        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("LanguageButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LanguageToggle));
            Undo.RegisterCreatedObjectUndo(go, "Language button");
            go.transform.SetParent(canvas.transform, false);
        }

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(420f * k, 90f * k);
        rect.anchoredPosition = new Vector2(-170f * k, -50f * k);
        rect.localScale = Vector3.one;

        // invisible, only the text shows
        var image = go.GetComponent<Image>();
        image.sprite = null;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        image.canvasRenderer.cullTransparentMesh = false;   // otherwise clicks go through an invisible image

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        // the label
        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(labelObject, "Language label");
            labelObject.transform.SetParent(go.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        labelRect.localScale = Vector3.one;

        var gameFont = FindGameFont();
        if (gameFont != null) label.font = gameFont;
        label.fontSize = 46f * k;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.color = HorrorUI.Bone;
        label.characterSpacing = 6f;
        label.raycastTarget = false;
        label.text = "MYANMAR";

        var toggle = go.GetComponent<LanguageToggle>();
        toggle.label = label;

        // keeps the vignette under it
        go.transform.SetAsLastSibling();
        var rotate = canvas.transform.Find("Rotate");
        if (rotate != null) rotate.SetAsLastSibling();

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
