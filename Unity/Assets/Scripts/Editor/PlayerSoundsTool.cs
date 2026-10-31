using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Ghost > Setup Player Sounds
// A small window for the sounds of the player (Assets/Resources/PlayerSfx, played by PlayerSfx.cs):
//   - it checks that every sound is there and can be loaded (green = ok, red = missing),
//   - FIX IMPORT SETTINGS puts the same good settings on all of them and imports them again (use it if Unity shows an import error),
//   - in Play mode every button plays its sound, as a man or as a woman, so you can listen without playing the game.
// Nothing has to be put in a scene: the game loads the sounds by name.
public class PlayerSoundsTool : EditorWindow
{
    const string Folder = "Assets/Resources/PlayerSfx";

    static readonly string[] Voices = { "Hurt1", "Hurt2", "Hurt3", "Dead", "Revive" };
    static readonly string[] VoiceNotes = { "health 80%", "health 60%", "health under 40%", "game over", "injector heals" };
    static readonly string[] Effects = { "BagOpen", "zip", "CageRestock", "TorchToggle", "GunTakeOut", "ScannerTakeOut", "ScannerPower", "InjectorTakeOut", "InjectorUse" };
    static readonly string[] EffectNotes = { "backpack opens", "backpack closes (zip)", "cage goes into the bag", "torch on / off", "gun out and away", "radar out and away", "scanner on / off (O)", "injector out", "injector used" };

    private Vector2 scroll;
    private int gender;

    [MenuItem("Tools/Ghost/Setup Player Sounds")]
    static void Open()
    {
        var window = GetWindow<PlayerSoundsTool>("Player Sounds");
        window.minSize = new Vector2(430f, 520f);
        window.gender = PlayerPrefs.GetString("PlayerGender", "male") == "female" ? 1 : 0;
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Player sounds", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("The sounds are in " + Folder + " and the game loads them by name. In the game the voice is the Male / Female of the agent account (a player who is not signed in is a man).", MessageType.Info);

        if (GUILayout.Button("FIX IMPORT SETTINGS (all sounds)", GUILayout.Height(30f))) FixImport();

        EditorGUILayout.Space(6f);
        bool play = Application.isPlaying;
        if (!play) EditorGUILayout.HelpBox("Press Play to listen: the test buttons work in Play mode.", MessageType.None);

        int picked = GUILayout.Toolbar(gender, new[] { "Male voice", "Female voice" });
        if (picked != gender)
        {
            gender = picked;
            PlayerPrefs.SetString("PlayerGender", gender == 1 ? "female" : "male");     // the test uses this (a signed in agent keeps his own choice)
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Voice", EditorStyles.boldLabel);
        for (int i = 0; i < Voices.Length; i++)
        {
            string line = Voices[i];
            string file = (gender == 1 ? "Female" : "Male") + line;
            Row(file, line + "  (" + VoiceNotes[i] + ")", () => PlayerSfx.Voice(line), play);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        for (int i = 0; i < Effects.Length; i++)
        {
            string file = Effects[i];
            Row(file, file + "  (" + EffectNotes[i] + ")", () => PlayerSfx.Play(file), play);
        }

        EditorGUILayout.EndScrollView();
    }

    // one line: a coloured mark (the file loads or not), its name and a play button
    static void Row(string file, string label, System.Action playIt, bool play)
    {
        var clip = Resources.Load<AudioClip>("PlayerSfx/" + file);
        EditorGUILayout.BeginHorizontal();

        var old = GUI.color;
        GUI.color = clip != null ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.45f, 0.45f);
        GUILayout.Label(clip != null ? "OK" : "MISSING", GUILayout.Width(62f));
        GUI.color = old;

        GUILayout.Label(label + (clip != null ? "   " + clip.length.ToString("0.0") + " s" : ""));

        GUI.enabled = play && clip != null;
        if (GUILayout.Button("Play", GUILayout.Width(60f))) playIt();
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // The same settings on every sound of the folder, then they are imported again
    static void FixImport()
    {
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Folder });
        int count = 0;
        var names = new List<string>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;       // a short sound: no wait when it plays
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;

            importer.SaveAndReimport();
            count++;
            names.Add(System.IO.Path.GetFileName(path));
        }

        AssetDatabase.Refresh();
        Debug.Log("[Player Sounds] " + count + " sounds imported again: " + string.Join(", ", names));
        EditorUtility.DisplayDialog("Player Sounds", count + " sounds were imported again with the same settings.\nIf Unity still shows an import error for one of them, tell me its name.", "OK");
    }
}
