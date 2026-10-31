using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Ghost > Setup Settings   (run it in the game scene, and again in the intro scene)
//  - adds the "Settings" object (SettingsPanel) with your gear picture (Assets/ICONS/setting.png) and the game font
//  - puts the gear button INTO your canvas, so you can see it in the Game view and move it
//  - in the game scene it also puts the status bar (FPS + battery) at the top of the HUD canvas
public static class SettingsSetupTool
{
    [MenuItem("Tools/Ghost/Setup Settings")]
    public static void Setup()
    {
        // the fonts (Inter, Space Grotesk, Rajdhani) and the Lucide icons of the new look
        AssetDatabase.Refresh();
        UiFontTool.CreateFonts();
        UiFontTool.SetupIcons();

        var intro = Object.FindFirstObjectByType<IntroMenu>();
        var player = Object.FindFirstObjectByType<PlayerController>();
        Canvas canvas = intro != null ? intro.GetComponentInParent<Canvas>() : FindHudCanvas();
        if (canvas == null && intro != null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Setup Settings", "No canvas found in this scene.", "OK");
            return;
        }
        canvas = canvas.rootCanvas;

        var scaler = canvas.GetComponent<CanvasScaler>();
        float k = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution.x > 100f
            ? scaler.referenceResolution.x / 1920f
            : 1f;

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Setup settings");

        // components whose script went missing are removed
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        // 1) the Settings object
        var panel = Object.FindFirstObjectByType<SettingsPanel>();
        if (panel == null)
        {
            var go = new GameObject("Settings");
            Undo.RegisterCreatedObjectUndo(go, "Settings");
            panel = go.AddComponent<SettingsPanel>();
        }
        Undo.RecordObject(panel, "Setup settings");

        var icon = FindGearIcon();
        if (icon != null) panel.gearIcon = icon;

        var font = FindFont();
        if (font != null) panel.font = font;

        // 2) the gear button inside your canvas
        string note = "";
        Transform existing = FindByName(canvas.transform, "SettingsButton");
        Button gear = existing != null ? existing.GetComponent<Button>() : null;

        if (gear == null)
        {
            if (icon != null)
            {
                Vector2 position = intro != null ? new Vector2(-30f * k, -30f * k) : new Vector2(-40f * k, -150f * k);
                gear = SettingsPanel.BuildGearButton(canvas.transform, icon, position, 90f * k);
                Undo.RegisterCreatedObjectUndo(gear.gameObject, "Settings button");
            }
            else
            {
                note += " The gear picture Assets/ICONS/setting.png was not found as a Sprite: the game draws a gear instead.";
            }
        }

        if (gear != null)
        {
            panel.gearButton = gear;
            gear.transform.SetAsLastSibling();
        }

        // 3) the status bar (FPS + battery) in the game
        if (player != null && Object.FindFirstObjectByType<SystemStatus>() == null)
        {
            var status = SystemStatus.Build(canvas.transform, font, k);
            Undo.RegisterCreatedObjectUndo(status.gameObject, "Status bar");
        }

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = gear != null ? gear.gameObject : panel.gameObject;

        Debug.Log("SettingsSetupTool: Settings are ready in '" + canvas.name + "' (gear button" + (player != null ? ", FPS and battery at the top" : "") + "). Key P or Esc also opens them." + note + " Save the scene.");
    }

    // The canvas with the clock and the health bar
    static Canvas FindHudCanvas()
    {
        Canvas fallback = null;

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || !c.gameObject.scene.IsValid()) continue;

            if (c.transform.Find("Clock") != null || c.transform.Find("HealthBar") != null) return c;

            string n = c.name;
            bool ours = n == "UpgradeCanvas" || n == "InventoryCanvas" || n == "SettingsCanvas" || n == "TorchCanvas" ||
                        n == "BrightnessCanvas" || n == "FullscreenPromptCanvas";
            if (!ours && fallback == null) fallback = c;
        }
        return fallback;
    }

    static Transform FindByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) return t;
        return null;
    }

    static TMP_FontAsset FindFont()
    {
        foreach (var guid in AssetDatabase.FindAssets("MetalMania-Regular SDF t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == "MetalMania-Regular SDF")
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
        return null;
    }

    static Sprite FindGearIcon()
    {
        string[] paths = { "Assets/ICONS/setting.png", "Assets/ICONS/settings.png" };
        foreach (var path in paths)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }

        foreach (var guid in AssetDatabase.FindAssets("setting t:Sprite"))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
            if (sprite != null) return sprite;
        }
        return null;
    }
}
