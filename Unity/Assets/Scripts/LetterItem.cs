using UnityEngine;

// The letter in the backpack. The item is made here in code (no asset to set up): its picture is Resources/Letter/letter.png.
// Click the slot in the backpack and the letter opens on the screen (LetterViewer).
// The player gets it when a game starts (not in the training). To use another picture, replace Resources/Letter/letter.png.
public static class LetterItem
{
    public const string Id = "Letter";

    private static InvItemData data;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        data = null;
    }

    public static InvItemData Data
    {
        get
        {
            if (data != null) return data;

            var page = Resources.Load<Sprite>("Letter/letter");
            if (page == null)
            {
                var texture = Resources.Load<Texture2D>("Letter/letter");
                if (texture != null)
                    page = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (page == null)
            {
                Debug.LogWarning("[Letter] Resources/Letter/letter.png was not found, so there is no letter.");
                return null;
            }

            data = ScriptableObject.CreateInstance<InvItemData>();
            data.hideFlags = HideFlags.DontUnloadUnusedAsset;
            data.name = Id;
            data.itemName = Id;
            data.itemId = Id;
            data.description = "An old letter.";
            data.icon = page;
            data.stackable = false;
            data.maxStack = 1;
            data.readable = true;
            data.readPage = page;
            return data;
        }
    }

    // The letter is in the backpack (it stays there: it can not be dropped or used up)
    public static void Ensure(Inventory inventory)
    {
        if (inventory == null || TutorialMode.Active) return;
        if (inventory.Has(Id)) return;

        var item = Data;
        if (item != null) inventory.AddData(item, 1);
    }
}
