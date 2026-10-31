using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Put on the "DropTable" object.
//  - It is only open between Open Hour and Close Hour on the wall clock (7:00 AM to 4:00 PM).
//  - Only a FILLED Faraday Cage can be put here (F while looking at the table, or left click with the cage in hand).
//  - The cage stays on the table while it is checked (1 minute, with a countdown above it).
//    When the check is done the player gets the point, and every upgrade goes up (UpgradeSystem).
public class DropTable : MonoBehaviour
{
    public static readonly List<DropTable> All = new List<DropTable>();

    [Header("Open hours (game clock, 24h)")]
    public bool useOpenHours = true;
    [Tooltip("7 = 7:00 AM")]
    public float openHour = 7f;
    [Tooltip("16 = 4:00 PM")]
    public float closeHour = 16f;
    [Tooltip("{0} = open time, {1} = close time")]
    public string closedPrompt = "The table is closed   (open {0} - {1})";

    [Header("Interaction")]
    public float reach = 5.2f;
    [Tooltip("How centered the table must be in the view (degrees)")]
    public float angle = 40f;
    public KeyCode submitKey = KeyCode.F;

    [Header("Texts")]
    [Tooltip("{0} = item name")]
    public string putPrompt = "Put {0} on the table   [F]";
    [Tooltip("Shown when you look at the table without a filled cage")]
    public string needPrompt = "Bring a filled Faraday Cage here";
    public bool showHintWhenEmpty = true;
    public string fullPrompt = "The table is full. Wait until a check is done";
    [Tooltip("Shown when you try to put an Empty Faraday Cage (or anything else) on the table")]
    public string wrongItemMessage = "Only a filled Faraday Cage can be checked here";
    [Tooltip("{0} = the time the check takes")]
    public string startedMessage = "Checking the cage...  {0}";

    [Header("Checking")]
    [Tooltip("Seconds a cage is checked on the table before the player gets the point (60 = 1 minute)")]
    public float checkSeconds = 60f;
    [Tooltip("The check waits while the table is closed")]
    public bool pauseWhenClosed = true;
    [Tooltip("{0} = time left")]
    public string checkingFormat = "CHECKING  {0}";
    public string pausedFormat = "WAITING (closed)  {0}";
    public string checkedText = "CHECKED";
    [Tooltip("Seconds the cage stays after the check is done")]
    public float checkedShowSeconds = 2f;
    [Tooltip("Gap between the top of the cage and the countdown (meters)")]
    public float textHeight = 0.3f;
    public float textSize = 3f;

    [Header("Giving the cage back")]
    [Tooltip("After the check the NPC puts an EMPTY cage on the table for the player. Empty = found by itself (the item that fills into the checked cage)")]
    public InvItemData returnItem;
    public bool giveBackEmptyCage = true;
    [Tooltip("{0} = the cage item name")]
    public string giveBackMessage = "Your empty {0} is back on the table";

    [Header("The cages on the table")]
    [Tooltip("Where the cages are put. Empty = the top of the table")]
    public Transform placePoint;
    [Tooltip("Moves the cage on the table top sideways (meters). The height is not used: the cage always lands with its bottom ON the table top")]
    public Vector3 visualOffset = new Vector3(0f, 0.1f, 0f);
    [Tooltip("How many cages can be checked at the same time")]
    public int maxShown = 3;
    [Tooltip("Space between the cages on the table (meters)")]
    public float spacing = 0.3f;
    [Tooltip("The cage falls onto the table from this height (meters)")]
    public float dropHeight = 0.5f;
    public float dropTime = 0.35f;

    private class Check
    {
        public GameObject cage;
        public TextMeshPro text;
        public float remaining;
        public int points;
        public int slot;
        public bool done;
        public float doneTimer;
        public InvItemData data;
        public float scale;
        public Vector3 target;
        public float surface;     // the height of the table top where it stands
    }

    // An empty cage that was given back and waits on the table until the player takes it
    private class Returned
    {
        public PlacedCage cage;
        public int slot;
    }

    private readonly List<Returned> returned = new List<Returned>();

    private readonly List<Check> checks = new List<Check>();
    private WallClockController clock;

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    // ===================== OPEN HOURS =====================

    public bool IsOpen
    {
        get
        {
            if (!useOpenHours) return true;

            if (clock == null) clock = FindFirstObjectByType<WallClockController>();
            if (clock == null) return true;

            float hour = clock.GameHour24;
            return openHour <= closeHour
                ? hour >= openHour && hour < closeHour
                : hour >= openHour || hour < closeHour;
        }
    }

    public string ClosedMessage => string.Format(closedPrompt, FormatHour(openHour), FormatHour(closeHour));

    public bool HasFreeSlot => FreeSlot() >= 0;

    // Cages that are being checked right now (the NPC works while this is above 0)
    public int ActiveChecks
    {
        get
        {
            int n = 0;
            foreach (var c in checks)
                if (c.cage != null) n++;   // also while the checked cage is swapped for the empty one
            return n;
        }
    }

    public static bool AnyChecking
    {
        get
        {
            foreach (var t in All)
                if (t != null && t.ActiveChecks > 0) return true;
            return false;
        }
    }

    // True while at least one table is open. No table at all counts as closed
    public static bool AnyOpen
    {
        get
        {
            foreach (var t in All)
                if (t != null && t.IsOpen) return true;
            return false;
        }
    }

    static string FormatHour(float hour24)
    {
        int h = Mathf.FloorToInt(hour24) % 24;
        int m = Mathf.RoundToInt((hour24 - Mathf.Floor(hour24)) * 60f);
        string period = h >= 12 ? "PM" : "AM";
        int h12 = h % 12;
        if (h12 == 0) h12 = 12;
        return h12 + ":" + m.ToString("00") + " " + period;
    }

    static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    // ===================== LOOKING =====================

    // Where the player has to look
    public Vector3 LookPoint
    {
        get
        {
            var r = GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : transform.position;
        }
    }

    // The box around the whole table (the NPC stands next to it)
    public Bounds TableBounds
    {
        get
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }

    // The middle of the table's top surface
    Vector3 TopPoint
    {
        get
        {
            var r = GetComponentInChildren<Renderer>();
            return r != null
                ? new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z)
                : transform.position + Vector3.up;
        }
    }

    // ===================== CHECKING =====================

    int FreeSlot()
    {
        int count = Mathf.Max(1, maxShown);
        returned.RemoveAll(r => r.cage == null);

        for (int i = 0; i < count; i++)
        {
            bool used = false;
            foreach (var c in checks)
                if (c.slot == i) { used = true; break; }
            foreach (var r in returned)
                if (r.slot == i) { used = true; break; }
            if (!used) return i;
        }
        return -1;
    }

    // The cage goes on the table and its check begins. False if the table is closed or full
    public bool StartCheck(InvItemData data, float scaleMultiplier)
    {
        if (!IsOpen || data == null || data.worldPrefab == null) return false;

        int slot = FreeSlot();
        if (slot < 0) return false;

        Vector3 center = placePoint != null ? placePoint.position : TopPoint;
        float surface = SurfaceHeight(center);

        var cage = Instantiate(data.worldPrefab, new Vector3(center.x, surface, center.z), data.worldPrefab.transform.rotation);
        cage.transform.localScale = data.worldPrefab.transform.localScale * scaleMultiplier;
        cage.SetActive(true);

        // Cages never overlap: the gap is at least as wide as the cage itself
        float width = Mathf.Max(BoundsOf(cage).size.x, BoundsOf(cage).size.z);
        float step = Mathf.Max(spacing, width * 1.2f);
        float side = (slot - (Mathf.Max(1, maxShown) - 1) * 0.5f) * step;
        Vector3 across = center + transform.right * side + new Vector3(visualOffset.x, 0f, visualOffset.z);

        // It lands with its BOTTOM on the table top (not with its middle on it, which sinks it into the table)
        Vector3 target = SeatOnSurface(cage, across, surface);
        cage.transform.position = target + Vector3.up * dropHeight;

        foreach (var c in cage.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var item in cage.GetComponentsInChildren<InvItem>(true)) item.pickable = false;

        // The countdown above the cage
        var textObject = new GameObject("CheckText");
        var text = textObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = textSize;
        text.color = Color.white;
        text.text = string.Format(checkingFormat, FormatTime(checkSeconds));

        checks.Add(new Check
        {
            cage = cage,
            text = text,
            remaining = checkSeconds,
            points = Mathf.Max(1, data.submitPoints),
            slot = slot,
            data = data,
            scale = scaleMultiplier,
            target = target,
            surface = surface
        });

        StartCoroutine(DropRoutine(cage.transform, target));
        return true;
    }

    public string StartedMessage => string.Format(startedMessage, FormatTime(checkSeconds));

    // ===================== THE SAVED GAME =====================
    // A saved game must not lose the cages that are on the table: the ones the NPC is checking (they go back on the table with the time they
    // still need), and the empty cages that wait to be taken (they go back in the backpack).

    // True if this object is a cage that is being checked here
    public bool OwnsCage(GameObject go)
    {
        foreach (var c in checks)
            if (c.cage != null && go != null && (go == c.cage || go.transform.IsChildOf(c.cage.transform))) return true;
        return false;
    }

    // pending: the cages still being checked. extra: items that belong in the backpack (item id -> how many): the empty cages that wait on
    // the table, and the empty cage of a check that is just finished
    public void ExportForSave(List<GameSave.CheckEntry> pending, Dictionary<string, int> extra)
    {
        foreach (var c in checks)
        {
            if (c.cage == null || c.data == null) continue;

            if (!c.done)
            {
                pending.Add(new GameSave.CheckEntry { id = c.data.Id, remaining = c.remaining, scale = c.scale });
            }
            else if (giveBackEmptyCage)
            {
                // the points are given already: the empty cage that would come back goes to the backpack
                InvItemData empty = returnItem != null ? returnItem : FindEmptyFor(c.data);
                if (empty != null) extra[empty.Id] = (extra.TryGetValue(empty.Id, out int n) ? n : 0) + 1;
            }
        }

        returned.RemoveAll(r => r.cage == null);
        foreach (var r in returned)
        {
            if (r.cage == null || r.cage.source == null) continue;
            string id = r.cage.source.Id;
            extra[id] = (extra.TryGetValue(id, out int n) ? n : 0) + 1;
        }
    }

    // A saved check goes back on the table and goes on with the time it still needed. False if the table is closed or full.
    public bool RestoreCheck(InvItemData data, float scale, float remaining)
    {
        if (!StartCheck(data, scale)) return false;

        var c = checks[checks.Count - 1];
        c.remaining = Mathf.Clamp(remaining, 0.5f, Mathf.Max(checkSeconds, remaining));
        c.text.text = string.Format(checkingFormat, FormatTime(c.remaining));
        return true;
    }

    void Update()
    {
        if (checks.Count == 0) return;

        bool open = IsOpen;
        Camera cam = Inventory.Instance != null && Inventory.Instance.playerCamera != null
            ? Inventory.Instance.playerCamera
            : Camera.main;

        for (int i = checks.Count - 1; i >= 0; i--)
        {
            Check c = checks[i];

            if (c.cage == null)
            {
                if (c.text != null) Destroy(c.text.gameObject);
                checks.RemoveAt(i);
                continue;
            }

            if (!c.done)
            {
                bool running = open || !pauseWhenClosed;
                if (running) c.remaining -= Time.deltaTime;

                c.text.text = string.Format(running ? checkingFormat : pausedFormat, FormatTime(c.remaining));

                if (c.remaining <= 0f) Complete(c);
            }
            else
            {
                c.doneTimer -= Time.deltaTime;
                if (c.doneTimer <= 0f)
                {
                    GiveBack(c);
                    Destroy(c.cage);
                    Destroy(c.text.gameObject);
                    checks.RemoveAt(i);
                    continue;
                }
            }

            // The text floats above the TOP of the cage (not inside it) and faces the player
            Bounds box = BoundsOf(c.cage);
            c.text.transform.position = new Vector3(box.center.x, box.max.y + textHeight, box.center.z);
            if (cam != null)
            {
                Vector3 away = c.text.transform.position - cam.transform.position;
                if (away.sqrMagnitude > 0.0001f) c.text.transform.rotation = Quaternion.LookRotation(away);
            }
        }
    }

    // The check is done: the player gets the point, the upgrades go up
    void Complete(Check c)
    {
        c.done = true;
        c.doneTimer = checkedShowSeconds;
        c.text.text = checkedText;

        // a cage that holds a Mirage's MEP (Lv 2) is worth more: the bonus of the Mirages waits until a cage is checked
        int earned = c.points;
        if (UpgradeSystem.Instance != null) earned += UpgradeSystem.Instance.TakeBonus(c.points);

        string summary = UpgradeSystem.Instance != null
            ? UpgradeSystem.Instance.AddPoints(earned)
            : "+" + earned + (earned == 1 ? " POINT" : " POINTS");

        if (Inventory.Instance != null)
            Inventory.Instance.ShowMessage(summary, 6f);
    }

    // The checked cage is gone: an EMPTY cage is put on the table in its place
    void GiveBack(Check c)
    {
        if (!giveBackEmptyCage) return;

        InvItemData empty = returnItem != null ? returnItem : FindEmptyFor(c.data);
        if (empty == null || empty.worldPrefab == null)
        {
            Debug.LogWarning("[DropTable] No empty cage item to give back. Assign Return Item on the DropTable.", this);
            return;
        }

        var prefab = empty.worldPrefab;
        var go = Instantiate(prefab, c.target, c.cage.transform.rotation);
        go.transform.localScale = prefab.transform.localScale * c.scale;
        go.SetActive(true);

        // the empty cage lands on the table top too: its own bottom is on the top
        Vector3 landing = SeatOnSurface(go, new Vector3(c.target.x, c.surface, c.target.z), c.surface);
        go.transform.position = landing + Vector3.up * (dropHeight * 0.5f);

        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var item in go.GetComponentsInChildren<InvItem>(true)) item.pickable = false;
        foreach (var old in go.GetComponentsInChildren<PlacedCage>(true)) Destroy(old);

        var placed = go.AddComponent<PlacedCage>();
        placed.source = empty;
        placed.onTable = true;

        returned.Add(new Returned { cage = placed, slot = c.slot });
        StartCoroutine(DropRoutine(go.transform, landing));

        if (Inventory.Instance != null)
            Inventory.Instance.ShowMessage(string.Format(giveBackMessage, empty.ShortName), 3f);
    }

    // The item that turns into the checked cage when it is filled (the Empty Faraday Cage)
    static InvItemData FindEmptyFor(InvItemData filled)
    {
        foreach (var d in Resources.FindObjectsOfTypeAll<InvItemData>())
            if (d != null && d.combineResult == filled) return d;
        return null;
    }

    // The height of the table top at a point: what a ray from above hits first (not the player). No table collider = the placement point
    float SurfaceHeight(Vector3 point)
    {
        Physics.SyncTransforms();

        var hits = Physics.RaycastAll(point + Vector3.up * 0.8f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (h.collider.GetComponentInParent<PlacedCage>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.NegativeInfinity ? best : point.y;
    }

    // Where to put an object so that its lowest point touches the surface height (at the given x and z)
    static Vector3 SeatOnSurface(GameObject obj, Vector3 xz, float surface)
    {
        obj.transform.position = new Vector3(xz.x, surface, xz.z);
        float bottom = BoundsOf(obj).min.y;
        return new Vector3(xz.x, surface + (surface - bottom) + 0.005f, xz.z);
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.3f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    IEnumerator DropRoutine(Transform cage, Vector3 target)
    {
        Vector3 start = cage.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, dropTime))
        {
            if (cage == null) yield break;

            // falls and settles with a small bounce
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float bounce = Mathf.Sin(t * Mathf.PI) * 0.05f * (1f - t);
            cage.position = Vector3.Lerp(start, target, eased) + Vector3.up * bounce;
            yield return null;
        }

        if (cage != null)
        {
            cage.position = target;
            Sfx.PlayAt("Box drop", target, 0.8f);       // the box lands on the table
        }
    }
}
