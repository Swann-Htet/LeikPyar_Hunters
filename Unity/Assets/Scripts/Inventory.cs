using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The player's backpack: 10 slots in a panel that opens and closes with B.
//  - Look at an item (InvItem + tag "inv") and press E to take it. The same item twice shares one slot (x2).
//  - While the backpack is open the mouse cursor is free: hover a slot to see the item's name, click it to hold the item in your hand.
//  - A held item is dropped on the floor with a left click. A dropped Empty Faraday Cage can be filled with MEP (F).
// Added to the player by Tools > Ghost > Setup Inventory (or automatically by GhostGun).
public class Inventory : MonoBehaviour
{
    public const string TagName = "inv";

    public static Inventory Instance { get; private set; }

    // True while the backpack is open (the player stops turning the camera)
    public static bool BackpackOpen => Instance != null && Instance.open;

    [System.Serializable]
    public class Entry
    {
        public string id;
        public string displayName;
        public Sprite icon;
        public int count;
        public int maxStack;
        public InvItemData data;
    }

    [System.Serializable]
    public class StartItem
    {
        public InvItemData item;
        public int count = 1;
    }

    [Header("Start")]
    [Tooltip("Items the player has when the game starts (e.g. Empty Faraday Cage x 3)")]
    public StartItem[] startingItems;

    [Header("Pickup")]
    public Camera playerCamera;
    public KeyCode takeKey = KeyCode.E;
    public float pickupReach = 4.8f;
    [Tooltip("How far from the center of the view the item can be (degrees)")]
    public float pickupAngle = 45f;
    [Tooltip("{0} = item name")]
    public string takePrompt = "[E] Take {0}";
    public string fullPrompt = "Inventory full";

    [Header("Hold and drop")]
    [Tooltip("Left click throws the held item out of your hand. It falls with gravity (needs a floor with a collider)")]
    public float dropThrowSpeed = 2.5f;
    [Tooltip("How high the little toss goes")]
    public float dropUpSpeed = 1.5f;
    [Tooltip("The dropped item appears this far in front of the camera (meters)")]
    public float dropSpawnForward = 0.8f;
    [Tooltip("How much the dropped item spins while it falls")]
    public float dropSpin = 3f;
    [Tooltip("Seconds the dropped item takes to grow from the held size to its real size")]
    public float dropGrowTime = 0.3f;
    [Tooltip("Only used to find the floor point if the item falls through the world (meters)")]
    public float dropReach = 3.6f;
    [Tooltip("{0} = item name")]
    public string heldPrompt = "Left click: drop {0}";
    [Tooltip("How many dropped cages may lie on the floor at the same time (0 = no limit). Fill or take back a cage to drop another")]
    public int maxDroppedAtOnce = 0;
    public string droppedLimitMessage = "Fill or take back the dropped cage first";
    [Tooltip("Shown in the tooltip of an item that can be held")]
    public string equipHint = "Click to hold in your hand";
    [Tooltip("Shown in the tooltip of an item that can be read (the letter)")]
    public string readHint = "Click to read";
    public bool closeBackpackOnEquip = true;
    [Tooltip("To fill a dropped cage: how close and how centered you must look at it")]
    public float cageReach = 4.8f;
    public float cageAngle = 30f;
    public KeyCode fillKey = KeyCode.F;

    [Header("Backpack")]
    [Tooltip("Opens and closes the backpack")]
    public KeyCode toggleKey = KeyCode.B;
    [Tooltip("A second key that does the same (None = off)")]
    public KeyCode alternateToggleKey = KeyCode.I;
    public bool startOpen = false;
    [Tooltip("Esc closes it too (Esc also frees the mouse, so B is the clean way)")]
    public bool closeOnEscape = false;
    [Tooltip("On phones a BAG button opens the backpack (there is no B key)")]
    public bool showMobileButton = true;
    public string panelTitle = "BACKPACK  [B]";

    [Header("Font")]
    [Tooltip("e.g. MetalMania-Regular SDF. Used for every text of the backpack")]
    public TMP_FontAsset font;

    [Header("Slots")]
    public int slotCount = 10;
    [Tooltip("Slots per row")]
    public int columns = 5;
    [Tooltip("Built automatically if empty (Tools > Ghost > Setup Inventory builds them in the scene so you can restyle them)")]
    public InventorySlotUI[] slots;
    public GameObject panelRoot;
    public Image panelImage;
    public GameObject tooltipRoot;

    [Header("Backgrounds (drag your own pictures; right-click this component > Apply Style)")]
    [Tooltip("Picture behind the whole backpack. Empty = the plain color below")]
    public Sprite panelSprite;
    public Color panelColor = new Color(0.07f, 0.07f, 0.09f, 0.92f);
    [Tooltip("Picture of every slot. Empty = the plain colors below")]
    public Sprite slotSprite;
    public Color slotEmptyColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
    public Color slotFullColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [Tooltip("The dark frame around each slot")]
    public bool showSlotBorder = true;
    public Color borderColor = new Color(0f, 0f, 0f, 0.6f);
    public Color tooltipColor = new Color(0.05f, 0.05f, 0.07f, 0.95f);

    [Header("Size (used when the slots are built)")]
    public float slotSize = 90f;
    public float slotSpacing = 8f;
    public float panelPadding = 20f;
    [Tooltip("Write the item's name inside the slot (the tooltip shows it anyway)")]
    public bool showNames = false;

    private readonly List<Entry> entries = new List<Entry>();
    private bool open;

    // Held item
    private float dropBlockedUntil;
    private string equippedId;
    private GameObject heldObject;
    private Transform hand;
    private GameObject dropButton;

    // Messages and tooltip
    private string messageText;
    private float messageUntil;
    private TMP_Text tooltipName;
    private TMP_Text tooltipDesc;
    private RectTransform tooltipRect;
    private int hoverIndex = -1;

    public int UsedSlots => entries.Count;

    // What is in the hand (the training checks it)
    public bool IsHolding => heldObject != null;
    public string HeldId => heldObject != null ? equippedId : null;
    public bool IsFull => entries.Count >= slotCount;
    public bool IsOpen => open;
    public IReadOnlyList<Entry> Entries => entries;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) playerCamera = Camera.main;

        hand = FindDeep(transform, "Gun Hand");

        // If the camera is ever switched off or destroyed, the Console says which code did it
        if (playerCamera != null && playerCamera.GetComponent<CameraWatch>() == null)
            playerCamera.gameObject.AddComponent<CameraWatch>();

        RemoveItemFromPlayer();
        AdoptTaggedObjects();

        if (slots == null || slots.Length == 0)
        {
            // Slots already built in the scene by the setup tool?
            var found = FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length > 0)
            {
                System.Array.Sort(found, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                slots = found;
            }
            else
            {
                BuildUI();
            }
        }

        // The panel is the top piece under the canvas that holds the slots
        // (your design may have a "SLOTS" container in between: that is not the panel)
        Transform top = slots.Length > 0 && slots[0] != null ? TopPanel(slots[0].transform) : null;
        if (top != null && (panelRoot == null || (panelRoot.transform != top && panelRoot.transform.IsChildOf(top))))
        {
            panelRoot = top.gameObject;
            panelImage = top.GetComponent<Image>();
        }
        if (panelImage == null && panelRoot != null)
            panelImage = panelRoot.GetComponent<Image>();

        // The slots report hovering and clicking to this inventory
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            slots[i].owner = this;
            slots[i].index = i;

            var border = slots[i].GetComponent<Image>();
            if (border != null)
            {
                slots[i].border = border;
                border.raycastTarget = true; // the slot itself receives the mouse
            }
            else if (slots[i].GetComponent<Graphic>() == null)
            {
                // Your own slot without a picture on it: an invisible one so hovering and clicking work
                var hit = slots[i].gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
            }
        }

        ApplyStyle(false);

        Canvas canvas = panelRoot != null ? panelRoot.GetComponentInParent<Canvas>(true) : null;
        if (tooltipRoot == null && canvas != null) BuildTooltip(canvas.transform);
        else if (tooltipRoot != null) CacheTooltip();

        ApplyFont();
        BuildMobileButtons();
        GiveStartingItems();
        LetterItem.Ensure(this);          // the letter is always in the backpack (not in the training)
        RefreshSlots();

        UpgradeSystem.Changed += OnUpgradesChanged;

        if (panelRoot == null)
            Debug.LogError("[Inventory] No backpack panel found, so " + toggleKey + " can not open it. Run Tools > Ghost > Setup Inventory.", this);
        else
            Debug.Log("[Inventory] Ready: backpack '" + panelRoot.name + "' with " + slots.Length + " slots. Press " + toggleKey +
                      (alternateToggleKey != KeyCode.None ? " or " + alternateToggleKey : "") + " to open it.");

        open = startOpen;
        if (panelRoot != null) panelRoot.SetActive(open);
        if (open) SetCursor(true);
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    // The player must never be an item (it was tagged "inv" by mistake): that made E "take" the player.
    // Fixed here for good; also fix it in the scene: Person > Tag = Untagged, remove the InvItem.
    void RemoveItemFromPlayer()
    {
        foreach (var own in GetComponentsInParent<InvItem>(true))
        {
            Debug.LogWarning("[Inventory] '" + own.name + "' (the player) had the InvItem script. Removed. Set its Tag to Untagged in the scene too.", own);
            Destroy(own);
        }
        foreach (var own in GetComponentsInChildren<InvItem>(true))
        {
            Debug.LogWarning("[Inventory] '" + own.name + "' (part of the player) had the InvItem script. Removed.", own);
            Destroy(own);
        }

        try
        {
            if (gameObject.CompareTag(TagName)) gameObject.tag = "Untagged";
        }
        catch (UnityException) { }
    }

    // Every object tagged "inv" that has no InvItem yet gets one (named like the object)
    void AdoptTaggedObjects()
    {
        try
        {
            foreach (var go in GameObject.FindGameObjectsWithTag(TagName))
            {
                if (go.GetComponent<InvItem>() != null) continue;
                var item = go.AddComponent<InvItem>();
                item.itemName = go.name;
            }
        }
        catch (UnityException)
        {
            Debug.LogWarning("[Inventory] The tag '" + TagName + "' does not exist yet. Run Tools > Ghost > Setup Inventory.");
        }
    }

    void GiveStartingItems()
    {
        if (startingItems == null) return;

        foreach (var s in startingItems)
        {
            if (s != null && s.item != null && s.count > 0)
                AddData(s.item, s.count);
        }
    }

    void Update()
    {
        if (!SettingsPanel.IsOpen && !LetterViewer.IsOpen && (Input.GetKeyDown(toggleKey) || (alternateToggleKey != KeyCode.None && Input.GetKeyDown(alternateToggleKey))))
            Toggle();
        else if (closeOnEscape && open && Input.GetKeyDown(KeyCode.Escape))
            SetOpen(false);

        UpdateTooltipPosition();

        if (!open && !UiLock.Open)
        {
            // Left click drops the held item (on phones the DROP button does)
            if (heldObject != null && !DeviceDetection.IsMobile() && Input.GetMouseButtonDown(0) &&
                Time.time >= dropBlockedUntil && Cursor.lockState == CursorLockMode.Locked)
                TryDrop();

            HandleWorld();
        }

        if (Time.time < messageUntil)
            InteractPrompt.Request(messageText);
    }

    void OnDestroy()
    {
        UpgradeSystem.Changed -= OnUpgradesChanged;
    }

    // Upgrades changed a level: icons, names and tooltips are redrawn
    void OnUpgradesChanged()
    {
        RefreshSlots();
    }

    void LateUpdate()
    {
        if (playerCamera == null) return;

        // Safety net: a camera that was switched off is switched on again
        if (!playerCamera.gameObject.activeInHierarchy)
        {
            for (Transform t = playerCamera.transform; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                Debug.LogError("[Inventory] '" + t.name + "' was switched off (the camera needs it). Switching it on again.");
                t.gameObject.SetActive(true);
            }
        }
        else if (!playerCamera.enabled)
        {
            Debug.LogError("[Inventory] The camera component was disabled. Enabling it again.");
            playerCamera.enabled = true;
        }
    }

    [Header("One kind at a time")]
    [Tooltip("On: the player can carry only one KIND of item. A second kind can only be taken after the first is used or dropped")]
    public bool oneKindAtATime = true;
    public string oneKindPrompt = "You can carry only one kind of item. Use or drop the {0} first";

    // The name of another kind of item the player already carries (null = none)
    string CarriedOtherKind(string id)
    {
        // an item that belongs together with a carried one (the MEP goes into the empty Faraday Cage) is always allowed
        foreach (var e in entries)
        {
            if (e.count <= 0 || e.data == null) continue;
            if (e.data.combineWith != null && e.data.combineWith.Id == id) return null;
        }

        foreach (var e in entries)
            if (e.count > 0 && e.id != id) return e.displayName;
        return null;
    }

    // What the player is looking at: a dropped cage, an item to take, or nothing
    void HandleWorld()
    {
        // An empty cage the NPC gave back sits ON the table: looking straight at it comes first
        PlacedCage cage = FindLookedCage();
        if (cage != null && cage.onTable)
        {
            HandleCage(cage);
            return;
        }

        DropTable table = FindLookedTable();
        if (table != null && HandleTable(table))
            return;

        if (cage != null)
        {
            HandleCage(cage);
            return;
        }

        InvItem target = FindItem();
        if (target != null)
        {
            // only one KIND of item at a time (3 of the same kind are fine)
            string carried = CarriedOtherKind(target.Id);
            if (oneKindAtATime && carried != null)
            {
                InteractPrompt.Request(string.Format(oneKindPrompt, carried));
                return;
            }

            if (!CanAdd(target))
            {
                InteractPrompt.Request(fullPrompt);
                return;
            }

            InteractPrompt.Request(string.Format(takePrompt, target.Name));

            if (Input.GetKeyDown(takeKey) || MobileInput.GetInteract())
            {
                if (Add(target))
                    target.OnPickedUp();
            }
            return;
        }

        if (heldObject != null)
        {
            Entry held = FindEntry(equippedId);
            if (held != null)
                InteractPrompt.Request(string.Format(heldPrompt, held.displayName));
        }
    }

    // ===================== BACKPACK =====================

    public void Toggle()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[Inventory] There is no backpack panel to open. Run Tools > Ghost > Setup Inventory.", this);
            return;
        }

        SetOpen(!open);
        Debug.Log("[Inventory] Backpack " + (open ? "opened" : "closed"));
    }

    public void SetOpen(bool value)
    {
        if (open == value) return;

        open = value;
        PlayerSfx.Play(value ? "BagOpen" : "zip");          // B opens the bag with its sound, B again zips it
        if (value && UpgradePanel.IsOpen) UpgradePanel.Instance.SetOpen(false);
        if (!value) dropBlockedUntil = Time.time + 0.35f;
        if (panelRoot != null) panelRoot.SetActive(value);
        if (!value) HideTooltip();

        SetCursor(value);
    }

    // The mouse is free while the backpack is open and locked again when it closes (phones have no mouse)
    void SetCursor(bool free)
    {
        if (DeviceDetection.IsMobile()) return;

        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    // ===================== SLOT EVENTS =====================

    public void OnSlotHover(int index, bool inside)
    {
        if (!inside)
        {
            if (hoverIndex == index) HideTooltip();
            return;
        }

        hoverIndex = index;
        ShowTooltip(index);
    }

    public void OnSlotClicked(int index)
    {
        if (index < 0 || index >= entries.Count) return;

        Entry e = entries[index];

        // A letter: it opens on the screen (the backpack closes, the game waits)
        if (e.data != null && e.data.readable && e.data.readPage != null)
        {
            HideTooltip();
            SetOpen(false);
            LetterViewer.Show(e.data.readPage);
            return;
        }

        if (e.data == null || !e.data.equippable || e.data.worldPrefab == null) return;

        // Click the held item again: put it away
        if (equippedId == e.id)
        {
            Unequip();
            return;
        }

        if (maxDroppedAtOnce > 0 && PlacedCage.All.Count >= maxDroppedAtOnce)
        {
            ShowMessage(droppedLimitMessage, 2f);
            return;
        }

        Equip(e);
        if (closeBackpackOnEquip) SetOpen(false);
    }

    // ===================== HOLD AND DROP =====================

    void Equip(Entry e)
    {
        Unequip();

        // The gun, the scanner, the injector and the held item never show together
        if (GhostGun.Instance != null) GhostGun.Instance.HolsterGun();
        if (Hotbar.Instance != null) Hotbar.Instance.Clear();

        equippedId = e.id;
        dropBlockedUntil = Time.time + 0.35f; // the click that picked it must not also drop it

        Transform parent = hand != null ? hand : (playerCamera != null ? playerCamera.transform : transform);
        Transform prefab = e.data.worldPrefab.transform;

        heldObject = Instantiate(e.data.worldPrefab, parent, false);
        heldObject.transform.localPosition = e.data.heldPosition;
        heldObject.transform.localRotation = Quaternion.Euler(e.data.heldEuler) * prefab.localRotation;
        heldObject.transform.localScale = prefab.localScale * e.data.heldScale;

        // A held model is only for looking at: no collisions, no pickup
        foreach (var c in heldObject.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var item in heldObject.GetComponentsInChildren<InvItem>(true)) item.pickable = false;
        foreach (var cage in heldObject.GetComponentsInChildren<PlacedCage>(true)) Destroy(cage);

        heldObject.SetActive(true);
        RefreshSlots();
        UpdateDropButton();
    }

    public void Unequip()
    {
        if (heldObject != null) Destroy(heldObject);
        heldObject = null;
        equippedId = null;

        RefreshSlots();
        UpdateDropButton();
    }

    // Throws the held item out of the hand: it grows to its real size, spins, falls with gravity and lands on the floor
    public void TryDrop()
    {
        Entry e = FindEntry(equippedId);
        if (e == null || e.data == null || e.data.worldPrefab == null || playerCamera == null)
        {
            Unequip();
            return;
        }

        // Looking at the Drop Table with a cage in hand: it goes on the table (and earns the point)
        DropTable lookedTable = FindLookedTable();
        if (lookedTable != null)
        {
            if (e.data.submitPoints > 0) SubmitToTable(lookedTable, e);
            else ShowMessage(lookedTable.wrongItemMessage, 2.5f); // an Empty Faraday Cage is not accepted
            return;
        }

        Transform cam = playerCamera.transform;
        Transform prefab = e.data.worldPrefab.transform;

        // It leaves just in front of the player, a little below the eyes, so it is always in the camera's view
        // (and not inside a wall: it starts closer if something is right in front)
        float ahead = dropSpawnForward;
        var aheadHits = Physics.RaycastAll(cam.position, cam.forward, dropSpawnForward + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in aheadHits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            ahead = Mathf.Min(ahead, Mathf.Max(0.3f, h.distance - 0.3f));
        }

        Vector3 start = cam.position + cam.forward * ahead + Vector3.down * 0.25f;
        Quaternion startRotation = Quaternion.Euler(0f, cam.eulerAngles.y + e.data.dropYaw, 0f) * prefab.rotation;

        GameObject dropped = Instantiate(e.data.worldPrefab, start, startRotation);
        dropped.transform.localScale = prefab.localScale * e.data.heldScale;
        dropped.SetActive(true);

        // Solid and heavy: colliders from the model's meshes, a rigidbody for gravity
        AddBoxColliders(dropped);

        var body = dropped.GetComponent<Rigidbody>();
        if (body == null) body = dropped.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.linearDamping = 0.1f;
        body.angularDamping = 0.6f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = cam.forward * dropThrowSpeed + Vector3.up * dropUpSpeed;

        // It stays upright (only turns around the up axis), so it lands the right way up, like in the prefab
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.angularVelocity = Vector3.up * Random.Range(-dropSpin, dropSpin);

        // It never collides with the player or anything the player carries (gun, hand, body): otherwise it
        // could be pushed away (even behind the player) the moment it appears
        var droppedColliders = dropped.GetComponentsInChildren<Collider>(true);
        foreach (var playerCollider in GetComponentsInChildren<Collider>(true))
        {
            foreach (var col in droppedColliders)
                Physics.IgnoreCollision(playerCollider, col);
        }

        Debug.Log("[Inventory] Dropped '" + e.displayName + "' at " + start.ToString("F1") + " thrown along " + cam.forward.ToString("F1"));

        // It can be filled (MEP) and taken back
        var placed = dropped.AddComponent<PlacedCage>();
        placed.source = e.data;
        placed.body = body;
        placed.safePoint = FindDropPoint(cam) + e.data.dropOffset;
        placed.GrowTo(prefab.localScale, dropGrowTime);

        string id = e.id;
        Remove(id, 1);

        // One click per cage: after every drop the hands are empty again.
        // Click the slot again to take the next cage in your hand.
        Unequip();
    }

    static void AddBoxColliders(GameObject go)
    {
        int added = 0;
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;

            var box = filter.gameObject.AddComponent<BoxCollider>();
            box.center = filter.sharedMesh.bounds.center;
            box.size = filter.sharedMesh.bounds.size;
            added++;
        }

        if (added == 0 && go.GetComponentInChildren<Collider>() == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.4f;
        }
    }

    // The floor point where the item lands: in front of the player, stopped by walls
    Vector3 FindDropPoint(Transform cam)
    {
        float distance = dropReach;

        var hits = Physics.RaycastAll(cam.position, cam.forward, dropReach, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.distance < nearest) nearest = h.distance;
        }
        if (nearest < distance) distance = Mathf.Max(0.8f, nearest - 0.4f);

        Vector3 target = cam.position + cam.forward * distance;

        float ground;
        Vector3 from = new Vector3(target.x, cam.position.y, target.z);
        if (!CatchableGhost.TryGetGround(from, transform, null, out ground))
            ground = transform.position.y;

        return new Vector3(target.x, ground, target.z);
    }

    void UpdateDropButton()
    {
        if (dropButton != null) dropButton.SetActive(heldObject != null);
    }

    // ===================== DROP TABLE =====================

    DropTable FindLookedTable()
    {
        if (playerCamera == null) return null;

        Transform cam = playerCamera.transform;
        DropTable best = null;
        float bestAngle = float.MaxValue;

        foreach (var table in DropTable.All)
        {
            if (table == null) continue;

            Vector3 to = table.LookPoint - cam.position;
            if (to.magnitude > table.reach) continue;

            float a = Vector3.Angle(cam.forward, to);
            if (a <= table.angle && a < bestAngle)
            {
                bestAngle = a;
                best = table;
            }
        }
        return best;
    }

    // The first item in the backpack that earns points on the table (the held one first)
    Entry FindSubmittable()
    {
        Entry held = FindEntry(equippedId);
        if (held != null && held.data != null && held.data.submitPoints > 0) return held;

        foreach (var e in entries)
            if (e.data != null && e.data.submitPoints > 0) return e;
        return null;
    }

    // Shows the prompt. Returns true when it handled the table (so nothing else is shown)
    bool HandleTable(DropTable table)
    {
        if (!table.IsOpen)
        {
            InteractPrompt.Request(table.ClosedMessage);
            return true;
        }

        Entry e = FindSubmittable();
        if (e == null)
        {
            if (!table.showHintWhenEmpty) return false;

            InteractPrompt.Request(table.needPrompt);
            return true;
        }

        if (!table.HasFreeSlot)
        {
            InteractPrompt.Request(table.fullPrompt);
            return true;
        }

        InteractPrompt.Request(string.Format(table.putPrompt, e.displayName));

        if (Input.GetKeyDown(table.submitKey) || MobileInput.GetInteract())
            SubmitToTable(table, e);
        return true;
    }

    // The filled cage goes on the table and its check starts. The point comes when the check is done
    void SubmitToTable(DropTable table, Entry e)
    {
        InvItemData data = e.data;

        if (!table.IsOpen)
        {
            ShowMessage(table.ClosedMessage, 2.5f);
            return;
        }

        if (!table.StartCheck(data, data.heldScale))
        {
            ShowMessage(table.fullPrompt, 2.5f);
            return;
        }

        Remove(e.id, 1);
        ShowMessage(table.StartedMessage, 3f);

        // Hands empty again (one click per cage)
        Unequip();
    }

    // ===================== DROPPED CAGE =====================

    PlacedCage FindLookedCage()
    {
        if (playerCamera == null) return null;

        Transform cam = playerCamera.transform;
        PlacedCage best = null;
        float bestAngle = cageAngle;

        foreach (var cage in PlacedCage.All)
        {
            if (cage == null) continue;

            Vector3 to = cage.Point - cam.position;
            if (to.magnitude > cageReach) continue;

            float angle = Vector3.Angle(cam.forward, to);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = cage;
            }
        }
        return best;
    }

    void HandleCage(PlacedCage cage)
    {
        InvItemData data = cage.source;
        if (data == null) return;

        bool canFill = data.combineWith != null && data.combineResult != null;
        bool hasItem = canFill && Has(data.combineWith.Id);

        string prompt;
        if (!canFill) prompt = "";
        else if (hasItem) prompt = string.Format(data.putPrompt, data.combineWith.ShortName);
        else prompt = string.Format(data.missingPrompt, data.combineWith.ShortName);

        if (prompt.Length > 0) prompt += "\n";
        prompt += "[E] Take back";
        InteractPrompt.Request(prompt);

        if (hasItem && (Input.GetKeyDown(fillKey) || MobileInput.GetInteract()))
        {
            FillCage(cage);
        }
        else if (canFill && !hasItem && Input.GetKeyDown(fillKey))
        {
            // Nothing to put in: only the text, nothing else happens
            ShowMessage(string.Format(data.missingPrompt, data.combineWith.ShortName), 1.5f);
        }
        else if (Input.GetKeyDown(takeKey))
        {
            if (AddData(data, 1) > 0) { PlayerSfx.Play("CageRestock"); DestroyCage(cage); }
            else ShowMessage(fullPrompt, 2f);
        }
    }

    // F: the MEP goes into the cage and the filled Faraday Cage is added to the backpack
    void FillCage(PlacedCage cage)
    {
        InvItemData data = cage.source;

        Remove(data.combineWith.Id, 1);

        if (AddData(data.combineResult, 1) <= 0)
        {
            // No room: give the MEP back
            AddData(data.combineWith, 1);
            ShowMessage(fullPrompt, 2f);
            return;
        }

        PlayerSfx.Play("CageRestock");
        DestroyCage(cage);
        ShowMessage(string.Format(data.doneMessage, data.combineResult.itemName), 3f);
    }

    // Removes a dropped item, but never anything that holds the player or the camera
    void DestroyCage(PlacedCage cage)
    {
        bool holdsPlayer = transform.IsChildOf(cage.transform) || (playerCamera != null && playerCamera.transform.IsChildOf(cage.transform));
        if (holdsPlayer)
        {
            Debug.LogError("[Inventory] The dropped item '" + cage.name + "' is a parent of the player or the camera, so it is not destroyed.", cage);
            Destroy(cage);
            return;
        }

        Destroy(cage.gameObject);
    }

    public void ShowMessage(string text, float seconds)
    {
        messageText = text;
        messageUntil = Time.time + seconds;
    }

    // The item closest to the center of the view, within range
    InvItem FindItem()
    {
        if (playerCamera == null) return null;

        Transform cam = playerCamera.transform;
        InvItem best = null;
        float bestAngle = pickupAngle;

        foreach (var item in InvItem.All)
        {
            if (item == null || !item.pickable) continue;
            if (item.transform.IsChildOf(transform) || transform.IsChildOf(item.transform)) continue; // never the player itself
            if (item.IsProtected) continue;

            Vector3 to = item.PickupPoint - cam.position;
            if (to.magnitude > pickupReach) continue;

            float angle = Vector3.Angle(cam.forward, to);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = item;
            }
        }
        return best;
    }

    // ===================== INVENTORY =====================

    Entry FindEntry(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var e in entries)
            if (e.id == id) return e;
        return null;
    }

    Entry FindStack(string id, bool stackable)
    {
        if (!stackable) return null;

        foreach (var e in entries)
        {
            if (e.id == id && e.count < e.maxStack) return e;
        }
        return null;
    }

    public bool CanAdd(InvItem item)
    {
        return FindStack(item.Id, item.Stackable) != null || !IsFull;
    }

    // A world item taken with E
    public bool Add(InvItem item)
    {
        Entry stack = FindStack(item.Id, item.Stackable);

        if (stack != null)
        {
            stack.count++;
        }
        else
        {
            if (IsFull) return false;

            entries.Add(new Entry
            {
                id = item.Id,
                displayName = item.Name,
                icon = item.Icon,
                count = 1,
                maxStack = Mathf.Max(1, item.MaxStack),
                data = item.data
            });
        }

        RefreshSlots();
        return true;
    }

    // Adds an item type straight to the backpack. Returns how many were added
    public int AddData(InvItemData data, int amount)
    {
        if (data == null) return 0;

        int added = 0;
        for (int i = 0; i < amount; i++)
        {
            Entry stack = FindStack(data.Id, data.stackable);
            if (stack != null)
            {
                stack.count++;
                added++;
                continue;
            }

            if (IsFull) break;

            entries.Add(new Entry
            {
                id = data.Id,
                displayName = data.itemName,
                icon = data.icon,
                count = 1,
                maxStack = Mathf.Max(1, data.maxStack),
                data = data
            });
            added++;
        }

        RefreshSlots();
        return added;
    }

    public int GetCount(string id)
    {
        int total = 0;
        foreach (var e in entries)
            if (e.id == id) total += e.count;
        return total;
    }

    public bool Has(string id, int amount = 1)
    {
        return GetCount(id) >= amount;
    }

    // Takes items out (e.g. when they are used)
    public bool Remove(string id, int amount = 1)
    {
        if (GetCount(id) < amount) return false;

        for (int i = entries.Count - 1; i >= 0 && amount > 0; i--)
        {
            if (entries[i].id != id) continue;

            int take = Mathf.Min(amount, entries[i].count);
            entries[i].count -= take;
            amount -= take;

            if (entries[i].count <= 0) entries.RemoveAt(i);
        }

        // The held item is gone from the backpack
        if (heldObject != null && FindEntry(equippedId) == null) Unequip();

        RefreshSlots();
        return true;
    }

    public void RefreshSlots()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            Entry e = i < entries.Count ? entries[i] : null;

            // Items with levels can change their picture and name
            if (e != null && e.data != null)
            {
                e.icon = e.data.CurrentIcon;
                e.displayName = e.data.CurrentName;
            }

            slots[i].Show(e, showNames, e != null && e.id == equippedId && heldObject != null);
        }

        if (hoverIndex >= 0 && tooltipRoot != null && tooltipRoot.activeSelf)
            ShowTooltip(hoverIndex);
    }

    // ===================== TOOLTIP =====================

    void ShowTooltip(int index)
    {
        if (tooltipRoot == null || index < 0 || index >= entries.Count)
        {
            HideTooltip();
            return;
        }

        Entry e = entries[index];

        string description = e.data != null ? e.data.description : "";
        if (e.data != null && e.data.equippable && !string.IsNullOrEmpty(equipHint))
            description = string.IsNullOrEmpty(description) ? equipHint : description + "\n" + equipHint;
        if (e.data != null && e.data.readable && !string.IsNullOrEmpty(readHint))
            description = string.IsNullOrEmpty(description) ? readHint : description + "\n" + readHint;

        // Upgradable items show their level and percent
        if (e.data != null && !string.IsNullOrEmpty(e.data.upgradeId) && UpgradeSystem.Instance != null)
        {
            string upgrade = UpgradeSystem.Instance.Describe(e.data.upgradeId);
            if (upgrade.Length > 0)
                description = string.IsNullOrEmpty(description) ? upgrade : description + "\n" + upgrade;
        }

        tooltipName.text = e.displayName;
        tooltipDesc.text = description;

        float width = tooltipRect.sizeDelta.x;
        float descHeight = string.IsNullOrEmpty(description) ? 0f : tooltipDesc.GetPreferredValues(description, width - 24f, 0f).y;

        var descRt = (RectTransform)tooltipDesc.transform;
        descRt.sizeDelta = new Vector2(-24f, descHeight);
        tooltipDesc.gameObject.SetActive(descHeight > 0f);

        tooltipRect.sizeDelta = new Vector2(width, 14f + 34f + (descHeight > 0f ? descHeight + 8f : 0f) + 12f);

        tooltipRoot.transform.SetAsLastSibling();
        tooltipRoot.SetActive(true);
        UpdateTooltipPosition();
    }

    void HideTooltip()
    {
        hoverIndex = -1;
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    // The tooltip follows the mouse and stays on the screen
    void UpdateTooltipPosition()
    {
        if (tooltipRoot == null || !tooltipRoot.activeSelf || tooltipRect == null) return;

        Canvas canvas = tooltipRoot.GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 size = tooltipRect.sizeDelta * scale;

        Vector2 mouse = Input.mousePosition;
        Vector2 position = mouse + new Vector2(22f, -22f);

        if (position.x + size.x > Screen.width) position.x = mouse.x - size.x - 12f;
        if (position.y - size.y < 0f) position.y = mouse.y + size.y + 12f;

        tooltipRect.position = position;
    }

    void BuildTooltip(Transform canvasTransform)
    {
        tooltipRoot = NewChild(canvasTransform, "Tooltip", typeof(Image));

        tooltipRect = (RectTransform)tooltipRoot.transform;
        tooltipRect.anchorMin = tooltipRect.anchorMax = Vector2.zero;
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(320f, 70f);

        var bg = tooltipRoot.GetComponent<Image>();
        bg.color = tooltipColor;
        bg.raycastTarget = false;

        tooltipName = NewText(tooltipRoot.transform, "Name", 26f, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        var nameRt = (RectTransform)tooltipName.transform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.sizeDelta = new Vector2(-24f, 34f);
        nameRt.anchoredPosition = new Vector2(0f, -14f);

        tooltipDesc = NewText(tooltipRoot.transform, "Description", 18f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        tooltipDesc.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var descRt = (RectTransform)tooltipDesc.transform;
        descRt.anchorMin = new Vector2(0f, 1f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0.5f, 1f);
        descRt.sizeDelta = new Vector2(-24f, 60f);
        descRt.anchoredPosition = new Vector2(0f, -52f);

        tooltipRoot.SetActive(false);
    }

    void CacheTooltip()
    {
        tooltipRect = tooltipRoot.GetComponent<RectTransform>();

        var name = tooltipRoot.transform.Find("Name");
        var desc = tooltipRoot.transform.Find("Description");
        if (name != null) tooltipName = name.GetComponent<TMP_Text>();
        if (desc != null) tooltipDesc = desc.GetComponent<TMP_Text>();
    }

    // ===================== LOOK =====================

    [ContextMenu("Apply Style")]
    void ApplyStyleFromMenu()
    {
        ApplyStyle(true);
        ApplyFont();
    }

    // Puts the backgrounds from the fields above onto the panel and the slots.
    // At game start only pictures you assigned are applied, so colors you changed by hand stay.
    public void ApplyStyle(bool includeColors)
    {
        if (panelImage != null)
        {
            if (panelSprite != null || includeColors) SetSprite(panelImage, panelSprite);
            if (includeColors) panelImage.color = panelColor;
        }

        if (slots == null) return;

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            if (includeColors)
            {
                slot.useStyleColors = true;
                slot.emptyColor = slotEmptyColor;
                slot.fullColor = slotFullColor;
                slot.borderNormal = borderColor;
                slot.showBorder = showSlotBorder;
            }

            if (slot.fill != null && (slotSprite != null || includeColors))
                SetSprite(slot.fill, slotSprite);
        }

        RefreshSlots();
    }

    // Every text of the backpack uses the font above
    public void ApplyFont()
    {
        if (font == null || panelRoot == null) return;

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>(true);
        Transform root = canvas != null ? canvas.transform : panelRoot.transform;

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            text.font = font;
    }

    // The piece directly under the canvas that contains this slot
    static Transform TopPanel(Transform slot)
    {
        var canvas = slot.GetComponentInParent<Canvas>(true);
        Transform t = slot;
        while (t.parent != null && (canvas == null || t.parent != canvas.transform))
            t = t.parent;

        return t == slot ? slot.parent : t;
    }

    static void SetSprite(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
    }

    // ===================== UI BUILDING =====================

    // Builds the backpack (a canvas named "InventoryCanvas" with a panel and the slots).
    // Called automatically, or by Tools > Ghost > Setup Inventory so everything exists in the scene
    public void BuildUI()
    {
        var canvasGO = new GameObject("InventoryCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        int n = Mathf.Max(1, slotCount);
        int cols = Mathf.Clamp(columns, 1, n);
        int rows = Mathf.CeilToInt(n / (float)cols);
        const float titleHeight = 44f;

        float width = cols * slotSize + (cols - 1) * slotSpacing + 2f * panelPadding;
        float height = titleHeight + rows * slotSize + (rows - 1) * slotSpacing + 2f * panelPadding;

        // The panel (the backpack's background)
        panelRoot = NewChild(canvasGO.transform, "Backpack", typeof(Image));
        var panelRt = (RectTransform)panelRoot.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(width, height);
        panelRt.anchoredPosition = Vector2.zero;
        panelImage = panelRoot.GetComponent<Image>();
        panelImage.raycastTarget = true; // taps on the open backpack don't turn the camera

        // Title
        var title = NewText(panelRoot.transform, "Title", 26f, TextAlignmentOptions.Center, FontStyles.Bold);
        title.text = panelTitle;
        var titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, titleHeight);
        titleRt.anchoredPosition = new Vector2(0f, -panelPadding * 0.5f);

        // The slots, in rows from the top left
        slots = new InventorySlotUI[n];
        float startX = -width / 2f + panelPadding + slotSize / 2f;
        float startY = height / 2f - panelPadding - titleHeight - slotSize / 2f;

        for (int i = 0; i < n; i++)
        {
            var slotGO = new GameObject("Slot " + (i + 1), typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(panelRoot.transform, false);

            var rt = (RectTransform)slotGO.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(slotSize, slotSize);
            rt.anchoredPosition = new Vector2(
                startX + (i % cols) * (slotSize + slotSpacing),
                startY - (i / cols) * (slotSize + slotSpacing));

            var border = slotGO.GetComponent<Image>();
            border.raycastTarget = true; // receives the mouse (hover and click)

            var slot = slotGO.AddComponent<InventorySlotUI>();
            slot.border = border;
            slot.owner = this;
            slot.index = i;

            // The slot's background
            var fill = NewChild(slotGO.transform, "Fill", typeof(Image));
            Stretch(fill, 4f, 4f, 4f, 4f);
            slot.fill = fill.GetComponent<Image>();
            slot.fill.raycastTarget = false;

            // The item's picture: fills the whole slot
            var icon = NewChild(slotGO.transform, "Icon", typeof(Image));
            Stretch(icon, 4f, 4f, 4f, 4f);
            slot.icon = icon.GetComponent<Image>();
            slot.icon.preserveAspect = true;
            slot.icon.raycastTarget = false;
            slot.icon.enabled = false;

            // First letter when the item has no picture
            slot.letter = NewText(slotGO.transform, "Letter", 36f, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(slot.letter.gameObject, 0f, 0f, 0f, 0f);
            slot.letter.gameObject.SetActive(false);

            // Item name (small, at the bottom; off by default, the tooltip shows it)
            slot.nameText = NewText(slotGO.transform, "Name", 14f, TextAlignmentOptions.Center, FontStyles.Normal);
            slot.nameText.enableAutoSizing = true;
            slot.nameText.fontSizeMin = 6f;
            slot.nameText.fontSizeMax = 14f;
            slot.nameText.overflowMode = TextOverflowModes.Ellipsis;
            var nameRt = (RectTransform)slot.nameText.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.sizeDelta = new Vector2(-6f, 18f);
            nameRt.anchoredPosition = new Vector2(0f, 3f);
            slot.nameText.gameObject.SetActive(false);

            // Count (x3) in the corner, on top of the picture
            slot.countText = NewText(slotGO.transform, "Count", 28f, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            slot.countText.outlineWidth = 0.25f;
            slot.countText.outlineColor = new Color32(0, 0, 0, 255);
            var countRt = (RectTransform)slot.countText.transform;
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0f);
            countRt.pivot = new Vector2(1f, 0f);
            countRt.sizeDelta = new Vector2(60f, 36f);
            countRt.anchoredPosition = new Vector2(-6f, 4f);
            slot.countText.gameObject.SetActive(false);

            slots[i] = slot;
        }

        BuildTooltip(canvasGO.transform);
        ApplyStyle(true);
        ApplyFont();
        panelRoot.SetActive(startOpen);
    }

    // Called by MobileControls: a phone seen late (Device Simulator) still gets its BAG and DROP buttons
    public void EnsureMobileButtons()
    {
        if (dropButton == null) BuildMobileButtons();
    }

    // BAG (opens the backpack) and DROP (drops the held item) buttons for phones
    void BuildMobileButtons()
    {
        if (!DeviceDetection.IsMobile() || panelRoot == null) return;

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        if (showMobileButton)
            MakeMobileButton(canvas.transform, "BagButton", "BAG", new Vector2(-30f, -190f), Toggle);

        dropButton = MakeMobileButton(canvas.transform, "DropButton", "DROP", new Vector2(-30f, -430f), TryDrop);
        dropButton.SetActive(false);
    }

    GameObject MakeMobileButton(Transform canvasTransform, string name, string label, Vector2 position, UnityAction onClick)
    {
        var go = NewChild(canvasTransform, name, typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.anchoredPosition = position;

        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var text = NewText(go.transform, "Label", 26f, TextAlignmentOptions.Center, FontStyles.Bold);
        text.text = label;
        Stretch(text.gameObject, 0f, 0f, 0f, 0f);

        go.GetComponent<Button>().onClick.AddListener(onClick);
        return go;
    }

    static GameObject NewChild(Transform parent, string name, params System.Type[] extraComponents)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var type in extraComponents)
            go.AddComponent(type);
        return go;
    }

    TMP_Text NewText(Transform parent, string name, float size, TextAlignmentOptions alignment, FontStyles style)
    {
        var go = NewChild(parent, name);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(GameObject go, float left, float bottom, float right, float top)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static Transform FindDeep(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }
}
