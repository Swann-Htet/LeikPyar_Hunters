using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Put on the generator. Walk a leashed baby ghost (see GhostGun) into the Put Position:
// the generator works for Produce Time seconds, then a capsule appears at the Produce Position.
// The baby is used up and comes back the next night.
public class BabyGhostGenerator : MonoBehaviour
{
    [Header("Positions (found by child name if empty)")]
    [Tooltip("Where the baby ghost is put. Child named 'Put Position'")]
    public Transform putPosition;
    [Tooltip("Where the capsule appears. Child named 'Produce Position'")]
    public Transform producePosition;
    [Tooltip("More places where a ghost can be put. Every child named 'Put...' (Put 2, Put 3...) is also used automatically")]
    public Transform[] extraPutPositions;

    [Header("Production")]
    [Tooltip("The capsule to create. Empty = a simple placeholder capsule")]
    public GameObject capsulePrefab;
    [Tooltip("Seconds the generator works before the capsule appears")]
    public float produceTime = 9f;
    [Tooltip("On: the whole run (start + working + shutdown) takes Produce Time Override seconds, whatever Produce Time says")]
    public bool overrideProduceTime = true;
    [Tooltip("The whole generator run in seconds: Start sound + Working loop + Shutdown sound = 9")]
    public float produceTimeOverride = 9f;
    [Tooltip("Seconds the baby takes to settle on the Put Position")]
    public float flyTime = 0.5f;

    [Header("Put VFX (drag a particle prefab, e.g. CFXR Electrified 3)")]
    [Tooltip("Plays at the Put Position the moment a ghost is put, until the capsule appears")]
    public GameObject putVfx;
    [Tooltip("Moves the Put VFX from the Put Position (world meters)")]
    public Vector3 putVfxOffset = Vector3.zero;
    [Tooltip("Plays once at the capsule when it appears (optional)")]
    public GameObject doneVfx;
    [Tooltip("Seconds before the Done VFX is removed")]
    public float doneVfxLifetime = 3f;

    [Header("Capsule Appear Animation (positions are local to the Produce Position)")]
    [Tooltip("Where the capsule starts")]
    public Vector3 capsuleStartOffset = new Vector3(0f, 0f, 0.95f);
    [Tooltip("Where it slides to and stays")]
    public Vector3 capsuleEndOffset = Vector3.zero;
    [Tooltip("The capsule's rotation (degrees, local to the Produce Position)")]
    public Vector3 capsuleRotation = new Vector3(0f, 90f, 0f);
    [Tooltip("Seconds the capsule takes to slide in while fading in")]
    public float appearTime = 1.2f;
    [Tooltip("The capsule keeps spinning (off = it stays at the rotation above)")]
    public bool spinCapsule = false;
    [Tooltip("The capsule as an inventory item (e.g. the MEP item asset). Its name and picture are used")]
    public InvItemData capsuleItem;
    [Tooltip("Or just a picture of the capsule in the inventory")]
    public Sprite capsuleIcon;
    [Tooltip("The capsule's name in the inventory (same name = they stack)")]
    public string capsuleItemName = "Capsule";
    [Tooltip("Makes the fade work in the WebGL build. Create it with Tools > Ghost > Create Capsule Fade Material")]
    public Material fadeMaterial;

    [Header("Put")]
    [Tooltip("The baby is put into the generator when it is this close to the Put Position (meters)")]
    public float putRadius = 1.5f;

    [Header("Mirage (Lv 2 ghost)")]
    [Tooltip("The generator takes a Mirage only when the EM upgrade is at least this percent. An Orb (Lv 1) needs nothing. 0 = a Mirage goes in at once")]
    public float mirageNeedsEm = 10f;
    [Tooltip("A Mirage is worth this many points more than an Orb (it is added to the next cage that is checked)")]
    public int mirageBonusPoints = 1;
    [Tooltip("{0} = the EM percent needed, {1} = the EM percent now")]
    public string mirageLockedPrompt = "The EM needs an upgrade to take a Mirage ({1}% / {0}%). Press U at the NPC";

    [Header("MEP a night")]
    [Tooltip("ON: only as many MEP can be made in one night as the MEP upgrade says (3 to begin with, up to 6). The training has no limit")]
    public bool limitPerNight = true;
    [Tooltip("The warning when the player puts one ghost too many in the generator (the MEP of tonight are all made)")]
    public string noMepText = "You don't have enough MEP left";

    private int madeTonight;
    private int countedNight = -1;

    [Header("Player")]
    [Tooltip("The countdown is shown when the player is this close (meters)")]
    public float useRange = 6f;
    [Tooltip("{0} = seconds left")]
    public string producingPrompt = "Producing capsule... {0:0.0}s";

    [Header("Sounds (Start / Working / Shutdown: empty = the default in Resources/Sfx)")]
    [Tooltip("1. Plays once when a ghost is put in and the machine starts")]
    public AudioClip startClip;
    [Tooltip("2. Loops while the machine works (it begins when the Start sound ends)")]
    public AudioClip workingClip;
    [Tooltip("3. Plays once when the work is done and the machine shuts down")]
    public AudioClip shutdownClip;
    [Tooltip("4. YOUR sound for the produced capsule (MEP tube). It plays the moment the capsule appears, right after the Shutdown sound. No default: empty = silent")]
    public AudioClip producedClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private AudioSource startSource;
    private AudioSource workingSource;
    private AudioSource shutdownSource;
    private AudioSource producedSource;

    private Bounds bodyBounds;
    private bool hasBounds;
    private float nextHintLog;
    private float baseProduceTime;
    private bool baseProduceTimeSet;
    private readonly List<Transform> putPoints = new List<Transform>();
    private bool busy;
    private float timeLeft;
    private GhostSpawner spawner;

    // The EM upgrade: 0.3 = 30% less production time
    public void SetSpeedBonus(float bonus)
    {
        if (!baseProduceTimeSet)
        {
            baseProduceTime = overrideProduceTime ? produceTimeOverride : produceTime;
            baseProduceTimeSet = true;
        }
        produceTime = baseProduceTime * Mathf.Clamp01(1f - bonus);
    }

    void Start()
    {
        if (!baseProduceTimeSet)
        {
            baseProduceTime = overrideProduceTime ? produceTimeOverride : produceTime;
            baseProduceTimeSet = true;
            produceTime = baseProduceTime;
        }

        spawner = FindFirstObjectByType<GhostSpawner>();

        // 3D sounds: louder when the player is close to the machine
        startSource = Sfx.AddSource(gameObject, true, false, sfxVolume);
        workingSource = Sfx.AddSource(gameObject, true, true, sfxVolume);
        shutdownSource = Sfx.AddSource(gameObject, true, false, sfxVolume);
        producedSource = Sfx.AddSource(gameObject, true, false, sfxVolume);

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (putPosition == null && n.Contains("put")) putPosition = t;
            if (producePosition == null && n.Contains("produce")) producePosition = t;
        }

        if (putPosition == null || producePosition == null)
            Debug.LogWarning("[BabyGhostGenerator] Put Position or Produce Position not found. Assign them on '" + name + "'.", this);

        BuildPutPoints();

        // VFX templates that sit in the scene (not prefab assets) must not play on their own
        if (putVfx != null && putVfx.scene.IsValid()) putVfx.SetActive(false);
        if (doneVfx != null && doneVfx.scene.IsValid()) doneVfx.SetActive(false);

        // A capsule template that sits in the scene (not a prefab asset) must not show up on its own
        if (capsulePrefab != null && capsulePrefab.scene.IsValid())
            capsulePrefab.SetActive(false);

        // The machine's body: the ghost also counts as put when it touches it, even if the Put Position sits deep inside the model
        var renderers = GetComponentsInChildren<Renderer>(false);
        foreach (var r in renderers)
        {
            if (!hasBounds) { bodyBounds = r.bounds; hasBounds = true; }
            else bodyBounds.Encapsulate(r.bounds);
        }
        if (hasBounds && bodyBounds.size.magnitude > 12f) hasBounds = false;
    }

    // Shows the Put zone (green) and where the capsule appears (blue) in the Scene view
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) BuildPutPoints();

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        if (putPoints.Count == 0)
            Gizmos.DrawWireSphere(PutPoint, putRadius);
        foreach (var put in putPoints)
            if (put != null) Gizmos.DrawWireSphere(put.position, putRadius);

        if (producePosition != null)
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireSphere(producePosition.position, 0.2f);
        }
    }

    void Update()
    {
        GhostGun gun = GhostGun.Instance;
        if (gun == null) return;

        if (busy)
        {
            if (PlayerNear(gun))
                InteractPrompt.Request(string.Format(producingPrompt, Mathf.Max(0f, timeLeft)));
            return;
        }

        // The leashed baby reached the Put Position (or touches the machine itself)
        Transform baby = gun.LeashedBaby;
        if (baby == null) return;

        float toPut;
        Transform chosenPut = NearestPut(baby.position, out toPut);
        float toBody = hasBounds ? Mathf.Sqrt(bodyBounds.SqrDistance(baby.position)) : float.MaxValue;

        if (toPut > putRadius && toBody > putRadius)
        {
            // Close but not close enough: say how far it is (the green circle in the Scene view is the Put zone)
            if (Mathf.Min(toPut, toBody) <= putRadius + 8f && Time.time >= nextHintLog)
            {
                nextHintLog = Time.time + 2f;
                Debug.Log("[BabyGhostGenerator] '" + name + "': the ghost is " + toPut.ToString("F1") + "m from Put Position and " +
                          (hasBounds ? toBody.ToString("F1") : "?") + "m from the machine body. It must be within " + putRadius +
                          "m. Check the green circle in the Scene view.", this);
            }
            return;
        }

        // A Mirage (Lv 2) is only taken by an upgraded EM: an Orb (Lv 1) always goes in
        bool mirage = baby.GetComponent<MirageGhost>() != null;
        if (mirage && !MirageAllowed(out float now))
        {
            if (PlayerNear(gun)) InteractPrompt.Request(string.Format(mirageLockedPrompt, mirageNeedsEm.ToString("0.#"), now.ToString("0.#")));
            return;
        }

        // only so many MEP a night (the MEP upgrade): the ghost stays on the leash, nothing is used up
        int made, most;
        if (MepLimitReached(out made, out most))
        {
            if (PlayerNear(gun)) InteractPrompt.Request(noMepText);
            return;
        }
        madeTonight++;

        gun.ReleaseBaby();
        if (spawner != null) spawner.CollectBaby(baby);
        StartCoroutine(ProcessRoutine(baby, chosenPut, mirage));
    }

    // True when the MEP of tonight are all made. The count starts again with every night (see GhostSpawner.NightNumber)
    bool MepLimitReached(out int made, out int most)
    {
        made = 0;
        most = 0;
        if (!limitPerNight || UpgradeSystem.Instance == null) return false;
        if (spawner != null && spawner.tutorialMode) return false;

        if (countedNight != GhostSpawner.NightNumber)
        {
            countedNight = GhostSpawner.NightNumber;
            madeTonight = 0;
        }

        made = madeTonight;
        most = UpgradeSystem.Instance.MepCount;
        return made >= most;
    }

    bool MirageAllowed(out float emPercent)
    {
        emPercent = UpgradeSystem.Instance != null ? UpgradeSystem.Instance.GetPercent("EM") : 100f;
        return mirageNeedsEm <= 0f || emPercent >= mirageNeedsEm;
    }

    // The first Put Position (kept for older scripts)
    public Vector3 PutPoint => putPoints.Count > 0 && putPoints[0] != null
        ? putPoints[0].position
        : (putPosition != null ? putPosition.position : transform.position + Vector3.up);

    // All Put Positions: the assigned one, the Extra list, and every child named "Put..."
    void BuildPutPoints()
    {
        putPoints.Clear();

        if (putPosition != null) putPoints.Add(putPosition);

        if (extraPutPositions != null)
            foreach (var t in extraPutPositions)
                if (t != null && !putPoints.Contains(t)) putPoints.Add(t);

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform || t == producePosition || putPoints.Contains(t)) continue;
            if (t.name.ToLowerInvariant().StartsWith("put")) putPoints.Add(t);
        }
    }

    Transform NearestPut(Vector3 position, out float distance)
    {
        Transform best = null;
        distance = float.MaxValue;

        foreach (var put in putPoints)
        {
            if (put == null) continue;
            float d = Vector3.Distance(position, put.position);
            if (d < distance) { distance = d; best = put; }
        }

        if (best == null)
            distance = Vector3.Distance(position, PutPoint);
        return best;
    }

    // Is this position within 'distance' of any Put Position? (used by the gun)
    public bool IsNear(Vector3 position, float distance)
    {
        float d;
        NearestPut(position, out d);
        return d <= distance;
    }

    bool PlayerNear(GhostGun gun)
    {
        if (gun.PlayerRoot == null) return false;
        float d;
        NearestPut(gun.PlayerRoot.position, out d);
        return d <= useRange;
    }

    GameObject SpawnVfx(GameObject template, Vector3 position)
    {
        if (template == null) return null;

        var fx = Instantiate(template, position, template.transform.rotation);
        fx.SetActive(true);
        return fx;
    }

    IEnumerator ProcessRoutine(Transform baby, Transform put, bool mirage)
    {
        busy = true;
        timeLeft = produceTime;

        // An Orb runs away from the player when the gun is hot (OrbRunner). On the generator it must stay where it is put:
        // the runner was moving it sideways off the machine, so it is switched off until the generator has used the soul up.
        var runner = baby.GetComponent<OrbRunner>();
        if (runner != null) runner.enabled = false;

        baby.gameObject.SetActive(true);
        Renderer[] renderers = baby.GetComponentsInChildren<Renderer>(true);
        Vector3 startScale = baby.localScale;
        Vector3 from = baby.position;
        Vector3 target = put != null ? put.position : PutPoint;

        // The VFX starts the moment the ghost is put
        GameObject putFx = SpawnVfx(putVfx, target + putVfxOffset);

        // 1) start, then 2) the working loop right after it
        AudioClip start = Sfx.Clip(startClip, "EMStart");
        Sfx.Play(startSource, start);
        Sfx.PlayLoopAfter(workingSource, Sfx.Clip(workingClip, "EMWorking"), start != null ? start.length : 0f);

        // 1) The baby settles on the Put Position
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, flyTime))
        {
            SetVisible(renderers, true);
            baby.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        baby.position = target;

        // 2) The generator works, using the baby up
        AudioClip shutdown = Sfx.Clip(shutdownClip, "EMShutdown");
        float shutdownLength = shutdown != null ? shutdown.length : 0f;
        bool shutdownPlayed = false;

        while (timeLeft > 0f)
        {
            // The Shutdown sound begins so that it ends exactly when the 9 seconds are over
            if (!shutdownPlayed && timeLeft <= shutdownLength)
            {
                shutdownPlayed = true;
                Sfx.Stop(workingSource);
                Sfx.Play(shutdownSource, shutdown);
            }

            SetVisible(renderers, true);
            timeLeft -= Time.deltaTime;
            float left = Mathf.Clamp01(timeLeft / Mathf.Max(0.01f, produceTime));
            baby.localScale = Vector3.Lerp(startScale * 0.4f, startScale, left);
            yield return null;
        }

        // 3) The capsule appears
        if (putFx != null) Destroy(putFx);

        // 3) the shutdown has just ended: the capsule (tube) appears with YOUR produced sound
        Sfx.Stop(workingSource);
        if (!shutdownPlayed) Sfx.Play(shutdownSource, shutdown);
        Sfx.Play(producedSource, Sfx.Clip(producedClip, "Tube"));     // the tube sound (Resources/Sfx/Tube) unless you gave your own
        SpawnCapsule();
        if (mirage && UpgradeSystem.Instance != null) UpgradeSystem.Instance.AddBonus(mirageBonusPoints);

        SetVisible(renderers, false);
        baby.localScale = startScale;
        if (runner != null) runner.enabled = true;
        busy = false;
    }

    void SpawnCapsule()
    {
        // The offsets are local to the Produce Position (position and rotation only, in meters)
        Transform frame = producePosition != null ? producePosition : transform;
        Vector3 basePos = producePosition != null ? producePosition.position : transform.position + Vector3.up;
        Quaternion baseRot = frame.rotation;

        Vector3 start = basePos + baseRot * capsuleStartOffset;
        Quaternion rotation = baseRot * Quaternion.Euler(capsuleRotation);

        GameObject capsule;
        if (capsulePrefab != null)
        {
            capsule = Instantiate(capsulePrefab, start, rotation);
        }
        else
        {
            capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetPositionAndRotation(start, rotation);
            capsule.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
            Destroy(capsule.GetComponent<Collider>());
        }

        capsule.SetActive(true);

        var ghostCapsule = capsule.GetComponent<GhostCapsule>();
        if (ghostCapsule == null) ghostCapsule = capsule.AddComponent<GhostCapsule>();
        if (!spinCapsule) ghostCapsule.spinSpeed = 0f;

        // The player takes it into the inventory (tag "inv" + InvItem)
        var invItem = capsule.GetComponent<InvItem>();
        if (invItem == null)
        {
            invItem = capsule.AddComponent<InvItem>();
            invItem.data = capsuleItem;
            invItem.itemName = capsuleItemName;
            invItem.icon = capsuleIcon;
        }
        try { capsule.tag = Inventory.TagName; }
        catch (UnityException) { Debug.LogWarning("[BabyGhostGenerator] The tag 'inv' does not exist yet. Run Tools > Ghost > Setup Inventory."); }

        Vector3 endPos = basePos + baseRot * capsuleEndOffset;

        if (doneVfx != null)
        {
            var fx = SpawnVfx(doneVfx, endPos);
            if (fx != null) Destroy(fx, doneVfxLifetime);
        }

        StartCoroutine(AppearRoutine(capsule, endPos));
    }

    // The capsule slides from its start to its end position while it fades in
    IEnumerator AppearRoutine(GameObject capsule, Vector3 end)
    {
        Renderer[] renderers = capsule.GetComponentsInChildren<Renderer>(true);
        var originals = new Material[renderers.Length][];
        var fades = new Material[renderers.Length][];
        bool fading = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            originals[i] = renderers[i].sharedMaterials;
            fades[i] = new Material[originals[i].Length];

            for (int j = 0; j < originals[i].Length; j++)
            {
                if (originals[i][j] == null) continue;

                var copy = new Material(originals[i][j]);
                MakeTransparent(copy);
                SetAlpha(copy, 0f);
                fades[i][j] = copy;
                fading = true;
            }
            renderers[i].materials = fades[i];
        }

        Vector3 start = capsule.transform.position;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, appearTime))
        {
            // The player took it before it finished
            if (capsule == null)
            {
                DestroyMaterials(fades);
                yield break;
            }

            float e = Mathf.SmoothStep(0f, 1f, t);
            capsule.transform.position = Vector3.Lerp(start, end, e);

            if (fading)
                foreach (var set in fades)
                    foreach (var m in set)
                        if (m != null) SetAlpha(m, e);

            yield return null;
        }

        if (capsule == null)
        {
            DestroyMaterials(fades);
            yield break;
        }

        capsule.transform.position = end;

        // Back to the normal, fully solid materials
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = originals[i];
        DestroyMaterials(fades);
    }

    static void DestroyMaterials(Material[][] sets)
    {
        foreach (var set in sets)
            foreach (var m in set)
                if (m != null) Destroy(m);
    }

    // Turns a URP/Lit (or standard) material transparent so its alpha can fade
    public static void MakeTransparent(Material m)
    {
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        // Preserve Specular keeps shiny/glowing parts solid while the rest fades: turn it off so everything fades
        if (m.HasProperty("_BlendModePreserveSpecular")) m.SetFloat("_BlendModePreserveSpecular", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetShaderPassEnabled("DepthOnly", false);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public static void SetAlpha(Material m, float alpha)
    {
        if (m.HasProperty("_BaseColor"))
        {
            Color c = m.GetColor("_BaseColor");
            c.a = alpha;
            m.SetColor("_BaseColor", c);
        }
        else if (m.HasProperty("_Color"))
        {
            Color c = m.GetColor("_Color");
            c.a = alpha;
            m.SetColor("_Color", c);
        }
    }

    static void SetVisible(Renderer[] renderers, bool visible)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = visible;
    }
}
