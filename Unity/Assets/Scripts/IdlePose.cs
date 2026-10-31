using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// A calm standing idle for a humanoid model with Mixamo bone names (mixamorig:Hips, mixamorig:LeftArm ...), made by code.
// The Mirage model came with a Mixamo DANCE as its only animation (arms swing up to 100 degrees), so its clip is not played any more:
// the arms are lowered to hang at the sides (found from the bones' positions, so it does not depend on how the file was exported) and
// the body breathes and sways a little, the head turns slowly. The movement is small and never repeats exactly.
// Put it on the model (the object above the bones). MirageGhost adds it by itself to a model that has the old AnimatedModel dance.
public class IdlePose : MonoBehaviour
{
    [Tooltip("How big the movement is (1 = a calm idle, 0 = a still statue)")]
    [Range(0f, 3f)] public float amount = 1f;
    [Tooltip("How fast it moves (1 = calm)")]
    [Range(0.1f, 3f)] public float speed = 1f;
    [Tooltip("How far the arms hang away from the body (degrees from straight down)")]
    [Range(0f, 40f)] public float armsAway = 9f;

    private class Part
    {
        public Transform bone;
        public Quaternion baseLocal;
        public Vector3 axisRight, axisUp, axisForward;      // the model's axes, written in the bone's own frame
        public float pitch, yaw, roll;                      // degrees
        public float frequency, phase;
    }

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly List<Part> parts = new List<Part>();
    private float seed;

    void Awake()
    {
        seed = Random.value * 100f;

        // what came with the model (the dance) must not play
        foreach (var legacy in GetComponentsInChildren<Animation>(true)) { legacy.Stop(); legacy.enabled = false; }
        foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var clipPlayer in GetComponentsInChildren<AnimatedModel>(true)) clipPlayer.enabled = false;

        Index();
        if (!bones.ContainsKey("Hips")) { enabled = false; return; }     // not a Mixamo model: nothing to do

        HangArms();
        Setup();
    }

    // "mixamorig:LeftArm_09" -> "LeftArm"  (a glb export adds a number to the bone names)
    void Index()
    {
        var pattern = new Regex(@"^mixamorig:(\w+?)(?:_\d+)?$");
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            var m = pattern.Match(t.name);
            if (m.Success && !bones.ContainsKey(m.Groups[1].Value)) bones[m.Groups[1].Value] = t;
        }
    }

    Transform Bone(string key)
    {
        bones.TryGetValue(key, out Transform t);
        return t;
    }

    // The T-pose becomes a standing pose: each arm is turned so that it points down (a little away from the body)
    void HangArms()
    {
        var spine = Bone("Spine2") ?? Bone("Spine1") ?? Bone("Spine");
        foreach (var side in new[] { "Left", "Right" })
        {
            var arm = Bone(side + "Arm");
            var fore = Bone(side + "ForeArm");
            if (arm == null || fore == null) continue;

            Vector3 current = (fore.position - arm.position).normalized;
            Vector3 away = spine != null ? arm.position - spine.position : current;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;

            Vector3 target = (Vector3.down + away * Mathf.Tan(armsAway * Mathf.Deg2Rad)).normalized;
            arm.rotation = Quaternion.FromToRotation(current, target) * arm.rotation;
        }
    }

    void Setup()
    {
        //  bone          pitch yaw roll  speed  phase
        Add("Hips",          0.8f, 1.4f, 1.0f, 0.30f, 0.0f);
        Add("Spine",         1.0f, 1.0f, 0.8f, 0.45f, 0.4f);
        Add("Spine1",        1.2f, 0.8f, 0.6f, 0.45f, 0.8f);
        Add("Spine2",        1.6f, 1.2f, 0.8f, 0.45f, 1.2f);       // the chest: the breath
        Add("Neck",          2.0f, 4.0f, 1.5f, 0.22f, 0.5f);
        Add("Head",          2.5f, 6.0f, 2.0f, 0.16f, 1.1f);       // looks slowly to one side and the other
        Add("LeftShoulder",  0.0f, 0.0f, 1.6f, 0.45f, 1.2f);
        Add("RightShoulder", 0.0f, 0.0f, 1.6f, 0.45f, 1.2f);
        Add("LeftArm",       1.5f, 0.0f, 2.5f, 0.28f, 0.3f);
        Add("RightArm",      1.5f, 0.0f, 2.5f, 0.28f, 2.1f);
        Add("LeftForeArm",   3.0f, 0.0f, 0.0f, 0.28f, 0.9f);
        Add("RightForeArm",  3.0f, 0.0f, 0.0f, 0.28f, 2.7f);
    }

    void Add(string key, float pitch, float yaw, float roll, float frequency, float phase)
    {
        var bone = Bone(key);
        if (bone == null) return;

        // the model's right, up and forward, written in the frame of the bone as it stands now
        Quaternion inverse = Quaternion.Inverse(bone.rotation);
        parts.Add(new Part
        {
            bone = bone,
            baseLocal = bone.localRotation,
            axisRight = inverse * transform.right,
            axisUp = inverse * transform.up,
            axisForward = inverse * transform.forward,
            pitch = pitch, yaw = yaw, roll = roll,
            frequency = frequency, phase = phase
        });
    }

    void LateUpdate()
    {
        float t = Time.time * speed + seed;

        foreach (var p in parts)
        {
            if (p.bone == null) continue;

            // three slow waves that do not repeat together
            float a = Mathf.Sin(t * p.frequency * 6.2832f + p.phase);
            float b = Mathf.Sin(t * p.frequency * 4.4f + p.phase * 1.3f + 1f);
            float c = Mathf.Sin(t * p.frequency * 3.1f + p.phase * 0.7f + 2f);

            var offset = Quaternion.AngleAxis(p.pitch * a * amount, p.axisRight) *
                         Quaternion.AngleAxis(p.yaw * b * amount, p.axisUp) *
                         Quaternion.AngleAxis(p.roll * c * amount, p.axisForward);
            p.bone.localRotation = p.baseLocal * offset;
        }
    }
}
