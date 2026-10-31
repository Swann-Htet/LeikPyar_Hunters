using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// What the agent did in this run of the game: souls sealed, cages checked, nights survived. The score is made from them:
//   100 per soul sealed, 500 per cage checked, 250 per night survived.
// The score is sent to the server (the leaderboard) when the player is signed in: at every dawn, when the player dies, and every
// minute while something new happened. The server counts the score itself from these three numbers: the game's own number is never trusted.
// The training (TutorialScene) counts nothing.
public static class AgentStats
{
    public const int ScoreCapture = 100, ScoreCage = 500, ScoreNight = 250;

    public static int Captures { get; private set; }
    public static int Cages { get; private set; }
    public static int Nights { get; private set; }
    // The day of this run: Day 1 until the first dawn, then 2, 3... (the ghost gets harder every day)
    public static int Day { get { return Nights + 1; } }
    public static int Score { get { return Captures * ScoreCapture + Cages * ScoreCage + Nights * ScoreNight; } }

    public static event Action Changed;

    private static bool dirty;
    private static float lastSent = -100f;
    private static bool sending;
    private static string lastMessage;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Captures = Cages = Nights = 0;
        dirty = false;
        sending = false;
        lastSent = -100f;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        ApiClient.Run(Tick());
        ApiClient.Run(Toast());
    }

    // A new run: the numbers start again when the game scene opens
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SampleScene")
        {
            Captures = Cages = Nights = 0;
            dirty = false;
            if (Changed != null) Changed();
        }
        if (!AgentAccount.Checked) AgentAccount.Refresh();
    }

    static bool Counting
    {
        get { return !TutorialMode.Active && SceneManager.GetActiveScene().name != TutorialMode.SceneName; }
    }

    public static void AddCapture() { if (!Counting) return; Captures++; Touched(); }
    public static void AddCage(int count) { if (!Counting) return; Cages += Mathf.Max(0, count); Touched(); }

    // A saved game was loaded: the run goes on with the numbers it had
    public static void Restore(int captures, int cages, int nights)
    {
        Captures = Mathf.Max(0, captures);
        Cages = Mathf.Max(0, cages);
        Nights = Mathf.Max(0, nights);
        dirty = false;
        if (Changed != null) Changed();
    }

    // The night is over (dawn) and the agent is still alive: the score is sent at once
    public static void NightSurvived()
    {
        if (!Counting) return;
        Nights++;
        Touched();
        SubmitNow();
    }

    // The agent died: the score of this run is sent at once
    public static void OnDied()
    {
        if (!Counting) return;
        SubmitNow();
    }

    static void Touched()
    {
        dirty = true;
        if (Changed != null) Changed();
    }

    public static void SubmitNow()
    {
        if (!AgentAccount.SignedIn || sending || Score <= 0) return;
        sending = true;
        lastSent = Time.realtimeSinceStartup;
        dirty = false;

        string body = "{\"captures\":" + Captures + ",\"cages\":" + Cages + ",\"nights\":" + Nights + "}";
        ApiClient.Post("/api/score", body, r =>
        {
            sending = false;
            if (!r.ok) { dirty = true; return; }   // it is tried again later

            try
            {
                var answer = JsonUtility.FromJson<ApiClient.ScoreAnswer>(r.text);
                if (answer != null && answer.ok)
                {
                    AgentAccount.Update(answer.agent);
                    if (answer.improved)
                        lastMessage = "New best score " + answer.score.ToString("N0") + (answer.agent != null && answer.agent.rank > 0 ? "   Rank " + answer.agent.rank : "");
                }
            }
            catch (Exception) { }
        });
    }

    // Every half minute: a score with something new is sent if the last one was a minute ago
    static System.Collections.IEnumerator Tick()
    {
        var wait = new WaitForSecondsRealtime(30f);
        for (;;)
        {
            yield return wait;
            if (dirty && Time.realtimeSinceStartup - lastSent > 60f) SubmitNow();
        }
    }

    // "New best score ..." shows for a few seconds as the game's prompt line
    static System.Collections.IEnumerator Toast()
    {
        string shown = null;
        float until = 0f;
        for (;;)
        {
            yield return null;
            if (lastMessage != null)
            {
                shown = lastMessage;
                lastMessage = null;
                until = Time.unscaledTime + 4f;
            }
            if (shown != null && Time.unscaledTime < until) InteractPrompt.Request(shown);
        }
    }
}
