using System.Collections.Generic;
using UnityEngine;

// The sounds of the player, loaded by name from Assets/Resources/PlayerSfx (nothing to set up).
//   Voice (a man's or a woman's, from the Male / Female of the agent account; a player who is not signed in is a man):
//     Hurt1 / Hurt2 / Hurt3  when the health goes down to 80% / 60% / under 40%
//     Dead                   game over
//     Revive                 the injector heals
//   Effects: BagOpen, CageRestock, TorchToggle, GunTakeOut, ScannerTakeOut, InjectorTakeOut, InjectorUse
// The files are named  MaleHurt1.mp3, FemaleHurt1.mp3 ...  to add a sound put a file in that folder and call PlayerSfx.Play("Name").
public static class PlayerSfx
{
    const string Folder = "PlayerSfx/";
    const string GenderKey = "PlayerGender";

    // the health (a share of the maximum) at which each hurt voice is used
    public const float Hurt1At = 0.8f, Hurt2At = 0.6f, Hurt3At = 0.4f;

    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static AudioSource effects, voice;
    static float nextHurt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        cache.Clear();
        effects = voice = null;
        nextHurt = 0f;
    }

    // "male" or "female": the choice of the agent account (kept on the device, so it is known without a sign in too)
    public static string Gender
    {
        get
        {
            var agent = AgentAccount.Agent;
            if (agent != null && !string.IsNullOrEmpty(agent.gender))
            {
                if (PlayerPrefs.GetString(GenderKey, "") != agent.gender) PlayerPrefs.SetString(GenderKey, agent.gender);
                return agent.gender;
            }
            return PlayerPrefs.GetString(GenderKey, "male");
        }
    }

    static AudioSource Source(ref AudioSource source, string name)
    {
        if (source != null) return source;

        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;                 // a plain 2D sound, in the ears of the player
        source.ignoreListenerPause = true;        // the game over screen waits for the game (Time.timeScale 0), the voice must still be heard
        return source;
    }

    static AudioClip Load(string name)
    {
        if (cache.TryGetValue(name, out AudioClip clip) && clip != null) return clip;

        clip = Resources.Load<AudioClip>(Folder + name);
        if (clip == null) Debug.LogWarning("[PlayerSfx] No sound called " + Folder + name);
        cache[name] = clip;
        return clip;
    }

    // An effect (the bag, the torch, the things taken out)
    public static void Play(string name, float volume = 1f)
    {
        var clip = Load(name);
        if (clip == null) return;
        Source(ref effects, "Player Sfx").PlayOneShot(clip, volume);
    }

    // A voice line of the player: Hurt1, Hurt2, Hurt3, Dead, Revive. A new one cuts the one that is playing
    public static void Voice(string line)
    {
        var clip = Load((Gender == "female" ? "Female" : "Male") + line);
        if (clip == null) return;

        var source = Source(ref voice, "Player Voice");
        source.Stop();
        source.clip = clip;
        source.volume = 1f;
        source.Play();
    }

    // The player was hit: the voice depends on how much health is left. A hit above 80% is silent, and the voice is not repeated while it plays
    public static void Hurt(int health, int maxHealth)
    {
        if (maxHealth <= 0) return;

        float share = health / (float)maxHealth;
        string line = share < Hurt3At ? "Hurt3" : share <= Hurt2At ? "Hurt2" : share <= Hurt1At ? "Hurt1" : null;
        if (line == null) return;

        if (Time.unscaledTime < nextHurt || (voice != null && voice.isPlaying)) return;
        nextHurt = Time.unscaledTime + 1.5f;
        Voice(line);
    }
}
