using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows the items the player carries on the HUD (a column of small plates at the left edge of the screen): the picture of the item and,
// when there is more than one, how many. The backpack (B) still holds the details. It makes itself in the game scene.
public class ItemHud : MonoBehaviour
{
    [Header("Place (canvas units, from the left edge and the middle of the screen)")]
    public Vector2 position = new Vector2(34f, 40f);
    public float slotSize = 78f;
    public float spacing = 10f;
    [Tooltip("The column is hidden while the backpack is open (it shows the same things)")]
    public bool hideWhileBackpackOpen = true;

    static readonly Color Plate = new Color(0.04f, 0.04f, 0.045f, 0.7f);
    static readonly Color Edge = new Color(0.35f, 0.063f, 0.11f, 0.95f);
    static readonly Color Bone = new Color(0.92f, 0.88f, 0.80f, 0.95f);

    private class Slot
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text count;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private RectTransform column;
    private CanvasGroup group;
    private float nextRefresh;
    private string lastSignature = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoCreate();
    }

    static void AutoCreate()
    {
        if (FindFirstObjectByType<Inventory>() == null || FindFirstObjectByType<ItemHud>() != null) return;
        new GameObject("Item HUD (auto)").AddComponent<ItemHud>();
    }

    void Start()
    {
        var canvasGo = new GameObject("ItemHudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 66;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<HorrorSkinIgnore>();

        group = canvasGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        column = UiKit.NewRect("Column", canvasGo.transform);
        column.anchorMin = column.anchorMax = new Vector2(0f, 0.5f);
        column.pivot = new Vector2(0f, 0.5f);
        column.anchoredPosition = position;
        column.sizeDelta = new Vector2(slotSize, slotSize);

        Refresh(true);
    }

    void Update()
    {
        if (group != null)
        {
            bool hide = hideWhileBackpackOpen && Inventory.Instance != null && Inventory.Instance.IsOpen;
            group.alpha = hide ? 0f : 1f;
        }

        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        Refresh(false);
    }

    void Refresh(bool force)
    {
        var inventory = Inventory.Instance;
        if (inventory == null || column == null) return;

        // only redraw when something changed
        var entries = inventory.Entries;
        string signature = "";
        for (int i = 0; i < entries.Count; i++)
            signature += entries[i].id + ":" + entries[i].count + ";";
        if (!force && signature == lastSignature) return;
        lastSignature = signature;

        while (slots.Count < entries.Count) slots.Add(MakeSlot(slots.Count));

        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < entries.Count;
            slots[i].root.gameObject.SetActive(used);
            if (!used) continue;

            var e = entries[i];
            slots[i].icon.sprite = e.icon;
            slots[i].icon.enabled = e.icon != null;
            slots[i].count.text = e.count > 1 ? "x" + e.count : "";
        }
    }

    Slot MakeSlot(int index)
    {
        var slot = new Slot();

        slot.root = UiKit.NewRect("Item " + index, column);
        slot.root.anchorMin = slot.root.anchorMax = new Vector2(0f, 0.5f);
        slot.root.pivot = new Vector2(0f, 0.5f);
        slot.root.sizeDelta = new Vector2(slotSize, slotSize);
        slot.root.anchoredPosition = new Vector2(0f, -index * (slotSize + spacing));

        var plate = slot.root.gameObject.AddComponent<Image>();
        plate.sprite = UiKit.Circle;
        plate.color = Plate;
        plate.raycastTarget = false;

        var ring = UiKit.NewRect("Edge", slot.root);
        UiKit.Stretch(ring);
        var ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = UiKit.Ring;
        ringImage.color = Edge;
        ringImage.raycastTarget = false;

        var iconRt = UiKit.NewRect("Icon", slot.root);
        UiKit.Stretch(iconRt);
        iconRt.offsetMin = new Vector2(slotSize * 0.14f, slotSize * 0.14f);
        iconRt.offsetMax = new Vector2(-slotSize * 0.14f, -slotSize * 0.14f);
        slot.icon = iconRt.gameObject.AddComponent<Image>();
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;

        slot.count = UiKit.Label(slot.root, "Count", "", UiKit.DmSemi != null ? UiKit.DmSemi : UiKit.Body, slotSize * 0.28f, Bone,
            TextAlignmentOptions.BottomRight, new Vector2(slotSize * 0.12f, -slotSize * 0.12f), new Vector2(slotSize * 0.7f, slotSize * 0.4f));
        slot.count.raycastTarget = false;
        slot.count.gameObject.AddComponent<LocalizeIgnore>();

        return slot;
    }
}
