using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Plays one animation clip in a loop on a model that has an Animator (no Animator Controller to make).
// It is for models from .glb files (the Mirage ghost, the butterfly): put this on the model's root (the object with the Animator) and drag
// the clip into "Clip". Tools > Ghost > Use Butterfly Orb, MirageGhost And Muzzle Orb does it for you.
public class AnimatedModel : MonoBehaviour
{
    [Tooltip("The animation to play (open the .glb in the Project window and take the clip inside it)")]
    public AnimationClip clip;
    [Tooltip("1 = normal speed")]
    public float speed = 1f;
    [Tooltip("Every copy starts at a different moment, so copies do not move together")]
    public bool randomStart = true;

    private Animator animator;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private bool added;

    void OnEnable()
    {
        Build();
    }

    void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    void Build()
    {
        if (clip == null || graph.IsValid()) return;

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
            added = true;
        }

        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        graph = PlayableGraph.Create("AnimatedModel " + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetSpeed(speed);
        output.SetSourcePlayable(playable);

        if (randomStart && clip.length > 0.01f) playable.SetTime(Random.value * clip.length);
        graph.Play();
    }

    void Update()
    {
        if (!graph.IsValid() || clip == null) return;

        // a loop: back to the start when the clip has ended
        double time = playable.GetTime();
        if (clip.length > 0.01f && time >= clip.length)
            playable.SetTime(time % clip.length);
    }
}
