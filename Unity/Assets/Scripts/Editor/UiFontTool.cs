using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

// Tools > Ghost > Setup UI Fonts And Icons
//  - makes the TextMeshPro fonts of the Settings menu (Inter Medium / SemiBold, Cinzel Bold, Rajdhani SemiBold)
//    from the font files in Assets/FONTS/UI and puts them in Assets/Resources/UIFonts
//  - sets the Lucide icons in Assets/Resources/UIIcons up as Sprites
// "Setup Settings" runs this by itself.
public static class UiFontTool
{
    const string FontFolder = "Assets/FONTS/UI";
    const string AssetFolder = "Assets/Resources/UIFonts";
    const string IconFolder = "Assets/Resources/UIIcons";

    static readonly string[,] Fonts =
    {
        { "Inter-Medium", "Inter Medium SDF" },
        { "Inter-SemiBold", "Inter SemiBold SDF" },
        { "Cinzel-Bold", "Cinzel Bold SDF" },
        { "Rajdhani-SemiBold", "Rajdhani SemiBold SDF" },
        { "DMSans-Medium", "DM Sans Medium SDF" },
        { "DMSans-SemiBold", "DM Sans SemiBold SDF" },
    };

    [MenuItem("Tools/Ghost/Setup UI Fonts And Icons")]
    public static void Setup()
    {
        AssetDatabase.Refresh();
        int fonts = CreateFonts();
        int icons = SetupIcons();
        Debug.Log("UiFontTool: " + fonts + " UI fonts ready, " + icons + " icons set up as Sprites.");
    }

    public static int CreateFonts()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Resources", "UIFonts");

        string ascii = BuildAscii();
        int ready = 0;

        for (int i = 0; i < Fonts.GetLength(0); i++)
        {
            string assetPath = AssetFolder + "/" + Fonts[i, 1] + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) { ready++; continue; }

            var source = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + Fonts[i, 0] + ".ttf");
            if (source == null)
            {
                Debug.LogWarning("UiFontTool: " + FontFolder + "/" + Fonts[i, 0] + ".ttf is missing. Run Tools/ui/build_ui_assets.py.");
                continue;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(source);
            if (fontAsset == null)
            {
                Debug.LogWarning("UiFontTool: TextMeshPro could not read " + Fonts[i, 0] + ".ttf");
                continue;
            }

            fontAsset.name = Fonts[i, 1];
            fontAsset.isMultiAtlasTexturesEnabled = true;
            fontAsset.TryAddCharacters(ascii, out string missing);

            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = Fonts[i, 1] + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTextures != null)
            {
                for (int a = 0; a < fontAsset.atlasTextures.Length; a++)
                {
                    if (fontAsset.atlasTextures[a] == null) continue;
                    fontAsset.atlasTextures[a].name = Fonts[i, 1] + " Atlas" + (a > 0 ? " " + a : "");
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[a], fontAsset);
                }
            }

            EditorUtility.SetDirty(fontAsset);
            ready++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return ready;
    }

    public static int SetupIcons()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder)) return 0;

        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool already = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single;
            if (!already)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            count++;
        }
        return count;
    }

    static string BuildAscii()
    {
        var sb = new StringBuilder();
        for (int c = 32; c < 127; c++) sb.Append((char)c);
        return sb.ToString();
    }
}
