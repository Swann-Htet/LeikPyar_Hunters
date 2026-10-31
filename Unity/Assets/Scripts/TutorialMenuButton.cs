using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds the TUTORIAL button to the intro menu (between START NEW GAME and LOAD GAME) by itself when the intro scene starts.
// It copies the look of the LOAD GAME button, so it always matches the menu. If the menu was built with
// Tools > Ghost > Build Horror Intro Menu (which also makes this button), nothing is added twice.
public static class TutorialMenuButton
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Start();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Start();
    }

    static void Start()
    {
        var menu = Object.FindFirstObjectByType<IntroMenu>();
        if (menu == null) return;

        // a moment later: the other scripts have finished setting the menu up
        menu.StartCoroutine(Inject(menu));
    }

    static IEnumerator Inject(IntroMenu menu)
    {
        yield return null;
        yield return null;

        Transform load = null, credits = null, quit = null, toast = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            switch (t.name)
            {
                case "Tutorial": if (t.GetComponent<Button>() != null) yield break; break;   // already there
                case "LoadGame": load = t; break;
                case "Credits": credits = t; break;
                case "Quit": quit = t; break;
                case "Toast": toast = t; break;
            }
        }

        if (load == null || credits == null || !(load is RectTransform) || !(credits is RectTransform)) yield break;

        var loadRect = (RectTransform)load;
        var creditsRect = (RectTransform)credits;
        float spacing = loadRect.anchoredPosition.y - creditsRect.anchoredPosition.y;
        if (Mathf.Abs(spacing) < 1f) yield break;

        // the copy takes the place of LOAD GAME, the others move one step down
        var clone = Object.Instantiate(load.gameObject, load.parent);
        clone.name = "Tutorial";
        ((RectTransform)clone.transform).anchoredPosition = loadRect.anchoredPosition;

        Shift(loadRect, spacing);
        Shift(creditsRect, spacing);
        if (quit is RectTransform) Shift((RectTransform)quit, spacing);
        if (toast is RectTransform) Shift((RectTransform)toast, spacing);

        foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true))
            if (!string.IsNullOrEmpty(text.text)) text.text = "TUTORIAL";

        var button = clone.GetComponent<Button>();
        if (button != null)
        {
            // the old click (LOAD GAME) is switched off, the new one opens the training
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(menu.StartTutorial);
        }
    }

    static void Shift(RectTransform rt, float spacing)
    {
        rt.anchoredPosition += new Vector2(0f, -Mathf.Abs(spacing));
    }
}
