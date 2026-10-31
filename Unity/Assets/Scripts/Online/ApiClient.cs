using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Talks to the game server (accounts, scores, the leaderboard). The game and the website share one server and one account.
//  - In the browser (WebGL) the address of the page is used, so the game works on any address (the IP now, leikpyarhunters.org later).
//  - In the Editor and in other builds, "Default Base" is used (change it below, or set ApiClient.BaseOverride from code).
// The answer is JSON; the small classes below hold it (JsonUtility).
public static class ApiClient
{
    // The server used in the Editor. After the domain works: "https://leikpyarhunters.org"
    public const string DefaultBase = "https://leikpyarhunters.org";
    public static string BaseOverride;

    public static string BaseUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(BaseOverride)) return BaseOverride.TrimEnd('/');
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var uri = new Uri(Application.absoluteURL);
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch (Exception) { }
#endif
            return DefaultBase;
        }
    }

    // The place where the website of the game lives (for the links in the game)
    public static string SiteUrl => BaseUrl + "/";

    // ---------------------------------------------------------------- answers
    [Serializable]
    public class AgentDto
    {
        public string agentId;
        public string codename;
        public string gender;          // "male", "female", or empty (an old agent who has not chosen yet)
        public int bestScore;
        public int bestCaptures;
        public int bestCages;
        public int bestNights;
        public int runs;
        public int rank;
        public bool Valid => !string.IsNullOrEmpty(agentId);
    }

    [Serializable]
    public class AuthAnswer
    {
        public bool ok;
        public string error;
        public string token;
        public AgentDto agent;
    }

    [Serializable]
    public class ConfigAnswer
    {
        public bool ok;
        public bool captcha;      // creating an agent needs the robot check (it is done on the website)
    }

    [Serializable]
    public class ScoreAnswer
    {
        public bool ok;
        public string error;
        public int score;
        public bool improved;
        public AgentDto agent;
    }

    [Serializable]
    public class EntryDto
    {
        public int rank;
        public string agentId;
        public string codename;
        public int score;
        public int captures;
        public int cages;
        public int nights;
        public bool Valid => !string.IsNullOrEmpty(agentId);
    }

    [Serializable]
    public class BoardAnswer
    {
        public bool ok;
        public string error;
        public List<EntryDto> entries;
        public EntryDto me;
        public int total;
    }

    public class Result
    {
        public bool ok;          // the server answered with ok = true
        public long code;
        public string text;
        public string error;     // a message for the player
    }

    // ---------------------------------------------------------------- the runner (survives scene changes)
    class Runner : MonoBehaviour { }
    static Runner runner;

    public static void Run(IEnumerator routine)
    {
        if (runner == null)
        {
            var go = new GameObject("ApiClient");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        runner.StartCoroutine(routine);
    }

    // ---------------------------------------------------------------- the session
    const string TokenKey = "AgentToken";
    public static string Token
    {
        get { return PlayerPrefs.GetString(TokenKey, ""); }
        set { PlayerPrefs.SetString(TokenKey, value ?? ""); PlayerPrefs.Save(); }
    }

    // ---------------------------------------------------------------- requests
    public static void Get(string path, Action<Result> done)
    {
        Run(Send("GET", path, null, done));
    }

    public static void Post(string path, string json, Action<Result> done)
    {
        Run(Send("POST", path, json ?? "{}", done));
    }

    static IEnumerator Send(string method, string path, string json, Action<Result> done)
    {
        var result = new Result();
        using (var request = new UnityWebRequest(BaseUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            string token = Token;
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 15;

            // Unity refuses a plain http address (Player Settings > Other Settings > Allow downloads over HTTP) by throwing at once
            UnityWebRequestAsyncOperation operation = null;
            try { operation = request.SendWebRequest(); }
            catch (InvalidOperationException e)
            {
                Debug.LogWarning("[Api] " + method + " " + BaseUrl + path + " refused: " + e.Message +
                    "  (Player Settings > Other Settings > Allow downloads over HTTP = Always allowed, or use an https address)");
            }

            if (operation == null)
            {
                result.ok = false;
                result.error = "Insecure connection not allowed: see Player Settings";
                if (done != null) done(result);
                yield break;
            }

            yield return operation;

            result.code = request.responseCode;
            result.text = request.downloadHandler != null ? request.downloadHandler.text : "";

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                result.ok = false;
                result.error = request.error != null && request.error.Contains("nsecure")
                    ? "Insecure connection not allowed: see Player Settings"
                    : "No connection to the server";
                Debug.LogWarning("[Api] " + method + " " + BaseUrl + path + " failed: " + request.error +
                    (request.error != null && request.error.Contains("nsecure") ? "  (Player Settings > Other Settings > Allow downloads over HTTP)" : ""));
            }
            else
            {
                // the server answers {"ok":true,...} or {"ok":false,"error":"..."} (also with an error code)
                result.ok = request.responseCode >= 200 && request.responseCode < 300;
                if (!result.ok)
                {
                    try
                    {
                        var e = JsonUtility.FromJson<AuthAnswer>(result.text);
                        result.error = e != null && !string.IsNullOrEmpty(e.error) ? e.error : "Something went wrong";
                    }
                    catch (Exception) { result.error = "Something went wrong"; }
                }
            }
        }

        if (done != null) done(result);
    }

    public static string Quote(string s)
    {
        if (s == null) return "\"\"";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.Append(' ');
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
