using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Ghost > Add Spot Lights To Lamps
// Gives every lamp of the open scene its light, and keeps it in the scene (the game also makes them by itself when it starts):
//   - ceiling tubes (material lightEmissive01_MAT, and the objects called "light", "light12"...): a spot light pointing down
//   - bulbs (material bulb_emissive_MAT, the SM_bulb objects): a yellow horror light shining down that flickers more
// A lamp that already has a LampSpot is skipped, so it is safe to run again.
// Each light has a LampSpot: it goes dark when the power is out and flickers a little.
public static class LampLightTool
{
    [MenuItem("Tools/Ghost/Add Spot Lights To Lamps")]
    public static void Run()
    {
        var handled = new System.Collections.Generic.HashSet<Renderer>();
        foreach (var existing in Object.FindObjectsByType<LampSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.glow != null) handled.Add(existing.glow);

        int tubes = 0, bulbs = 0, skipped = 0;

        // the light sticks ("light", "light12"...) get the glowing tube material, kept in the scene
        int glowing = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!r.gameObject.scene.IsValid() || !LampSpot.NeedsGlowMaterial(r)) continue;
            Undo.RecordObject(r, "Glowing tube");
            LampSpot.ApplyGlowMaterial(r);
            EditorUtility.SetDirty(r);
            glowing++;
        }

        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!r.gameObject.scene.IsValid() || !LampSpot.TryClassify(r, out LampSpot.Kind kind)) continue;

            if (handled.Contains(r)) { skipped++; continue; }

            var go = LampSpot.CreateFor(r, kind, r.transform);
            Undo.RegisterCreatedObjectUndo(go, "Lamp light");

            if (kind == LampSpot.Kind.Bulb) bulbs++; else tubes++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("LampLightTool: " + tubes + " tube spot light(s) and " + bulbs + " bulb light(s) added, " + skipped +
                  " lamp(s) already had one, " + glowing + " light stick(s) now glow. Lights are without shadows (the nearest tubes get them while playing) and far lights are switched off. " +
                  "Many lights cost speed (WebGL / phones): lower Reach or delete some if the game gets slow. Save the scene.");
    }
}
