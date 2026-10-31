using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.UI;

public class SceneChanger : MonoBehaviour
{
    [Header("Video Player")]
    public VideoPlayer videoPlayer;

    [Header("StreamingAssets Video (lobby background)")]
    [Tooltip("Path inside Assets/StreamingAssets, e.g. VIDEOS/Lobby.mp4")]
    public string backgroundVideo = "VIDEOS/Lobby.mp4";

    [Header("Loading Photo")]
    [Tooltip("Shown full screen while loading. Empty = Resources/Loading.png")]
    public Texture loadingImage;
    public string loadingImageResource = "Loading";

    [Header("UI")]
    public RawImage sleepingText;
    public GameObject startButton;
    [Tooltip("Everything else that disappears when Start is pressed (title, texts...)")]
    public GameObject[] hideOnStart;

    [Header("Scene")]
    public string sceneName = "SampleScene";

    [Header("Loading screens (the game enters when the player presses a key)")]
    [Tooltip("The loading pictures (drag them here: Assets/UI/Loading). One is shown each time, never the same one twice in a row. Empty = the folder below, then the picture above")]
    public Texture[] loadingScreens;
    [Tooltip("Folder in Assets/Resources with the loading pictures (put all of them there: any number). The game shows one of them and never the same one twice in a row. Empty folder = the picture above")]
    public string loadingScreensFolder = "LoadingScreens";
    [Tooltip("A loading picture stays at least this long, in seconds")]
    public float minShowSeconds = 3f;
    [Tooltip("When the game is loaded the player must press Enter (or tap, on a phone) to go in. Until then the bar stays at 100%")]
    public bool waitForKey = true;

    [Header("Loading Time (the old fixed wait: not used any more, the game now loads for real)")]
    public float delayBeforeScene = 10f;

    private const float BarHeight = 6f;

    private bool isLoading = false;
    private bool skipKeyWait = false;
    private RectTransform barFill;
    private TMP_Text promptText;
    private TMP_Text percentText;

    private void Start()
    {
        // Play background video
        PlayVideo(backgroundVideo, true);
    }

    // The TRAINING: its own scene when it is in the build, otherwise the game scene in training mode
    public void GoToTutorialScene()
    {
        string path = "Assets/Scenes/" + TutorialMode.SceneName + ".unity";
        if (SceneUtility.GetBuildIndexByScenePath(path) >= 0)
        {
            sceneName = TutorialMode.SceneName;
        }
        else
        {
            sceneName = TutorialMode.GameSceneName;
            TutorialMode.Requested = true;
        }

        // the training is a short step: no long loading wait and no key to press
        minShowSeconds = Mathf.Min(minShowSeconds, 1f);
        skipKeyWait = true;
        GoToSampleScene();
    }

    public void GoToSampleScene()
    {
        if (isLoading)
            return;

        isLoading = true;

        // Hide "အိပ်ရာဝင်ချိန်"
        if (sleepingText != null)
        {
            sleepingText.gameObject.SetActive(false);
        }

        // Hide "စတင်မည်"
        if (startButton != null)
        {
            startButton.SetActive(false);
        }

        if (hideOnStart != null)
        {
            foreach (var go in hideOnStart)
                if (go != null) go.SetActive(false);
        }

        // Lobby video -> Loading photo
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.Stop();
        }
        ShowLoadingImage();

        // the scene loads in the background; the player enters with the Enter key
        StartCoroutine(LoadGame());
    }

    // A full screen photo (covers the screen, keeps its proportions) on top of everything
    private void ShowLoadingImage()
    {
        Texture texture = PickLoadingScreen();
        if (texture == null)
        {
            Debug.LogWarning("SceneChanger: loading photo not found (Resources/" + loadingImageResource + ")");
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // a black screen with the photo on it (the photo keeps its proportions, see LoadingPhotoFit)
        var back = new GameObject("LoadingBack", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(canvas.rootCanvas.transform, false);
        back.transform.SetAsLastSibling();
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;
        var backImage = back.GetComponent<Image>();
        backImage.color = Color.black;
        backImage.raycastTarget = true;

        var overlay = back.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 5000;
        back.AddComponent<GraphicRaycaster>();

        var go = new GameObject("LoadingImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(LoadingPhotoFit));
        go.transform.SetParent(back.transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;

        go.GetComponent<LoadingPhotoFit>().photoAspect = (float)texture.width / Mathf.Max(1, texture.height);

        BuildLoadingBar(back.transform);
    }

    // One of the pictures in Resources/<loadingScreensFolder>: a random one, never the same as last time
    private Texture PickLoadingScreen()
    {
        Texture[] all = loadingScreens;
        if (all == null || all.Length == 0)
            all = string.IsNullOrEmpty(loadingScreensFolder) ? new Texture[0] : Resources.LoadAll<Texture>(loadingScreensFolder);
        if (all.Length > 0)
        {
            const string Key = "LastLoadingScreen";
            int last = PlayerPrefs.GetInt(Key, -1);
            int index = Random.Range(0, all.Length);
            if (all.Length > 1 && index == last) index = (index + 1 + Random.Range(0, all.Length - 1)) % all.Length;
            PlayerPrefs.SetInt(Key, index);
            return all[index];
        }
        return loadingImage != null ? loadingImage : Resources.Load<Texture>(loadingImageResource);
    }

    // The bar: as wide as the screen, thin, red, at the very bottom (no margin). A big red percent at the right, and the line
    // "PRESS ENTER TO CONTINUE" at the left, above the bar.
    private void BuildLoadingBar(Transform parent)
    {
        bool phone = DeviceDetection.IsMobile();
        var red = new Color(0.88f, 0.1f, 0.16f, 1f);

        var track = UiKit.NewRect("LoadingBar", parent);
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.offsetMin = new Vector2(0f, 0f);
        track.offsetMax = new Vector2(0f, BarHeight);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.14f);
        trackImage.raycastTarget = false;

        barFill = UiKit.NewRect("Fill", track);
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        var fillImage = barFill.gameObject.AddComponent<Image>();
        fillImage.color = red;
        fillImage.raycastTarget = false;

        percentText = UiKit.Label(parent, "Percent", "0%", UiKit.DmSemi, 30f, red,
            TextAlignmentOptions.Right, Vector2.zero, new Vector2(400f, 50f));
        var percentRt = percentText.rectTransform;
        percentRt.anchorMin = percentRt.anchorMax = new Vector2(1f, 0f);
        percentRt.pivot = new Vector2(1f, 0f);
        percentRt.anchoredPosition = new Vector2(-40f, BarHeight + 12f);
        percentText.raycastTarget = false;
        percentText.gameObject.AddComponent<LocalizeIgnore>();

        promptText = UiKit.Label(parent, "PressEnter", phone ? "TAP TO ENTER" : "PRESS ENTER TO CONTINUE", UiKit.DmSemi, 20f,
            new Color(0.97f, 0.94f, 0.88f, 1f), TextAlignmentOptions.Left, Vector2.zero, new Vector2(800f, 40f));
        var rt = promptText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(40f, BarHeight + 16f);
        promptText.raycastTarget = false;
        promptText.characterSpacing = 3f;
        promptText.gameObject.SetActive(false);
    }

    private void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (barFill != null) barFill.anchorMax = new Vector2(value, 1f);
        if (percentText != null) percentText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    // Only Enter lets the game in (on a phone, which has no Enter key, a tap does)
    private static bool EnterPressed()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return true;

        if (DeviceDetection.IsMobile())
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        }
        return false;
    }

    private void PlayVideo(string fileName, bool loop)
    {
        if (videoPlayer == null)
            return;

        videoPlayer.Stop();

        // StreamingAssets URL
        string videoURL = Application.streamingAssetsPath + "/" + fileName;

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = videoURL;
        videoPlayer.isLooping = loop;
        videoPlayer.playOnAwake = false;

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.Prepare();

        Debug.Log("Loading video: " + videoURL);
    }

    private void OnVideoPrepared(VideoPlayer player)
    {
        player.prepareCompleted -= OnVideoPrepared;

        player.Play();

        Debug.Log("Video started playing.");
    }

    // Loads the game scene for real (in the background). When it is ready and the picture was shown long enough,
    // the bar stays at 100%, the line "PRESS ENTER TO CONTINUE" blinks, and the game starts when the player presses Enter (a tap on a phone).
    private IEnumerator LoadGame()
    {
        var operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float shown = 0f;
        while (operation.progress < 0.9f || shown < minShowSeconds)
        {
            shown += Time.unscaledDeltaTime;
            SetProgress(Mathf.Min(operation.progress / 0.9f, shown / Mathf.Max(0.1f, minShowSeconds)));
            yield return null;
        }
        SetProgress(1f);

        if (waitForKey && !skipKeyWait && promptText != null)
        {
            if (promptText != null) promptText.gameObject.SetActive(true);
            yield return null;

            while (!EnterPressed())
            {
                if (promptText != null)
                {
                    float blink = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);
                    promptText.color = new Color(0.97f, 0.94f, 0.88f, blink);
                }
                yield return null;
            }
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
    }
}
