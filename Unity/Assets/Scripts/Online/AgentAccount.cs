using System;
using UnityEngine;

// The agent account of the player (the same account as on the website). Agent ID, codename, best score.
//  - Sign in or create an agent: AgentAccount.Login / Register
//  - The session is kept on the device (a token) and also by the browser (a cookie), so a player who signed in on the website is
//    already signed in here.
public static class AgentAccount
{
    public static bool SignedIn { get { return Agent != null && Agent.Valid; } }
    public static ApiClient.AgentDto Agent { get; private set; }
    public static bool Checked { get; private set; }

    // Signed in, but Male / Female was never chosen (an agent made before the choice existed)
    public static bool NeedsGender { get { return SignedIn && string.IsNullOrEmpty(Agent.gender); } }

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Agent = null;
        Checked = false;
        Changed = null;
    }

    static void Set(ApiClient.AgentDto agent)
    {
        Agent = agent != null && agent.Valid ? agent : null;
        Checked = true;
        if (Changed != null) Changed();
    }

    // Asks the server who this player is (the token or the cookie)
    public static void Refresh(Action<bool> done = null)
    {
        ApiClient.Get("/api/me", r =>
        {
            if (r.ok)
            {
                try
                {
                    var answer = JsonUtility.FromJson<ApiClient.AuthAnswer>(r.text);
                    Set(answer != null ? answer.agent : null);
                }
                catch (Exception) { Set(null); }
            }
            else
            {
                Checked = true;      // no server: the player plays without an account
                if (Changed != null) Changed();
            }
            if (done != null) done(SignedIn);
        });
    }

    public static void Login(string codename, string password, Action<bool, string> done)
    {
        Auth("/api/login", codename, password, null, done);
    }

    // gender: "male" or "female" (the server asks for it)
    public static void Register(string codename, string password, string gender, Action<bool, string> done)
    {
        Auth("/api/register", codename, password, gender, done);
    }

    // Male / Female for the signed in agent
    public static void SetGender(string gender, Action<bool, string> done)
    {
        ApiClient.Post("/api/gender", "{\"gender\":" + ApiClient.Quote(gender) + "}", r =>
        {
            ApiClient.AuthAnswer answer = null;
            if (r.ok) { try { answer = JsonUtility.FromJson<ApiClient.AuthAnswer>(r.text); } catch (Exception) { } }
            if (!r.ok || answer == null || !answer.ok || answer.agent == null || !answer.agent.Valid)
            {
                if (done != null) done(false, !r.ok ? r.error : "Something went wrong");
                return;
            }

            Set(answer.agent);
            if (done != null) done(true, null);
        });
    }

    static void Auth(string path, string codename, string password, string gender, Action<bool, string> done)
    {
        string body = "{\"codename\":" + ApiClient.Quote(codename) + ",\"password\":" + ApiClient.Quote(password) +
                      (string.IsNullOrEmpty(gender) ? "" : ",\"gender\":" + ApiClient.Quote(gender)) + "}";
        ApiClient.Post(path, body, r =>
        {
            if (!r.ok)
            {
                if (done != null) done(false, r.error);
                return;
            }

            ApiClient.AuthAnswer answer = null;
            try { answer = JsonUtility.FromJson<ApiClient.AuthAnswer>(r.text); } catch (Exception) { }
            if (answer == null || !answer.ok || answer.agent == null || !answer.agent.Valid)
            {
                if (done != null) done(false, answer != null && !string.IsNullOrEmpty(answer.error) ? answer.error : "Something went wrong");
                return;
            }

            ApiClient.Token = answer.token;
            Set(answer.agent);
            if (done != null) done(true, null);
        });
    }

    public static void Logout(Action done = null)
    {
        ApiClient.Post("/api/logout", "{}", r =>
        {
            ApiClient.Token = "";
            Set(null);
            if (done != null) done();
        });
    }

    // A better score came back from the server
    public static void Update(ApiClient.AgentDto agent)
    {
        if (agent != null && agent.Valid) Set(agent);
    }
}
