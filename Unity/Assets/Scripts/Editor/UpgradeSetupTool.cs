using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UpgradeSetupTool
{
    [MenuItem("Tools/Ghost/Setup Upgrades And Drop Table")]
    public static void Setup()
    {
        // 1) The upgrade system (Gun, EM, MEP, Radio)
        var system = Object.FindFirstObjectByType<UpgradeSystem>();
        if (system == null)
        {
            var go = new GameObject("Upgrades");
            Undo.RegisterCreatedObjectUndo(go, "Upgrades");
            system = go.AddComponent<UpgradeSystem>();
        }

        // 1b) The Upgrade Panel (key U). Your own design is kept: it is only built when there is none yet.
        var panel = Object.FindFirstObjectByType<UpgradePanel>();
        string panelResult;
        if (panel == null)
        {
            var panelObject = new GameObject("UpgradePanel");
            Undo.RegisterCreatedObjectUndo(panelObject, "Upgrade panel");
            panel = panelObject.AddComponent<UpgradePanel>();
            AssignFont(panel);
            panel.BuildUI();
            Undo.RegisterCreatedObjectUndo(GameObject.Find("UpgradeCanvas"), "Upgrade canvas");
            panelResult = "Upgrade Panel built";
        }
        else
        {
            AssignFont(panel);
            panelResult = "your Upgrade Panel was kept";
        }
        EditorUtility.SetDirty(panel);

        // 2) The Drop Table: the object named "DropTable", or the one you have selected
        GameObject tableObject = GameObject.Find("DropTable");
        if (tableObject == null && Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
            tableObject = Selection.activeGameObject;

        string tableResult = "no object named 'DropTable' found (select the table and run this again)";
        if (tableObject != null)
        {
            if (tableObject.GetComponent<DropTable>() == null)
                Undo.AddComponent<DropTable>(tableObject);
            tableResult = "Drop Table is on '" + tableObject.name + "'";
        }

        // 3) The items: the filled cage earns points, MEP is linked to its upgrade
        InvItemData filled = null, mep = null;
        foreach (var guid in AssetDatabase.FindAssets("t:InvItemData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<InvItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;

            string name = (data.itemName ?? "").ToLowerInvariant();
            string file = data.name.ToLowerInvariant();
            bool isFaraday = name.Contains("faraday") || file.Contains("faraday");
            bool isEmpty = name.Contains("empty") || file.Contains("empty");

            if (isFaraday && !isEmpty) filled = data;
            else if (file.Contains("mep") || name.Contains("molecular") || name == "mep") mep = data;
        }

        string itemResult = "";

        if (filled != null)
        {
            if (filled.submitPoints <= 0) filled.submitPoints = 1;
            if (filled.worldPrefab == null) filled.worldPrefab = FindCagePrefab();
            if (!filled.equippable) filled.equippable = true;
            EditorUtility.SetDirty(filled);
            itemResult += "'" + filled.itemName + "' gives 1 point on the table. ";
        }
        else
        {
            itemResult += "Filled Faraday Cage item not found. ";
        }

        if (mep != null)
        {
            if (string.IsNullOrEmpty(mep.upgradeId)) mep.upgradeId = "MEP";
            EditorUtility.SetDirty(mep);
            itemResult += "MEP is linked to the MEP upgrade. ";

            // Its picture is shown in the Upgrade Panel too
            foreach (var upgrade in system.upgrades)
            {
                if (upgrade != null && upgrade.id == "MEP" && upgrade.icon == null)
                    upgrade.icon = mep.icon;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(system);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = system.gameObject;

        Debug.Log("UpgradeSetupTool: 'Upgrades' (Gun, EM, MEP, Radio) is in the scene, " + panelResult + " (key U). " + tableResult + ". " + itemResult +
                  "Edit the gains, levels and pictures on the Upgrades object. Save the scene.");
    }

    static void AssignFont(UpgradePanel panel)
    {
        if (panel.font != null) return;

        foreach (var guid in AssetDatabase.FindAssets("MetalMania-Regular SDF t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == "MetalMania-Regular SDF")
            {
                panel.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                return;
            }
        }
    }

    // Throws your Upgrade Panel away and builds the standard one. Use it only if you want to start over.
    [MenuItem("Tools/Ghost/Rebuild Upgrade Panel (replaces my design!)")]
    public static void RebuildPanel()
    {
        if (!EditorUtility.DisplayDialog("Rebuild upgrade panel",
                "This DELETES the Upgrade Panel canvas in the scene (including your design) and builds the standard one. Continue?",
                "Rebuild", "Cancel"))
            return;

        var panel = Object.FindFirstObjectByType<UpgradePanel>();
        if (panel == null)
        {
            EditorUtility.DisplayDialog("Rebuild upgrade panel", "No UpgradePanel found. Run Setup Upgrades And Drop Table first.", "OK");
            return;
        }

        if (panel.panelRoot != null)
        {
            var canvas = panel.panelRoot.GetComponentInParent<Canvas>(true);
            Undo.DestroyObjectImmediate(canvas != null ? canvas.gameObject : panel.panelRoot);
        }

        Undo.RecordObject(panel, "Rebuild upgrade panel");
        panel.panelRoot = null;
        AssignFont(panel);
        panel.BuildUI();
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("UpgradeSetupTool: the standard Upgrade Panel was built again.");
    }

    static GameObject FindCagePrefab()
    {
        foreach (var guid in AssetDatabase.FindAssets("Faraday_Cage t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == "Faraday_Cage")
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
