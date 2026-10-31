using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Ghost > Build Horror Intro Menu
// Builds the intro canvas in the editor (so you SEE it and can move everything):
// full screen video, the game name and the menu in the dark area on the left
// (START NEW GAME, LOAD GAME, CREDITS, QUIT), a credits panel, a hint and a vignette.
// Safe to run again: it reuses the objects it made.
public static class HorrorMenuTool
{
    const string Folder = "Assets/UI/Horror";

    static readonly string[,] Credits =
    {
        { "GAME DESIGNER", "Ayar Min" },
        { "SOUND", "Wai Yan Min Thu" },
        { "DEVELOPER", "Swan Htet" },
        { "3D ART", "May Thinzar Khin" },
    };

    [MenuItem("Tools/Ghost/Build Horror Intro Menu")]
    public static void Build()
    {
        var changer = Object.FindFirstObjectByType<SceneChanger>();
        if (changer == null)
        {
            EditorUtility.DisplayDialog("Horror intro menu", "Open the intro scene first (the one with the SceneChanger).", "OK");
            return;
        }

        Canvas canvas = changer.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Horror intro menu", "No Canvas found in this scene.", "OK");
            return;
        }
        canvas = canvas.rootCanvas;

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Horror intro menu");
        // Components whose script could not be found (they were saved while their class sat in another file)
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);


        var font = FindFont();
        Sprite panel = SaveSprite("HorrorPanel", HorrorUI.PanelTexture(), new Vector4(HorrorUI.PanelBorder, HorrorUI.PanelBorder, HorrorUI.PanelBorder, HorrorUI.PanelBorder));
        Sprite vignette = SaveSprite("HorrorVignette", HorrorUI.VignetteTexture(), Vector4.zero);

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (scaler.referenceResolution.x < 100f) scaler.referenceResolution = new Vector2(1920f, 1080f);
        float k = scaler.referenceResolution.x / 1920f;   // every size below is written for 1920 wide

        Transform root = canvas.transform;

        // ---------- the menu script ----------
        var menu = canvas.GetComponent<IntroMenu>();
        if (menu == null) menu = Undo.AddComponent<IntroMenu>(canvas.gameObject);
        menu.changer = changer;

        // ---------- 1) the video fills the screen ----------
        var screen = FindScreen(root);
        if (screen != null)
        {
            Stretch((RectTransform)screen.transform);
            screen.raycastTarget = false;

            var fitter = screen.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = Undo.AddComponent<AspectRatioFitter>(screen.gameObject);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var rt = screen.texture as RenderTexture;
            fitter.aspectRatio = rt != null && rt.height > 0 ? (float)rt.width / rt.height : 16f / 9f;
        }

        // ---------- 2) the left column ----------
        var container = Get(root, root, "Menu");
        var containerRect = (RectTransform)container.transform;
        containerRect.anchorMin = containerRect.anchorMax = new Vector2(0f, 0.5f);
        containerRect.pivot = new Vector2(0f, 0.5f);
        containerRect.sizeDelta = new Vector2(560f * k, 960f * k);
        containerRect.anchoredPosition = new Vector2(70f * k, 0f);
        containerRect.localScale = Vector3.one;
        if (container.GetComponent<HorrorSkinIgnore>() == null) container.AddComponent<HorrorSkinIgnore>();

        // The game name: two lines, with a dark red glow copy behind it
        const string gameName = "SPECTRAL\n<color=#E5243B>AGENTS</color>";
        var glow = LeftText(root, container.transform, "TitleGlow", "SPECTRAL\nAGENTS", 104f * k, new Color(0.45f, 0.02f, 0.06f, 0.8f),
            new Vector2(5f * k, 318f * k), new Vector2(560f * k, 280f * k), font, 6f, TextAlignmentOptions.TopLeft);
        var title = LeftText(root, container.transform, "Title", gameName, 104f * k, HorrorUI.Bone,
            new Vector2(0f, 324f * k), new Vector2(560f * k, 280f * k), font, 6f, TextAlignmentOptions.TopLeft);
        title.richText = true;
        title.lineSpacing = -18f;
        glow.lineSpacing = -18f;

        var titleFx = title.GetComponent<HorrorTitleFx>();
        if (titleFx == null) titleFx = Undo.AddComponent<HorrorTitleFx>(title.gameObject);
        titleFx.glow = glow;

        var tagline = LeftText(root, container.transform, "Tagline", "SURVIVE THE NIGHT", 30f * k,
            new Color(HorrorUI.Bone.r, HorrorUI.Bone.g, HorrorUI.Bone.b, 0.8f),
            new Vector2(0f, 128f * k), new Vector2(560f * k, 50f * k), font, 16f, TextAlignmentOptions.MidlineLeft);

        // A thin blood-red divider
        var divider = Get(root, container.transform, "Divider", typeof(Image));
        var dividerRect = (RectTransform)divider.transform;
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0f, 0.5f);
        dividerRect.pivot = new Vector2(0f, 0.5f);
        dividerRect.sizeDelta = new Vector2(440f * k, 3f * k);
        dividerRect.anchoredPosition = new Vector2(0f, 82f * k);
        dividerRect.localScale = Vector3.one;
        var dividerImage = divider.GetComponent<Image>();
        dividerImage.sprite = null;
        dividerImage.color = new Color(HorrorUI.Blood.r, HorrorUI.Blood.g, HorrorUI.Blood.b, 0.9f);
        dividerImage.raycastTarget = false;

        // ---------- 3) the four menu items ----------
        var startObject = FindStart(root, changer);
        if (startObject == null) startObject = Get(root, container.transform, "Start", typeof(Image), typeof(Button));

        var tutorialObject = Get(root, container.transform, "Tutorial", typeof(Image), typeof(Button));
        var loadObject = Get(root, container.transform, "LoadGame", typeof(Image), typeof(Button));
        var creditsObject = Get(root, container.transform, "Credits", typeof(Image), typeof(Button));
        var quitObject = Get(root, container.transform, "Quit", typeof(Image), typeof(Button));

        BuildItem(startObject, container.transform, "START NEW GAME", -10f * k, k, font);
        BuildItem(tutorialObject, container.transform, "TUTORIAL", -105f * k, k, font);
        BuildItem(loadObject, container.transform, "LOAD GAME", -200f * k, k, font);
        BuildItem(creditsObject, container.transform, "CREDITS", -295f * k, k, font);
        BuildItem(quitObject, container.transform, "QUIT", -390f * k, k, font);

        Wire(startObject, menu, "StartNewGame");
        Wire(tutorialObject, menu, "StartTutorial");
        Wire(loadObject, menu, "LoadGame");
        Wire(creditsObject, menu, "OpenCredits");
        Wire(quitObject, menu, "Quit");

        // A small red message under the menu (LOAD GAME / QUIT on the web)
        var toast = LeftText(root, container.transform, "Toast", "", 30f * k, HorrorUI.BrightBlood,
            new Vector2(0f, -480f * k), new Vector2(560f * k, 50f * k), font, 8f, TextAlignmentOptions.MidlineLeft);
        menu.toast = toast;

        // ---------- 4) hint at the bottom left ----------
        var hint = LeftText(root, root, "Hint", "HEADPHONES RECOMMENDED   -   PLAY IN THE DARK", 24f * k,
            new Color(HorrorUI.Bone.r, HorrorUI.Bone.g, HorrorUI.Bone.b, 0.5f),
            Vector2.zero, new Vector2(900f * k, 40f * k), font, 8f, TextAlignmentOptions.MidlineLeft);
        var hintRect = (RectTransform)hint.transform;
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0.5f);
        hintRect.anchoredPosition = new Vector2(70f * k, 50f * k);

        // ---------- 5) the vignette (below the menu so the text is not dimmed) ----------
        var vignetteObject = Get(root, root, "HorrorVignette", typeof(Image), typeof(HorrorVignetteFx));
        Stretch((RectTransform)vignetteObject.transform);
        var vignetteImage = vignetteObject.GetComponent<Image>();
        vignetteImage.sprite = vignette;
        vignetteImage.raycastTarget = false;
        vignetteImage.color = new Color(1f, 1f, 1f, 0.75f);
        vignetteObject.GetComponent<HorrorVignetteFx>().strength = 0.75f;

        // ---------- 6) the credits panel ----------
        var creditsPanel = BuildCredits(root, k, font, panel, menu);
        menu.creditsPanel = creditsPanel;

        // ---------- order ----------
        screen?.transform.SetAsFirstSibling();
        vignetteObject.transform.SetSiblingIndex(1);
        container.transform.SetSiblingIndex(2);
        hint.transform.SetSiblingIndex(3);
        creditsPanel.transform.SetSiblingIndex(4);

        // keep the order inside the column: glow behind the title
        glow.transform.SetSiblingIndex(0);
        title.transform.SetSiblingIndex(1);

        var rotate = root.Find("Rotate");
        if (rotate != null) rotate.SetAsLastSibling();

        creditsPanel.SetActive(false);

        // ---------- SceneChanger ----------
        var so = new SerializedObject(changer);
        so.FindProperty("startButton").objectReferenceValue = container;

        var hide = so.FindProperty("hideOnStart");
        var hidden = new GameObject[] { container, hint.gameObject };
        hide.arraySize = hidden.Length;
        for (int i = 0; i < hidden.Length; i++)
            hide.GetArrayElementAtIndex(i).objectReferenceValue = hidden[i];
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(changer);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = container;

        Debug.Log("HorrorMenuTool: the intro menu is built (SPECTRAL AGENTS, START NEW GAME, LOAD GAME, CREDITS, QUIT). " +
                  "Move anything you like, then save the scene. Pictures are in " + Folder);
    }

    // ---------------- building blocks ----------------

    static void BuildItem(GameObject go, Transform container, string text, float y, float k, TMP_FontAsset font)
    {
        go.transform.SetParent(container, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(500f * k, 84f * k);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.localScale = Vector3.one;

        // The old big START button effect is not used here
        var oldFx = go.GetComponent<HorrorButtonFx>();
        if (oldFx != null) Undo.DestroyObjectImmediate(oldFx);

        // Invisible but clickable
        var image = go.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        image.canvasRenderer.cullTransparentMesh = false;   // otherwise clicks go through an invisible image

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        // Label
        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(labelObject, "Label");
            labelObject.transform.SetParent(go.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(40f * k, 0f);
        labelRect.offsetMax = Vector2.zero;
        labelRect.localScale = Vector3.one;
        label.text = text;
        label.fontSize = 50f * k;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = HorrorUI.Bone;
        label.characterSpacing = 8f;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Overflow;
        if (font != null) label.font = font;

        // A diamond that appears on hover
        var marker = Get(go.transform, go.transform, "Marker", typeof(Image));
        var markerRect = (RectTransform)marker.transform;
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(16f * k, 16f * k);
        markerRect.anchoredPosition = new Vector2(14f * k, 0f);
        markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        markerRect.localScale = Vector3.one;
        var markerImage = marker.GetComponent<Image>();
        markerImage.sprite = null;
        markerImage.color = new Color(HorrorUI.BrightBlood.r, HorrorUI.BrightBlood.g, HorrorUI.BrightBlood.b, 0f);
        markerImage.raycastTarget = false;

        // A red line under the text that grows on hover
        var line = Get(go.transform, go.transform, "Line", typeof(Image));
        var lineRect = (RectTransform)line.transform;
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0f, 0f);
        lineRect.sizeDelta = new Vector2(0f, 3f * k);
        lineRect.anchoredPosition = new Vector2(0f, 6f * k);
        lineRect.localScale = new Vector3(0f, 1f, 1f);
        var lineImage = line.GetComponent<Image>();
        lineImage.sprite = null;
        lineImage.color = new Color(HorrorUI.Blood.r, HorrorUI.Blood.g, HorrorUI.Blood.b, 0f);
        lineImage.raycastTarget = false;

        var item = go.GetComponent<HorrorMenuItem>();
        if (item == null) item = Undo.AddComponent<HorrorMenuItem>(go);
        item.label = label;
        item.marker = markerImage;
        item.line = lineImage;
        item.slide = 18f * k;
    }

    static void Wire(GameObject go, IntroMenu menu, string method)
    {
        var button = go.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);

        UnityEngine.Events.UnityAction action = null;
        switch (method)
        {
            case "StartNewGame": action = menu.StartNewGame; break;
            case "LoadGame": action = menu.LoadGame; break;
            case "OpenCredits": action = menu.OpenCredits; break;
            case "CloseCredits": action = menu.CloseCredits; break;
            case "Quit": action = menu.Quit; break;
        }
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    static GameObject BuildCredits(Transform root, float k, TMP_FontAsset font, Sprite panelSprite, IntroMenu menu)
    {
        // A dark layer over the whole screen that also blocks clicks behind it
        var layer = Get(root, root, "CreditsPanel", typeof(Image));
        Stretch((RectTransform)layer.transform);
        var layerImage = layer.GetComponent<Image>();
        layerImage.sprite = null;
        layerImage.color = new Color(0f, 0f, 0f, 0.82f);
        layerImage.raycastTarget = true;
        if (layer.GetComponent<HorrorSkinIgnore>() == null) layer.AddComponent<HorrorSkinIgnore>();

        // The frame
        var frame = Get(root, layer.transform, "Frame", typeof(Image));
        var frameRect = (RectTransform)frame.transform;
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1000f * k, 780f * k);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.localScale = Vector3.one;
        var frameImage = frame.GetComponent<Image>();
        frameImage.sprite = panelSprite;
        frameImage.type = Image.Type.Sliced;
        frameImage.pixelsPerUnitMultiplier = 0.5f / Mathf.Max(0.01f, k);
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;

        var title = CenterText(root, frame.transform, "CreditsTitle", "CREDITS", 90f * k, HorrorUI.BrightBlood,
            new Vector2(0f, 300f * k), new Vector2(900f * k, 120f * k), font, 14f);

        for (int i = 0; i < Credits.GetLength(0); i++)
        {
            var entry = CenterText(root, frame.transform, "Credit" + (i + 1),
                "<size=60%><color=#E5243B>" + Credits[i, 0] + "</color></size>\n" + Credits[i, 1],
                56f * k, HorrorUI.Bone, new Vector2(0f, (150f - i * 120f) * k), new Vector2(900f * k, 110f * k), font, 6f);
            entry.richText = true;
        }

        // BACK
        var back = Get(root, frame.transform, "Back", typeof(Image), typeof(Button));
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.sizeDelta = new Vector2(320f * k, 90f * k);
        backRect.anchoredPosition = new Vector2(0f, -325f * k);
        backRect.localScale = Vector3.one;

        var backImage = back.GetComponent<Image>();
        backImage.sprite = SaveSprite("HorrorButton", HorrorUI.ButtonTexture(), new Vector4(HorrorUI.ButtonBorder, HorrorUI.ButtonBorder, HorrorUI.ButtonBorder, HorrorUI.ButtonBorder));
        backImage.type = Image.Type.Sliced;
        backImage.pixelsPerUnitMultiplier = 0.5f / Mathf.Max(0.01f, k);
        backImage.color = Color.white;
        backImage.raycastTarget = true;

        var backButton = back.GetComponent<Button>();
        backButton.targetGraphic = backImage;
        backButton.transition = Selectable.Transition.ColorTint;
        var colors = backButton.colors;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.highlightedColor = new Color(1f, 0.72f, 0.72f, 1f);
        colors.pressedColor = new Color(0.5f, 0.4f, 0.4f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.08f;
        backButton.colors = colors;

        var label = CenterText(back.transform, back.transform, "Label", "BACK", 52f * k, HorrorUI.Bone, Vector2.zero, new Vector2(320f * k, 90f * k), font, 12f);
        Stretch((RectTransform)label.transform);

        var fx = back.GetComponent<HorrorButtonFx>();
        if (fx == null) fx = Undo.AddComponent<HorrorButtonFx>(back);
        fx.label = label;

        Wire(back, menu, "CloseCredits");
        return layer;
    }

    // ---------------- helpers ----------------

    // Finds an object by name anywhere under the canvas (so a second run reuses it), or makes it. It is moved under 'parent'
    static GameObject Get(Transform searchRoot, Transform parent, string name, params System.Type[] components)
    {
        GameObject found = null;
        foreach (var t in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t != searchRoot && t.name == name) { found = t.gameObject; break; }
        }

        if (found == null)
        {
            found = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(found, name);
        }

        if (found.transform.parent != parent) found.transform.SetParent(parent, false);

        foreach (var type in components)
            if (found.GetComponent(type) == null) Undo.AddComponent(found, type);

        return found;
    }

    static TextMeshProUGUI LeftText(Transform searchRoot, Transform parent, string name, string text, float size, Color color,
        Vector2 position, Vector2 box, TMP_FontAsset font, float spacing, TextAlignmentOptions alignment)
    {
        var go = Get(searchRoot, parent, name, typeof(TextMeshProUGUI));

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;

        return StyleText(go.GetComponent<TextMeshProUGUI>(), text, size, color, font, spacing, alignment);
    }

    static TextMeshProUGUI CenterText(Transform searchRoot, Transform parent, string name, string text, float size, Color color,
        Vector2 position, Vector2 box, TMP_FontAsset font, float spacing)
    {
        var go = Get(searchRoot, parent, name, typeof(TextMeshProUGUI));

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;

        return StyleText(go.GetComponent<TextMeshProUGUI>(), text, size, color, font, spacing, TextAlignmentOptions.Center);
    }

    static TextMeshProUGUI StyleText(TextMeshProUGUI t, string text, float size, Color color, TMP_FontAsset font, float spacing, TextAlignmentOptions alignment)
    {
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
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

    static Sprite SaveSprite(string name, Texture2D texture, Vector4 border)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/UI", "Horror");

        string path = Folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // The RawImage that shows the video (render texture)
    static RawImage FindScreen(Transform root)
    {
        var named = root.Find("Screen");
        if (named != null && named.GetComponent<RawImage>() != null) return named.GetComponent<RawImage>();

        foreach (var raw in root.GetComponentsInChildren<RawImage>(true))
            if (raw.texture is RenderTexture) return raw;
        return null;
    }

    // The old START button from the previous version of the tool (or yours), reused as the first menu item
    static GameObject FindStart(Transform root, SceneChanger changer)
    {
        var named = root.Find("Start");
        if (named == null)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Start" && t.GetComponent<Button>() != null) { named = t; break; }
        }
        return named != null && named.GetComponent<Button>() != null ? named.gameObject : null;
    }
}
