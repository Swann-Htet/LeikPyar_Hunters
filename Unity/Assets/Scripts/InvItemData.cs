using UnityEngine;

// One item type for the inventory: its name, your drawn picture, and what it can do.
// Create with: right-click in the Project window > Create > Ghost > Inventory Item.
// Many objects can share one of these (e.g. 10 keys all use the "Key" item), so you set the picture once.
[CreateAssetMenu(fileName = "NewItem", menuName = "Ghost/Inventory Item")]
public class InvItemData : ScriptableObject
{
    [Header("Slot")]
    [Tooltip("Shown in the pickup text and when you hover the slot")]
    public string itemName = "Item";

    [Tooltip("Your drawn picture (a Sprite). It fills the whole slot")]
    public Sprite icon;

    [Tooltip("Short name used in prompts, e.g. MEP. Empty = the item name")]
    public string shortName;

    [Tooltip("Shown under the name when you hover the slot")]
    [TextArea(1, 4)]
    public string description;

    [Tooltip("Items with the same ID share one slot (2 of them show '2'). Empty = the item name")]
    public string itemId;

    public bool stackable = true;
    public int maxStack = 99;

    [Header("Read (click the slot in the backpack)")]
    [Tooltip("Click it in the backpack: the picture below opens on the screen with an animation (a letter, a note)")]
    public bool readable;
    [Tooltip("The picture that opens when the item is read")]
    public Sprite readPage;

    [Header("Hold and drop (click the slot in the backpack)")]
    [Tooltip("Click it in the backpack: it appears in the gun hand. Left click drops it on the floor")]
    public bool equippable;
    [Tooltip("The model that is held and dropped, e.g. the Faraday_Cage prefab")]
    public GameObject worldPrefab;
    [Tooltip("Where the held model sits relative to the Gun Hand")]
    public Vector3 heldPosition = new Vector3(0f, 0f, 0.1f);
    [Tooltip("Extra rotation of the held model (degrees)")]
    public Vector3 heldEuler = Vector3.zero;
    [Tooltip("Size of the held model (1 = the prefab's own size)")]
    public float heldScale = 0.5f;
    [Tooltip("Moves the dropped model from the floor point (meters)")]
    public Vector3 dropOffset = Vector3.zero;
    [Tooltip("Turn of the dropped model around the up axis, relative to where the player looks (degrees)")]
    public float dropYaw = 180f;

    [Header("Fill it after dropping (e.g. put MEP in the cage)")]
    [Tooltip("The item the player must have to fill the dropped model (e.g. MEP). Empty = nothing to fill")]
    public InvItemData combineWith;
    [Tooltip("The item the player gets when F is pressed (e.g. the filled Faraday Cage)")]
    public InvItemData combineResult;
    [Tooltip("Shown when the player looks at the dropped model and has the item")]
    public string putPrompt = "Put {0} in this cage   [F]";
    [Tooltip("Shown when the player does not have the item. {0} = its name")]
    public string missingPrompt = "No {0}";
    [Tooltip("Shown after F. {0} = the new item's name")]
    public string doneMessage = "{0} added to inventory";

    [Header("Upgrades and the Drop Table")]
    [Tooltip("Links this item to an entry of the UpgradeSystem (e.g. MEP): its level and percent show in the tooltip")]
    public string upgradeId;
    [Tooltip("Optional: a different picture for each level (first = level 1)")]
    public Sprite[] levelIcons;
    [Tooltip("Show 'Lv 2' after the name")]
    public bool showLevelInName;
    [Tooltip("Points the player gets when this item is put on the Drop Table (0 = it can not be put there). 1 for the Faraday Cage")]
    public int submitPoints = 0;

    // The picture for the current level (if you gave level pictures), otherwise the normal icon
    public Sprite CurrentIcon
    {
        get
        {
            if (levelIcons != null && levelIcons.Length > 0 && !string.IsNullOrEmpty(upgradeId) && UpgradeSystem.Instance != null)
            {
                int level = UpgradeSystem.Instance.GetLevel(upgradeId);
                if (level > 0)
                {
                    Sprite sprite = levelIcons[Mathf.Clamp(level - 1, 0, levelIcons.Length - 1)];
                    if (sprite != null) return sprite;
                }
            }
            return icon;
        }
    }

    public string CurrentName
    {
        get
        {
            if (showLevelInName && !string.IsNullOrEmpty(upgradeId) && UpgradeSystem.Instance != null)
            {
                int level = UpgradeSystem.Instance.GetLevel(upgradeId);
                if (level > 0) return itemName + " Lv" + level;
            }
            return itemName;
        }
    }

    public string ShortName => string.IsNullOrEmpty(shortName) ? itemName : shortName;

    public string Id => string.IsNullOrEmpty(itemId) ? itemName : itemId;
}
