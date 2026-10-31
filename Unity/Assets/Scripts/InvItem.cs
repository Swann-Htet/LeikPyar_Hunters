using System.Collections.Generic;
using UnityEngine;

// Put this on anything the player can pick up into the inventory.
// The object's Tag must be "inv" (Tools > Ghost > Tag Selected Objects As Inventory Items does both).
public class InvItem : MonoBehaviour
{
    public static readonly List<InvItem> All = new List<InvItem>();

    [Header("Item type (easiest: one asset per item type)")]
    [Tooltip("Drag an item asset here (Create > Ghost > Inventory Item). Its name and picture are used instead of the fields below")]
    public InvItemData data;

    [Header("Or set it directly on this object")]
    [Tooltip("Shown under the picture and in the pickup text")]
    public string itemName = "Item";
    [Tooltip("The picture shown in the inventory slot")]
    public Sprite icon;

    [Header("Stacking")]
    [Tooltip("Items with the same ID share one slot (2 of them show '2'). Empty = the item name")]
    public string itemId;
    public bool stackable = true;
    public int maxStack = 99;

    [Header("Pickup")]
    public bool pickable = true;
    [Tooltip("Remove the object from the world when taken. Off = it is only hidden")]
    public bool destroyOnPickup = true;

    // What the inventory uses: the item asset if there is one, otherwise the fields on this object
    public string Name => data != null ? data.itemName : itemName;
    public Sprite Icon => data != null && data.icon != null ? data.icon : icon;
    public bool Stackable => data != null ? data.stackable : stackable;
    public int MaxStack => data != null ? data.maxStack : maxStack;
    public string Id
    {
        get
        {
            if (data != null) return data.Id;
            return string.IsNullOrEmpty(itemId) ? itemName : itemId;
        }
    }

    // Where the player has to look to take it
    public Vector3 PickupPoint
    {
        get
        {
            var r = GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : transform.position;
        }
    }

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    // The player, the camera and anything that holds them can never be picked up
    public bool IsProtected =>
        GetComponentInChildren<Camera>(true) != null ||
        GetComponentInChildren<PlayerController>(true) != null ||
        GetComponentInParent<PlayerController>(true) != null;

    public void OnPickedUp()
    {
        if (IsProtected)
        {
            Debug.LogError("[InvItem] '" + name + "' is the player (or has the camera), so it is not picked up. Remove the InvItem and set its Tag to Untagged.", this);
            pickable = false;
            return;
        }

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
