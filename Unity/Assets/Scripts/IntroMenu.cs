using System.Collections;
using TMPro;
using UnityEngine;

// The buttons of the intro menu (hooked up by Tools > Ghost > Build Horror Intro Menu)
public class IntroMenu : MonoBehaviour
{
    public SceneChanger changer;
    public GameObject creditsPanel;
    [Tooltip("Small red message under the menu")]
    public TMP_Text toast;

    [Header("Texts")]
    public string noSaveText = "NO SAVED GAME YET";
    public string quitWebText = "CLOSE THIS TAB TO QUIT";

    private Coroutine toastRoutine;

    public void StartNewGame()
    {
        if (changer != null) changer.GoToSampleScene();
    }

    // The training (like a shooter's training ground): a quiet house with a list of tasks
    public void StartTutorial()
    {
        if (changer != null) changer.GoToTutorialScene();
    }

    // LOAD GAME shows LEADERBOARD now (there is no save system): the high scores of the agents
    public void LoadGame()
    {
        LeaderboardScreen.Open();
    }

    public void OpenCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        Toast(quitWebText);
#else
        Application.Quit();
#endif
    }

    void Update()
    {
        if (creditsPanel != null && creditsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseCredits();
    }

    void Toast(string text)
    {
        if (toast == null) return;

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(text));
    }

    IEnumerator ToastRoutine(string text)
    {
        toast.text = text;
        Color c = toast.color;
        c.a = 1f;
        toast.color = c;

        yield return new WaitForSecondsRealtime(2f);

        for (float f = 1f; f > 0f; f -= Time.unscaledDeltaTime / 0.6f)
        {
            c.a = f;
            toast.color = c;
            yield return null;
        }

        toast.text = "";
    }
}
