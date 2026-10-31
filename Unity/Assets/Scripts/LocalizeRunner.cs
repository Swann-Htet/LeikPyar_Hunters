using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Swaps the text of every TMP text to Burmese (and back) while the game runs. Nothing to set up.
public class LocalizeRunner : MonoBehaviour
{
    private class State
    {
        public bool ignore;
        public string shown;          // the Burmese text we put there (null = the text is the script's own)
        public string source;         // the English text before we changed it
        public bool fontApplied;
        public TMP_FontAsset font;
        public Material material;
        public float fontSize;
        public float fontSizeMin;
        public float fontSizeMax;
        public float characterSpacing;
        public float wordSpacing;
        public float appliedScale;
    }

    // Burmese letters are joined into whole syllables, so the same font size looks much bigger than English, and the wide
    // letter spacing of the horror style (made for English capitals) tears the syllables apart. Burmese text is therefore a
    // bit smaller and has no extra spacing. The player can change the size on top of this (Loc.TextSize, Settings > Text Size and the
    // A- / A+ buttons of the intro menu); the intro menu is a little smaller still, its titles were too big.
    private const float BurmeseSizeScale = 0.5f;        // for big texts (titles, 70 and more)
    private const float BurmeseSmallScale = 0.88f;       // for small texts (24 and less): they stay readable
    private const float IntroSizeScale = 0.85f;

    private static LocalizeRunner instance;

    private readonly Dictionary<TMP_Text, State> states = new Dictionary<TMP_Text, State>();
    private TMP_Text[] texts = new TMP_Text[0];
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (instance != null) return;

        var go = new GameObject("LocalizeRunner");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LocalizeRunner>();
    }

    void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);

            // forget texts that no longer exist
            if (states.Count > texts.Length * 2 + 32)
            {
                var dead = new List<TMP_Text>();
                foreach (var pair in states)
                    if (pair.Key == null) dead.Add(pair.Key);
                foreach (var key in dead) states.Remove(key);
            }
        }

        bool burmese = Loc.IsBurmese;
        TMP_FontAsset font = burmese ? Loc.BurmeseFont : null;

        float scale = BurmeseSizeScale * Loc.TextSize;
        if (SceneManager.GetActiveScene().name == "IntroScene") scale *= IntroSizeScale;

        foreach (var t in texts)
        {
            if (t == null) continue;
            Process(t, burmese, font, scale);
        }
    }

    void Process(TMP_Text t, bool burmese, TMP_FontAsset burmeseFont, float scale)
    {
        if (!states.TryGetValue(t, out State st))
        {
            st = new State { ignore = t.GetComponentInParent<LocalizeIgnore>(true) != null };
            states[t] = st;
        }
        if (st.ignore) return;

        // English: give back everything we changed
        if (!burmese || burmeseFont == null)
        {
            if (st.shown != null || st.fontApplied) Restore(t, st);
            return;
        }

        // the size was changed (Text Size setting, intro menu)
        if (st.fontApplied && !Mathf.Approximately(st.appliedScale, scale)) ApplySize(t, st, scale);

        string text = t.text;
        if (st.shown != null && text == st.shown) return;   // still our Burmese text

        // A new text from a script
        string translated = Loc.Translate(text);
        if (string.IsNullOrEmpty(text) || translated == text)
        {
            if (st.fontApplied) RestoreFont(t, st);
            st.shown = null;
            return;
        }

        if (!st.fontApplied)
        {
            st.font = t.font;
            st.material = t.fontSharedMaterial;
            st.fontSize = t.fontSize;
            st.fontSizeMin = t.fontSizeMin;
            st.fontSizeMax = t.fontSizeMax;
            st.characterSpacing = t.characterSpacing;
            st.wordSpacing = t.wordSpacing;

            t.font = burmeseFont;
            t.characterSpacing = 0f;
            t.wordSpacing = Mathf.Min(0f, st.wordSpacing);
            ApplySize(t, st, scale);
            st.fontApplied = true;
        }

        st.source = text;
        st.shown = translated;
        t.text = translated;
    }

    // 'scale' = the base Burmese scale times the player's Text Size. Big texts shrink the most, small texts hardly at all.
    static float SizeFor(float original, float scale)
    {
        float t = Mathf.InverseLerp(24f, 70f, original);
        float baseScale = Mathf.Lerp(BurmeseSmallScale, BurmeseSizeScale, t);
        return baseScale * (scale / BurmeseSizeScale);
    }

    void ApplySize(TMP_Text t, State st, float scale)
    {
        if (t.enableAutoSizing)
        {
            t.fontSizeMin = st.fontSizeMin * SizeFor(st.fontSizeMin, scale);
            t.fontSizeMax = st.fontSizeMax * SizeFor(st.fontSizeMax, scale);
        }
        else
        {
            t.fontSize = st.fontSize * SizeFor(st.fontSize, scale);
        }
        st.appliedScale = scale;
    }

    void Restore(TMP_Text t, State st)
    {
        // Only put the English text back if a script has not already replaced it
        if (st.shown != null && t.text == st.shown) t.text = st.source;
        st.shown = null;
        RestoreFont(t, st);
    }

    void RestoreFont(TMP_Text t, State st)
    {
        if (!st.fontApplied) return;

        if (st.font != null) t.font = st.font;
        if (st.material != null) t.fontSharedMaterial = st.material;

        t.characterSpacing = st.characterSpacing;
        t.wordSpacing = st.wordSpacing;
        if (t.enableAutoSizing)
        {
            t.fontSizeMin = st.fontSizeMin;
            t.fontSizeMax = st.fontSizeMax;
        }
        else
        {
            t.fontSize = st.fontSize;
        }
        st.fontApplied = false;
    }
}
