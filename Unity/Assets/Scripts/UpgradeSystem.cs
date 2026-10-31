using System;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

// Upgrades. Every Faraday Cage that was checked on the Drop Table is 1 point.
// The player spends each point in the Upgrade Panel (key U): he picks ONE item, and that item
// gets a random amount between its Min and Max Gain (percent).
//   Gun   : its percent is the gun's Beam Power (starts at the value set on GhostGun, e.g. 10%)
//   EM    : the generator works faster the higher its percent is
//   MEP   : 3 levels (Level 2 at 50%, Level 3 at 100%)
//   Radio : percent and levels, for your own use
// Each entry is an "item character": add your own entries, change the gains, the levels, or turn gains off.
// Add with Tools > Ghost > Setup Upgrades And Drop Table.
public class UpgradeSystem : MonoBehaviour
{
    public static UpgradeSystem Instance { get; private set; }

    // Raised whenever a percent or level changed (the inventory refreshes its tooltips and icons)
    public static event Action Changed;

    public enum Driver
    {
        None,
        [Tooltip("The percent IS the gun's Beam Power")]
        GunBeamPower,
        [Tooltip("The higher the percent, the faster the generator makes capsules")]
        GeneratorSpeed
    }

    [Serializable]
    public class Upgrade
    {
        [Tooltip("The name items use to link here (InvItemData > Upgrade Id)")]
        public string id = "Item";
        [Tooltip("Shown in the Upgrade Panel and in messages")]
        public string displayName = "Item";
        [Tooltip("Your drawn picture, shown in the Upgrade Panel")]
        public Sprite icon;
        public Driver driver = Driver.None;

        [Tooltip("Where it starts (percent). The Gun starts at the Beam Power set on GhostGun")]
        public float startPercent = 0f;
        public float maxPercent = 100f;

        [Tooltip("Can the player spend points on this item? (off = it is not in the Upgrade Panel)")]
        public bool gainsFromPoints = true;
        [Tooltip("Every point spent on it adds a random amount between Min and Max (percent)")]
        public float minGain = 1f;
        public float maxGain = 3f;

        [Tooltip("Percent needed for each level: {0, 50, 100} = Level 1 from the start, Level 2 at 50%, Level 3 at 100%. Empty = no levels")]
        public float[] levelThresholds;

        [Tooltip("Called with the new level when a level is reached (e.g. play a sound)")]
        public UnityEvent<int> onLevelChanged;

        [NonSerialized] public float percent;
        [NonSerialized] public int level;
    }

    [Header("Upgrades")]
    public Upgrade[] upgrades =
    {
        new Upgrade { id = "Gun",   displayName = "Gun",   driver = Driver.GunBeamPower,   startPercent = 10f },
        new Upgrade { id = "EM",    displayName = "EM",    driver = Driver.GeneratorSpeed, startPercent = 0f },
        new Upgrade { id = "MEP",   displayName = "MEP",   driver = Driver.None,           startPercent = 0f, levelThresholds = new float[] { 0f, 50f, 100f } },
        new Upgrade { id = "Radio", displayName = "Radio", driver = Driver.None,           startPercent = 0f },
    };

    [Header("Effects")]
    [Tooltip("At 100% EM the generator needs this much less time (0.6 = 60% faster)")]
    [Range(0f, 0.95f)] public float generatorMaxSpeedBonus = 0.6f;

    [Tooltip("Shown in the Console and as a message after every submitted cage")]
    public bool logChanges = true;

    [Header("MEP: how many the generator makes in one night")]
    [Tooltip("The player can make this many MEP in a night to begin with")]
    public int mepStart = 3;
    [Tooltip("The most MEP in a night")]
    public int mepMax = 6;
    [Tooltip("Points for the first step (3 > 4)")]
    public int mepFirstCost = 10;
    [Tooltip("Every next step costs this much more (4 > 5 = 20, 5 > 6 = 30)")]
    public int mepCostStep = 10;

    public const string MepId = "MEP";

    // How many MEP the player may make in one night (the MEP upgrade is a number now: 3, 4, 5 or 6)
    public int MepCount
    {
        get
        {
            var u = Find(MepId);
            return u != null ? Mathf.Clamp(Mathf.RoundToInt(u.percent), mepStart, mepMax) : mepStart;
        }
    }

    // The points the next step costs (0 = already at the most)
    public int MepNextCost
    {
        get { return MepCount >= mepMax ? 0 : mepFirstCost + (MepCount - mepStart) * mepCostStep; }
    }

    // An old saved game keeps a percent (0..100) for the MEP: that is not a number of MEP, so it starts again at the beginning
    float NormalizeMep(float value)
    {
        int v = Mathf.RoundToInt(value);
        return v < mepStart || v > mepMax ? mepStart : v;
    }

    [Tooltip("{0} = points earned")]
    public string pointMessage = "+{0} POINT   (press U to upgrade)";

    // A saved game was loaded: the percents and the points are put back
    public void Restore(string[] ids, float[] percents, int points, int totalPoints)
    {
        if (ids != null && percents != null)
        {
            for (int i = 0; i < ids.Length && i < percents.Length; i++)
            {
                var u = Find(ids[i]);
                if (u == null) continue;
                u.percent = u.id == MepId ? NormalizeMep(percents[i]) : Mathf.Clamp(percents[i], 0f, u.maxPercent);
                u.level = LevelFor(u);
                Apply(u);
            }
        }

        Points = Mathf.Max(0, points);
        TotalPoints = Mathf.Max(Points, totalPoints);
        Changed?.Invoke();
    }

    // Points the player has not spent yet
    public int Points { get; private set; }
    public int TotalPoints { get; private set; }

    // The names of the five things of the game, as the player sees them (the Upgrade panel, the messages). They are set here, so the game scene
    // and the training scene both have them, whatever name an Upgrade has in the scene. The Myanmar names are in Tools/burmese/translations.json
    public static string NameFor(string id)
    {
        switch (id)
        {
            case "Gun": return "Proton Emitter Gun";
            case "Radio": return "EMF Scanner";
            case "EM": return "EM-Tethering Machine";
            case "MEP": return "Molecular Encapsulation Phial - MEP";
            default: return null;
        }
    }

    void Awake()
    {
        Instance = this;

        if (upgrades != null)
            foreach (var u in upgrades)
            {
                string nice = u != null ? NameFor(u.id) : null;
                if (nice != null) u.displayName = nice;
            }

        // the pictures made for each upgrade (Resources/UpgradeIcons: Gun, EM, MEP, Radio) replace the old ones
        foreach (var u in upgrades)
        {
            if (u == null) continue;
            var sprite = Resources.Load<Sprite>("UpgradeIcons/" + u.id);
            if (sprite != null) u.icon = sprite;
        }
    }

    void Start()
    {
        foreach (var u in upgrades)
        {
            if (u == null) continue;

            u.percent = u.startPercent;

            // The MEP is a number of MEP a night (3 to begin with), not a percent
            if (u.id == MepId)
            {
                u.percent = mepStart;
                u.maxPercent = mepMax;
                u.levelThresholds = null;
                u.gainsFromPoints = true;
            }

            // The gun begins at the Beam Power you set on GhostGun
            if (u.driver == Driver.GunBeamPower && GhostGun.Instance != null)
                u.percent = GhostGun.Instance.beamPower;

            u.percent = Mathf.Clamp(u.percent, 0f, u.maxPercent);
            u.level = LevelFor(u);
            Apply(u);
        }

        Changed?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Extra points that wait for the next checked cage: a Mirage (Lv 2 ghost) is worth more than an Orb
    private int bonusWaiting;

    public void AddBonus(int points)
    {
        bonusWaiting += Mathf.Max(0, points);
    }

    // The cage that was just checked takes the waiting bonus with it (1 cage = 1 MEP = at most the bonus of 1 Mirage)
    public int TakeBonus(int most)
    {
        int taken = Mathf.Clamp(bonusWaiting, 0, Mathf.Max(0, most));
        bonusWaiting -= taken;
        return taken;
    }

    // A checked cage was handed in: the player gets points to spend. Returns the text for the player
    public string AddPoints(int points)
    {
        Points += points;
        TotalPoints += points;
        AgentStats.AddCage(points);
        Changed?.Invoke();

        if (logChanges) Debug.Log("[Upgrade] +" + points + " point(s). Points to spend: " + Points);
        return string.Format(pointMessage, points);
    }

    // Points paid for something in the shop (the Inhaler). False when there are not enough
    public bool TrySpendPoints(int cost)
    {
        if (cost <= 0 || Points < cost) return false;

        Points -= cost;
        Changed?.Invoke();
        if (logChanges) Debug.Log("[Upgrade] " + cost + " point(s) spent in the shop. Points to spend: " + Points);
        return true;
    }

    // The player spends 1 point on one item: it goes up by a random Min..Max percent.
    // Returns the text to show, or null if there is no point to spend
    public string Spend(string id)
    {
        var u = Find(id);
        if (u != null && u.id == MepId) return SpendMep(u);
        if (u == null || !u.gainsFromPoints || Points <= 0) return null;

        if (u.percent >= u.maxPercent)
            return u.displayName + " is already at the maximum";

        Points--;

        float before = u.percent;
        int levelBefore = u.level;

        float low = Mathf.Min(u.minGain, u.maxGain);
        float high = Mathf.Max(u.minGain, u.maxGain);
        u.percent = Mathf.Min(u.maxPercent, u.percent + UnityEngine.Random.Range(low, high));
        u.level = LevelFor(u);

        float gained = u.percent - before;
        var text = new StringBuilder();
        text.Append(u.displayName).Append(" +").Append(gained.ToString("0.#")).Append("%  >  ").Append(u.percent.ToString("0.#")).Append("%");

        if (u.level > levelBefore)
        {
            text.Append("   LEVEL ").Append(u.level).Append("!");
            u.onLevelChanged?.Invoke(u.level);
        }

        Apply(u);
        Changed?.Invoke();

        if (logChanges) Debug.Log("[Upgrade] " + text + "   (points left: " + Points + ")");
        return text.ToString();
    }

    // MEP 3 > 4 costs 10 points, 4 > 5 costs 20, 5 > 6 costs 30
    string SpendMep(Upgrade u)
    {
        int count = MepCount;
        if (count >= mepMax) return "MEP is already at the most: " + mepMax + " a night";

        int cost = MepNextCost;
        if (Points < cost) return "Not enough points: " + cost + " are needed for " + (count + 1) + " MEP a night";

        Points -= cost;
        u.percent = count + 1;
        u.level = LevelFor(u);
        Changed?.Invoke();

        string text = "MEP " + count + " > " + (count + 1) + " a night   (-" + cost + " points)";
        if (logChanges) Debug.Log("[Upgrade] " + text + "   (points left: " + Points + ")");
        return text;
    }

    public Upgrade Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var u in upgrades)
            if (u != null && string.Equals(u.id, id, StringComparison.OrdinalIgnoreCase)) return u;
        return null;
    }

    public float GetPercent(string id)
    {
        var u = Find(id);
        return u != null ? u.percent : 0f;
    }

    // 0 = this upgrade has no levels
    public int GetLevel(string id)
    {
        var u = Find(id);
        return u != null ? u.level : 0;
    }

    // "Level 2  -  57.3%" (or just the percent when there are no levels)
    public string Describe(string id)
    {
        var u = Find(id);
        if (u == null) return "";

        string percent = u.percent.ToString("0.#") + "%";
        return u.level > 0 ? "Level " + u.level + "  -  " + percent : percent;
    }

    int LevelFor(Upgrade u)
    {
        // the MEP: level 1 at 3 a night, 2 at 4, 3 at 5, 4 at 6
        if (u.id == MepId) return Mathf.Max(1, Mathf.RoundToInt(u.percent) - mepStart + 1);

        if (u.levelThresholds == null || u.levelThresholds.Length == 0) return 0;

        int level = 0;
        for (int i = 0; i < u.levelThresholds.Length; i++)
        {
            if (u.percent >= u.levelThresholds[i]) level = i + 1;
        }
        return level;
    }

    void Apply(Upgrade u)
    {
        switch (u.driver)
        {
            case Driver.GunBeamPower:
                if (GhostGun.Instance != null)
                    GhostGun.Instance.beamPower = Mathf.Clamp(u.percent, 0f, GhostGun.Instance.maxBeamPower);
                break;

            case Driver.GeneratorSpeed:
                float bonus = generatorMaxSpeedBonus * Mathf.Clamp01(u.percent / 100f);
                foreach (var generator in FindObjectsByType<BabyGhostGenerator>(FindObjectsSortMode.None))
                    generator.SetSpeedBonus(bonus);
                break;
        }
    }
}
