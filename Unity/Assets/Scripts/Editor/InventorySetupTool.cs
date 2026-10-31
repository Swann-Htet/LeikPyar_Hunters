using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

public static class InventorySetupTool
{
    [MenuItem("Tools/Ghost/Setup Inventory (10 slots)")]
    public static void Setup()
    {
        // 1) The "inv" tag
        AddTag(Inventory.TagName);

        // 2) The Inventory component on the player
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Inventory", "No player (PlayerController) found in the scene.", "OK");
            return;
        }

        // The player must never be an item (that made E "take" the player and destroyed the camera)
        foreach (var own in player.GetComponentsInChildren<InvItem>(true))
            Undo.DestroyObjectImmediate(own);
        if (player.gameObject.tag == Inventory.TagName)
        {
            Undo.RecordObject(player.gameObject, "Player tag");
            player.gameObject.tag = "Untagged";
        }

        var inventory = player.GetComponent<Inventory>();
        if (inventory == null) inventory = Undo.AddComponent<Inventory>(player.gameObject);

        Undo.RecordObject(inventory, "Setup inventory");
        inventory.slotCount = 10;

        // 3) The font (MetalMania-Regular SDF)
        string fontResult = "font 'MetalMania-Regular SDF' not found";
        var font = FindAsset<TMP_FontAsset>("MetalMania-Regular SDF");
        if (font != null)
        {
            inventory.font = font;
            fontResult = "font " + font.name;
        }

        // 4) The Faraday Cage items and what the player starts with
        string itemsResult = ConfigureFaradayItems(inventory);

        // 5) The backpack in the scene: YOUR design is kept. It is only built when there is none yet.
        string backpackResult;
        if (UseExistingBackpack(inventory))
        {
            backpackResult = "Your backpack design was kept (nothing was rebuilt).";
        }
        else
        {
            BuildNewBackpack(inventory);
            backpackResult = "A new backpack was built under 'InventoryCanvas'.";
        }

        EditorUtility.SetDirty(inventory);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = inventory.gameObject;

        Debug.Log("InventorySetupTool: tag 'inv' ready, Inventory is on '" + player.name + "'. " + fontResult + ". " + itemsResult + " " + backpackResult +
                  " (the backpack is hidden until you press B in Play mode).");
    }

    // Uses the backpack that is already in the scene (your own design) and only connects it to the Inventory
    static Transform TopPanel(Transform slot)
    {
        var canvas = slot.GetComponentInParent<Canvas>(true);
        Transform t = slot;
        while (t.parent != null && (canvas == null || t.parent != canvas.transform))
            t = t.parent;

        return t == slot ? slot.parent : t;
    }

    static bool UseExistingBackpack(Inventory inventory)
    {
        var all = UnityEngine.Object.FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0) return false;

        // Group the slots by the panel they are in
        var groups = new Dictionary<Transform, List<InventorySlotUI>>();
        foreach (var slot in all)
        {
            if (slot == null || slot.transform.parent == null) continue;

            // The panel = the piece directly under the canvas (your design may have a "SLOTS" container in between)
            Transform panel = TopPanel(slot.transform);
            if (!groups.ContainsKey(panel)) groups[panel] = new List<InventorySlotUI>();
            groups[panel].Add(slot);
        }

        // The design you made: the panel with a picture, then the one that is switched on
        Transform chosen = null;
        int bestScore = -1;
        foreach (var pair in groups)
        {
            var image = pair.Key.GetComponent<Image>();
            int score = (image != null && image.sprite != null ? 4 : 0) +
                        (pair.Key.gameObject.activeInHierarchy ? 2 : 0) +
                        (pair.Value.Count == inventory.slotCount ? 1 : 0);
            if (score > bestScore) { bestScore = score; chosen = pair.Key; }
        }
        if (chosen == null) return false;

        var slots = groups[chosen];
        slots.Sort((x, y) => x.transform.GetSiblingIndex().CompareTo(y.transform.GetSiblingIndex()));

        inventory.slots = slots.ToArray();
        inventory.slotCount = slots.Count;
        inventory.panelRoot = chosen.gameObject;
        inventory.panelImage = chosen.GetComponent<Image>();

        // The tooltip next to the panel
        var canvas = chosen.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            var tooltip = canvas.transform.Find("Tooltip");
            if (tooltip != null) inventory.tooltipRoot = tooltip.gameObject;
        }

        if (groups.Count > 1)
            Debug.LogWarning("InventorySetupTool: there are " + groups.Count + " backpacks in the scene. Using '" + chosen.name +
                             "' (the one with your picture / the one that is switched on). Delete the others to keep things tidy.");

        return true;
    }

    static void BuildNewBackpack(Inventory inventory)
    {
        inventory.slots = null;
        inventory.panelRoot = null;
        inventory.panelImage = null;
        inventory.tooltipRoot = null;
        inventory.BuildUI();

        var canvas = GameObject.Find("InventoryCanvas");
        if (canvas != null) Undo.RegisterCreatedObjectUndo(canvas, "Inventory slots");
    }

    // Throws your backpack away and builds the standard one. Use it only if you want to start over.
    [MenuItem("Tools/Ghost/Rebuild Backpack UI (replaces my design!)")]
    public static void RebuildBackpack()
    {
        if (!EditorUtility.DisplayDialog("Rebuild backpack",
                "This DELETES the backpack that is in the scene (including your design) and builds the standard one. Continue?",
                "Rebuild", "Cancel"))
            return;

        var inventory = UnityEngine.Object.FindFirstObjectByType<Inventory>();
        if (inventory == null)
        {
            EditorUtility.DisplayDialog("Rebuild backpack", "No Inventory found. Run Setup Inventory first.", "OK");
            return;
        }

        foreach (var old in UnityEngine.Object.FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (old == null) continue;

            var oldCanvas = old.GetComponentInParent<Canvas>(true);
            GameObject root = oldCanvas != null && oldCanvas.gameObject.name == "InventoryCanvas"
                ? oldCanvas.gameObject
                : old.gameObject;
            Undo.DestroyObjectImmediate(root);
        }

        Undo.RecordObject(inventory, "Rebuild backpack");
        BuildNewBackpack(inventory);
        EditorUtility.SetDirty(inventory);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("InventorySetupTool: the standard backpack was built again.");
    }

    // Finds your item assets by name and connects them: Empty Faraday Cage (hold, drop, fill with MEP) -> Faraday Cage
    static string ConfigureFaradayItems(Inventory inventory)
    {
        InvItemData empty = null, filled = null, mep = null;

        foreach (var guid in AssetDatabase.FindAssets("t:InvItemData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<InvItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;

            string name = (data.itemName ?? "").ToLowerInvariant();
            string file = data.name.ToLowerInvariant();

            // Matched by the item name or by the asset's file name
            bool isFaraday = name.Contains("faraday") || file.Contains("faraday");
            bool isEmpty = name.Contains("empty") || file.Contains("empty");

            if (isFaraday && isEmpty) empty = data;
            else if (isFaraday) filled = data;
            else if (file.Contains("mep") || name.Contains("molecular") || name == "mep") mep = data;
        }

        if (empty == null)
            return "Item 'Empty Faraday Cage' not found (create it: Create > Ghost > Inventory Item, name it 'Empty Faraday Cage').";

        GameObject cagePrefab = null;
        foreach (var guid in AssetDatabase.FindAssets("Faraday_Cage t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == "Faraday_Cage")
            {
                cagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                break;
            }
        }

        // Only fills what is still empty, so the values you tuned are kept
        if (string.IsNullOrEmpty(empty.itemName) || empty.itemName == "Item") empty.itemName = empty.name;
        if (!empty.equippable) empty.equippable = true;
        if (empty.worldPrefab == null) empty.worldPrefab = cagePrefab;
        if (empty.combineWith == null) empty.combineWith = mep;
        if (empty.combineResult == null) empty.combineResult = filled;

        // Short name for the prompts ("No MEP"), and the new shorter default texts
        if (mep != null && string.IsNullOrEmpty(mep.shortName))
        {
            mep.shortName = "MEP";
            EditorUtility.SetDirty(mep);
        }
        if (empty.missingPrompt == "You need {0} to fill this cage") empty.missingPrompt = "No {0}";
        EditorUtility.SetDirty(empty);
        AssetDatabase.SaveAssets();

        // Every generator produces the MEP as this item (its picture and name)
        if (mep != null)
        {
            foreach (var generator in UnityEngine.Object.FindObjectsByType<BabyGhostGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(generator, "Capsule item");
                generator.capsuleItem = mep;
                EditorUtility.SetDirty(generator);
            }
        }

        inventory.startingItems = new[] { new Inventory.StartItem { item = empty, count = 3 } };

        return "Start items: Empty Faraday Cage x3 (cage model " + (cagePrefab != null ? "found" : "NOT found") +
               ", MEP " + (mep != null ? "found" : "NOT found") + ", filled cage " + (filled != null ? "found" : "NOT found") + ").";
    }

    static T FindAsset<T>(string name) where T : UnityEngine.Object
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:" + typeof(T).Name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }

    [MenuItem("Tools/Ghost/Tag Selected Objects As Inventory Items")]
    public static void TagSelected()
    {
        AddTag(Inventory.TagName);

        int count = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go.scene.IsValid() == false) continue;

            // The player and the camera can never be items (E would "take" the player)
            if (go.GetComponentInChildren<Camera>(true) != null ||
                go.GetComponentInChildren<PlayerController>(true) != null ||
                go.GetComponentInParent<PlayerController>(true) != null)
            {
                Debug.LogWarning("InventorySetupTool: '" + go.name + "' is (or holds) the player or the camera, so it was skipped.");
                continue;
            }

            Undo.RecordObject(go, "Tag inventory item");
            go.tag = Inventory.TagName;

            var item = go.GetComponent<InvItem>();
            if (item == null)
            {
                item = Undo.AddComponent<InvItem>(go);
                item.itemName = go.name;
            }

            EditorUtility.SetDirty(go);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("InventorySetupTool: " + count + " object(s) tagged 'inv' with an InvItem. Set each item's Data (or Icon and Item Name) in the Inspector.");
    }

    static void AddTag(string tag)
    {
        if (Array.IndexOf(InternalEditorUtility.tags, tag) < 0)
            InternalEditorUtility.AddTag(tag);
    }
}
