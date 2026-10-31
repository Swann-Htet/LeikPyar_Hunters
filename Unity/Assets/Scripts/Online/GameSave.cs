using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// SAVE GAME, with a photo. One saved game for each agent, kept on the server (so it follows the agent to any phone or computer):
//   - SAVE (the button / key V in the game): what the agent has (the clock and the day, the points and the upgrades, the backpack, the
//     injectors, the health, where he stands, the souls / cages / nights of this run) goes to the server together with a photo of the screen.
//   - CONTINUE (the card in the intro menu): shows the photo and the day, and loads the game from the save.
// The ghost and the orbs are not saved: they start fresh (the clock decides the night).
// The cages are: the ones in the backpack, the ones the NPC is checking on the table (they go back on the table, with the time they still need),
// the empty ones waiting on the table and the ones dropped on the floor (they go back in the backpack).
public static class GameSave
{
    [Serializable] public class UpgradeEntry { public string id; public float percent; }
    [Serializable] public class ItemEntry { public string id; public int count; }
    [Serializable] public class CheckEntry { public string id; public float remaining; public float scale = 1f; }

    [Serializable]
    public class State
    {
        public int version = 1;
        public int day;
        public float hour;
        public int captures, cages, nights;
        public int points, totalPoints;
        public UpgradeEntry[] upgrades;
        public ItemEntry[] items;
        public CheckEntry[] checks;          // the cages the NPC was checking
        public int injectors;
        public int health;
        public float x, y, z, yaw;
    }

    // What the server tells about the save (without the state itself, unless it is asked for)
    [Serializable]
    public class Info
    {
        public int day;
        public float hour;
        public int score;
        public long saved;          // unix seconds. 0 = there is no saved game
        public bool photo;
        public string state;
        public bool Exists { get { return saved > 0; } }
    }

    [Serializable] class InfoAnswer { public bool ok; public string error; public Info save; }

    // The text a screen shows for a moment ("Game saved")
    public static string ToastText;
    public static float ToastUntil;

    public static void Say(string text)
    {
        ToastText = text;
        ToastUntil = Time.unscaledTime + 3.5f;
    }

    // ---------------------------------------------------------------- what is saved

    public static State Collect()
    {
        var s = new State();

        var clock = UnityEngine.Object.FindFirstObjectByType<WallClockController>();
        s.hour = clock != null ? clock.GameHour24 : 20f;
        s.day = AgentStats.Day;
        s.captures = AgentStats.Captures;
        s.cages = AgentStats.Cages;
        s.nights = AgentStats.Nights;

        var upgrades = UpgradeSystem.Instance;
        if (upgrades != null)
        {
            s.points = upgrades.Points;
            s.totalPoints = upgrades.TotalPoints;

            var list = new List<UpgradeEntry>();
            foreach (var u in upgrades.upgrades)
                if (u != null) list.Add(new UpgradeEntry { id = u.id, percent = u.percent });
            s.upgrades = list.ToArray();
        }

        var inventory = Inventory.Instance;
        if (inventory != null)
        {
            // what is on the Drop Table and on the floor belongs to the player: nothing may be lost when the game is saved
            var extra = new Dictionary<string, int>();
            var pending = new List<CheckEntry>();
            foreach (var table in DropTable.All)
            {
                if (table != null) table.ExportForSave(pending, extra);
            }
            foreach (var cage in PlacedCage.All)
            {
                if (cage == null || cage.source == null || cage.onTable) continue;      // (the ones on the table are counted by the table)
                bool inCheck = false;
                foreach (var table in DropTable.All)
                    if (table != null && table.OwnsCage(cage.gameObject)) inCheck = true;
                if (inCheck) continue;

                string id = cage.source.Id;
                extra[id] = (extra.TryGetValue(id, out int n) ? n : 0) + 1;
            }
            s.checks = pending.ToArray();

            var seen = new HashSet<string>();
            var items = new List<ItemEntry>();
            foreach (var data in Resources.FindObjectsOfTypeAll<InvItemData>())
            {
                if (data == null || !seen.Add(data.Id)) continue;
                extra.TryGetValue(data.Id, out int more);
                int count = inventory.GetCount(data.Id) + more;
                if (count > 0) items.Add(new ItemEntry { id = data.Id, count = count });
            }
            s.items = items.ToArray();
        }

        var hotbar = Hotbar.Instance;
        s.injectors = hotbar != null ? hotbar.injectors : 0;

        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        s.health = health != null ? health.currentHealth : 100;

        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            s.x = player.transform.position.x;
            s.y = player.transform.position.y;
            s.z = player.transform.position.z;
            s.yaw = player.transform.eulerAngles.y;
        }
        return s;
    }

    // ---------------------------------------------------------------- loading it back (in the game scene)

    public static string PendingJson;

    public static void Apply(State s)
    {
        if (s == null) return;

        var clock = UnityEngine.Object.FindFirstObjectByType<WallClockController>();
        if (clock != null)
        {
            int hour = Mathf.Clamp(Mathf.FloorToInt(s.hour), 0, 23);
            int minute = Mathf.Clamp(Mathf.FloorToInt((s.hour - Mathf.Floor(s.hour)) * 60f), 0, 59);
            clock.SetGameTime(hour, minute);
        }

        AgentStats.Restore(s.captures, s.cages, s.nights);

        var upgrades = UpgradeSystem.Instance;
        if (upgrades != null && s.upgrades != null)
        {
            var ids = new string[s.upgrades.Length];
            var percents = new float[s.upgrades.Length];
            for (int i = 0; i < s.upgrades.Length; i++) { ids[i] = s.upgrades[i].id; percents[i] = s.upgrades[i].percent; }
            upgrades.Restore(ids, percents, s.points, s.totalPoints);
        }

        // the backpack: the saved amount of every item (more are added, too many are taken away)
        var inventory = Inventory.Instance;
        if (inventory != null && s.items != null)
        {
            var wanted = new Dictionary<string, int>();
            foreach (var item in s.items) wanted[item.id] = item.count;

            var seen = new HashSet<string>();
            foreach (var data in Resources.FindObjectsOfTypeAll<InvItemData>())
            {
                if (data == null || !seen.Add(data.Id)) continue;
                if (data.Id == LetterItem.Id) continue;          // the letter is always in the backpack (see LetterItem)
                int now = inventory.GetCount(data.Id);
                wanted.TryGetValue(data.Id, out int want);
                if (want > now) inventory.AddData(data, want - now);
                else if (want < now) inventory.Remove(data.Id, now - want);
            }
        }

        // the cages the NPC was checking go back on the table (or into the backpack if the table is closed or full): none is lost
        if (s.checks != null && s.checks.Length > 0)
        {
            foreach (var entry in s.checks)
            {
                InvItemData data = null;
                foreach (var candidate in Resources.FindObjectsOfTypeAll<InvItemData>())
                    if (candidate != null && candidate.Id == entry.id) { data = candidate; break; }
                if (data == null) continue;

                bool onTable = false;
                foreach (var table in DropTable.All)
                {
                    if (table != null && table.RestoreCheck(data, entry.scale > 0f ? entry.scale : 1f, entry.remaining)) { onTable = true; break; }
                }
                if (!onTable && inventory != null) inventory.AddData(data, 1);
            }
        }

        var hotbar = Hotbar.Instance;
        if (hotbar != null) hotbar.injectors = Mathf.Clamp(s.injectors, 0, hotbar.maxInjectors);

        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            health.currentHealth = Mathf.Clamp(s.health, 1, health.maxHealth);
            if (health.healthSlider != null) health.healthSlider.value = health.currentHealth;
        }

        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player != null && (s.x != 0f || s.y != 0f || s.z != 0f))
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(s.x, s.y + 0.05f, s.z), Quaternion.Euler(0f, s.yaw, 0f));
            if (controller != null) controller.enabled = true;
        }

        Debug.Log("[Save] The saved game is loaded: day " + s.day + ", hour " + s.hour.ToString("0.0"));
        Say("Saved game loaded");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PendingJson != null && scene.name == TutorialMode.GameSceneName) ApiClient.Run(ApplyWhenReady());
    }

    // The scene sets itself up in its first frames (the clock starts at 8 PM, the backpack gets its start items): the save is put on after that
    static IEnumerator ApplyWhenReady()
    {
        string json = PendingJson;
        PendingJson = null;

        float until = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < until)
        {
            if (UnityEngine.Object.FindFirstObjectByType<PlayerController>() != null && UpgradeSystem.Instance != null &&
                Inventory.Instance != null && Hotbar.Instance != null && UnityEngine.Object.FindFirstObjectByType<WallClockController>() != null)
                break;
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);

        State state = null;
        try { state = JsonUtility.FromJson<State>(json); }
        catch (Exception e) { Debug.LogWarning("[Save] The saved game could not be read: " + e.Message); }
        Apply(state);
    }

    // ---------------------------------------------------------------- the server

    public static void Upload(Texture2D photo, Action<bool, string> done)
    {
        var state = Collect();
        string stateJson = JsonUtility.ToJson(state);
        string picture = photo != null ? Convert.ToBase64String(photo.EncodeToJPG(72)) : "";

        string body = "{\"state\":" + ApiClient.Quote(stateJson) +
                      ",\"photo\":\"" + picture + "\"" +
                      ",\"day\":" + state.day +
                      ",\"hour\":" + state.hour.ToString("0.##", CultureInfo.InvariantCulture) +
                      ",\"score\":" + AgentStats.Score + "}";

        ApiClient.Post("/api/save-game", body, r => { if (done != null) done(r.ok, r.error); });
    }

    // Is there a saved game? (the photo is asked for separately)
    public static void FetchInfo(Action<Info> done)
    {
        ApiClient.Get("/api/save-game", r =>
        {
            Info info = null;
            if (r.ok)
            {
                try { var answer = JsonUtility.FromJson<InfoAnswer>(r.text); if (answer != null) info = answer.save; }
                catch (Exception) { }
            }
            if (done != null) done(info);
        });
    }

    public static void FetchPhoto(Action<Texture2D> done)
    {
        ApiClient.Run(PhotoRoutine(done));
    }

    static IEnumerator PhotoRoutine(Action<Texture2D> done)
    {
        using (var request = UnityWebRequestTexture.GetTexture(ApiClient.BaseUrl + "/api/save-game/photo"))
        {
            string token = ApiClient.Token;
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 20;

            UnityWebRequestAsyncOperation operation = null;
            try { operation = request.SendWebRequest(); }
            catch (InvalidOperationException) { }
            if (operation == null) { if (done != null) done(null); yield break; }

            yield return operation;

            Texture2D texture = null;
            if (request.result == UnityWebRequest.Result.Success)
                texture = DownloadHandlerTexture.GetContent(request);
            if (done != null) done(texture);
        }
    }

    // CONTINUE: asks the server for the saved state and opens the game with it
    public static void StartLoad(Action<string> failed)
    {
        ApiClient.Get("/api/save-game?full=1", r =>
        {
            Info info = null;
            if (r.ok)
            {
                try { var answer = JsonUtility.FromJson<InfoAnswer>(r.text); if (answer != null) info = answer.save; }
                catch (Exception) { }
            }

            if (info == null || !info.Exists || string.IsNullOrEmpty(info.state))
            {
                if (failed != null) failed(r.ok ? "No saved game" : (r.error ?? "No connection to the server"));
                return;
            }

            PendingJson = info.state;
            Time.timeScale = 1f;
            SceneManager.LoadScene(TutorialMode.GameSceneName);
        });
    }

    // "Day 3  ·  10:20 PM"
    public static string ClockText(float hour)
    {
        int h24 = Mathf.Clamp(Mathf.FloorToInt(hour), 0, 23);
        int minute = Mathf.Clamp(Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f), 0, 59);
        string period = h24 >= 12 ? "PM" : "AM";
        int h12 = h24 % 12;
        if (h12 == 0) h12 = 12;
        return h12.ToString("00") + ":" + minute.ToString("00") + " " + period;
    }
}
