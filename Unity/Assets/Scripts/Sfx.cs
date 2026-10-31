using UnityEngine;

// Small helpers for the gun and generator sounds.
// Every sound slot can be left empty: the default clip in Assets/Resources/Sfx is used
// (made by Tools/sfx/make_sfx.py). Put your own clip in the slot, or replace the file, to change a sound.
public static class Sfx
{
    // The clip you gave, or the default one from Resources/Sfx
    public static AudioClip Clip(AudioClip assigned, string defaultName)
    {
        return assigned != null ? assigned : Resources.Load<AudioClip>("Sfx/" + defaultName);
    }

    // A new AudioSource on the object. spatial = louder when you are close (use it for the machine)
    public static AudioSource AddSource(GameObject go, bool spatial, bool loop, float volume)
    {
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = spatial ? 1f : 0f;
        if (spatial)
        {
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 25f;
        }
        return source;
    }

    // A sound from Resources/Sfx played once at a place in the world (the box dropping on the floor or the table)
    public static void PlayAt(string name, Vector3 position, float volume)
    {
        var clip = Resources.Load<AudioClip>("Sfx/" + name);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // Starts a clip on a source (does nothing if there is no clip)
    public static void Play(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;

        source.clip = clip;
        source.Play();
    }

    // Plays a clip once, after a pause (exact timing, no gap)
    public static void PlayAfter(AudioSource source, AudioClip clip, float seconds)
    {
        if (source == null || clip == null) return;

        source.clip = clip;
        if (seconds <= 0f) source.Play();
        else source.PlayScheduled(AudioSettings.dspTime + seconds);
    }

    // Loops a clip, starting after a pause (e.g. when the Start sound ends)
    public static void PlayLoopAfter(AudioSource source, AudioClip clip, float seconds)
    {
        PlayAfter(source, clip, seconds);
    }

    public static void Stop(AudioSource source)
    {
        if (source != null) source.Stop();
    }
}
